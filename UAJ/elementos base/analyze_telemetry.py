"""
Analizador de telemetría para Heartbeat / juego de sigilo y terror.

Lee logs JSONL generados por el Traker de Unity, JSON arrays enviados por servidor,
JSON individuales y CSV del CsvSerializer. Genera un resumen JSON preparado para
el dashboard_telemetria.html.

Uso típico:
    python analyze_telemetry.py ./telemetria --json-out resumen_telemetria.json
    python analyze_telemetry.py telemetry_abc.jsonl telemetry_def.jsonl --json-out resumen.json
    para ejecutar el py : python .\\analyze_telemetry.py "$env:USERPROFILE\\AppData\\LocalLow\\AppleAxion\\HeartBeat" --json-out ".\\resumen_telemetria.json" --pretty
"""

from __future__ import annotations

import argparse
import csv
import glob
import json
import math
import statistics
import sys
from collections import Counter, defaultdict, deque
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional, Tuple

STATE_NAMES = {
    0: "Normal",
    1: "Corriendo",
    2: "Andando",
    3: "Oculto",
    4: "Fatigado",
}

ITEM_NAMES = {
    0: "Pildora",
    1: "Caja",
    2: "Reloj",
    3: "Llave",
}

HIDEOUT_NAMES = {
    0: "Armario",
    1: "Caja",
}

TERMINAL_TYPES = {"PlayerDeath", "LevelComplete", "SessionEnd"}
POSITION_KEYS = ("posicion", "position", "pos")
GREEN_ZONE_KEYS = ("tamañoZonaVerde", "tamanoZonaVerde", "greenZoneSize", "green_zone_size")

# En Unity se guarda el buildIndex de la escena. En este proyecto los niveles
# jugables están montados cada 3 escenas: build 3 => nivel 1, build 6 => nivel 2,
# build 9 => nivel 3, build 12 => nivel 4, build 15 => nivel 5 y build 18 => nivel 6/final.
LEVEL_INDEX_TO_REAL = {3: 1, 6: 2, 9: 3, 12: 4, 15: 5, 18: 6}
REAL_LEVELS = {1, 2, 3, 4, 5, 6}



# Bounds del mapa completo en coordenadas Unity para alinear los fondos.
# Sin esto, si el jugador no recorre todo el mapa, el dashboard estira el recorrido
# hasta los bordes de la imagen usando solo min/max de puntos visitados.
LEVEL_WORLD_BOUNDS_OVERRIDES = {
    # Bounds sacados manualmente desde el objeto padre "Black Rooms" de cada escena.
    # En el JSON el eje vertical se llama min_z/max_z por compatibilidad con el dashboard,
    # pero en este juego 2D corresponde realmente a Y de Unity.
    "1": {"min_x": -10.677, "max_x": 32.932, "min_z": -43.540, "max_z": 11.643},
    "2": {"min_x": -6.657, "max_x": 51.907, "min_z": -39.610, "max_z": 12.771},
    "3": {"min_x": -30.740, "max_x": 20.226, "min_z": -40.496, "max_z": 7.682},
    "5": {"min_x": -3.528, "max_x": 50.493, "min_z": -67.548, "max_z": 7.700},
}


def apply_level_bounds_override(level: Any, bounds: Dict[str, Optional[float]]) -> Dict[str, Optional[float]]:
    override = LEVEL_WORLD_BOUNDS_OVERRIDES.get(str(level))
    if not override:
        return bounds
    merged = dict(bounds)
    merged.update(override)
    merged.setdefault("x_axis", bounds.get("x_axis", "x"))
    merged.setdefault("y_axis", bounds.get("y_axis", "y"))
    merged["bounds_source"] = "manual_level_world_bounds"
    return merged


def pad_bounds(bounds: Dict[str, Optional[float]], ratio: float = 0.06) -> Dict[str, Optional[float]]:
    if bounds.get("min_x") is None or bounds.get("min_z") is None:
        return bounds
    min_x = float(bounds["min_x"])
    max_x = float(bounds["max_x"])
    min_z = float(bounds["min_z"])
    max_z = float(bounds["max_z"])
    pad_x = max((max_x - min_x) * ratio, 0.5)
    pad_z = max((max_z - min_z) * ratio, 0.5)
    padded = dict(bounds)
    padded["min_x"] = min_x - pad_x
    padded["max_x"] = max_x + pad_x
    padded["min_z"] = min_z - pad_z
    padded["max_z"] = max_z + pad_z
    padded.setdefault("bounds_source", "data_bounds_with_padding")
    return padded


def as_float(value: Any, default: Optional[float] = None) -> Optional[float]:
    if value is None:
        return default
    try:
        if isinstance(value, str) and value.strip() == "":
            return default
        return float(value)
    except (TypeError, ValueError):
        return default


def as_int(value: Any, default: int = 0) -> int:
    try:
        if value is None or value == "":
            return default
        return int(float(value))
    except (TypeError, ValueError):
        return default


def as_bool(value: Any, default: bool = False) -> bool:
    if isinstance(value, bool):
        return value
    if value is None:
        return default
    if isinstance(value, (int, float)):
        return value != 0
    text = str(value).strip().lower()
    if text in {"true", "1", "yes", "y", "si", "sí"}:
        return True
    if text in {"false", "0", "no", "n"}:
        return False
    return default


def normalize_enum(value: Any, mapping: Dict[int, str]) -> str:
    if value is None:
        return "Desconocido"
    if isinstance(value, bool):
        return str(value)
    if isinstance(value, (int, float)) and int(value) in mapping:
        return mapping[int(value)]
    text = str(value).strip()
    if text == "":
        return "Desconocido"
    try:
        number = int(float(text))
        if number in mapping:
            return mapping[number]
    except ValueError:
        pass
    return text


def get_event_type(event: Dict[str, Any]) -> str:
    return str(event.get("tipoEvento") or event.get("eventType") or event.get("type") or event.get("event") or "Unknown")


def get_session_id(event: Dict[str, Any]) -> str:
    return str(event.get("idSesion") or event.get("sessionId") or event.get("session_id") or "sin_sesion")


def get_raw_level(event: Dict[str, Any]) -> str:
    level = event.get("nivel", event.get("level", "sin_nivel"))
    if level is None or str(level).strip() == "":
        return "sin_nivel"
    return str(level).strip()


def get_level(event: Dict[str, Any]) -> str:
    """Devuelve el nivel real del juego, no el buildIndex crudo de Unity."""
    raw = get_raw_level(event)
    if raw == "sin_nivel":
        return raw

    number = as_float(raw)
    if number is not None and math.isfinite(number) and float(number).is_integer():
        int_level = int(number)
        if int_level in LEVEL_INDEX_TO_REAL:
            return str(LEVEL_INDEX_TO_REAL[int_level])
        if int_level in REAL_LEVELS:
            return str(int_level)
        if int_level == -1:
            return "sin_nivel"
    return raw


def level_sort_key(level: Any) -> Tuple[int, int, str]:
    text = str(level)
    number = as_float(text)
    if number is not None and math.isfinite(number) and float(number).is_integer():
        return (0, int(number), text)
    if text == "sin_nivel":
        return (2, 0, text)
    return (1, 0, text)


def get_timestamp(event: Dict[str, Any]) -> int:
    return as_int(event.get("timestamp", event.get("time", 0)), 0)


def get_id_event(event: Dict[str, Any]) -> int:
    return as_int(event.get("idEvento", event.get("eventId", 0)), 0)


def get_state(event: Dict[str, Any]) -> str:
    return normalize_enum(event.get("estadoJugador", event.get("playerState")), STATE_NAMES)


def get_item_type(event: Dict[str, Any]) -> str:
    return normalize_enum(event.get("tipoItem", event.get("itemType")), ITEM_NAMES)


def get_hideout_type(event: Dict[str, Any]) -> str:
    return normalize_enum(event.get("tipoEscondite", event.get("hideoutType")), HIDEOUT_NAMES)


def get_green_zone(event: Dict[str, Any]) -> Optional[float]:
    for key in GREEN_ZONE_KEYS:
        if key in event:
            return as_float(event.get(key))
    return None


def get_position_from(value: Any) -> Optional[Dict[str, float]]:
    """Normaliza Vector3 de Unity a {x,y,z}. Acepta dict, lista o texto '(x,y,z)'."""
    if value is None:
        return None

    if isinstance(value, dict):
        x = as_float(value.get("x"))
        y = as_float(value.get("y"), 0.0)
        z = as_float(value.get("z"))
        if x is None or z is None:
            return None
        return {"x": x, "y": y or 0.0, "z": z}

    if isinstance(value, (list, tuple)) and len(value) >= 3:
        x = as_float(value[0])
        y = as_float(value[1], 0.0)
        z = as_float(value[2])
        if x is None or z is None:
            return None
        return {"x": x, "y": y or 0.0, "z": z}

    if isinstance(value, str):
        text = value.strip().strip("()[]{}")
        parts = [p.strip() for p in text.split(",")]
        if len(parts) >= 3:
            x = as_float(parts[0])
            y = as_float(parts[1], 0.0)
            z = as_float(parts[2])
            if x is not None and z is not None:
                return {"x": x, "y": y or 0.0, "z": z}
    return None


def get_position(event: Dict[str, Any], preferred_keys: Iterable[str] = POSITION_KEYS) -> Optional[Dict[str, float]]:
    for key in preferred_keys:
        if key in event:
            pos = get_position_from(event.get(key))
            if pos:
                return pos
    return None


def parse_payload(payload: str) -> Dict[str, Any]:
    """Parsea payload del CsvSerializer: campo1=valor;campo2=valor;"""
    result: Dict[str, Any] = {}
    if not payload:
        return result
    for part in payload.split(";"):
        if "=" not in part:
            continue
        key, value = part.split("=", 1)
        key = key.strip()
        value = value.strip()
        if not key:
            continue
        if value.lower() in {"true", "false"}:
            result[key] = as_bool(value)
        else:
            result[key] = value
    return result


def parse_csv(path: Path) -> List[Dict[str, Any]]:
    events: List[Dict[str, Any]] = []
    with path.open("r", encoding="utf-8-sig", newline="") as fh:
        reader = csv.DictReader(fh)
        for row in reader:
            if not row:
                continue
            payload = parse_payload(row.get("payload", ""))
            event = {**row, **payload}
            # Normalizamos nombres de columnas del CSV del proyecto.
            if "tipoEvento" not in event and "type" in event:
                event["tipoEvento"] = event["type"]
            events.append(event)
    return events


def parse_json_text(text: str, source: str) -> List[Dict[str, Any]]:
    text = text.strip()
    if not text:
        return []

    # JSON completo: objeto o array.
    try:
        parsed = json.loads(text)
        if isinstance(parsed, list):
            return [x for x in parsed if isinstance(x, dict)]
        if isinstance(parsed, dict):
            # Permite que alguien pase un informe ya generado o un wrapper de eventos.
            if isinstance(parsed.get("events"), list):
                return [x for x in parsed["events"] if isinstance(x, dict)]
            return [parsed]
    except json.JSONDecodeError:
        pass

    # JSON Lines: una línea por evento; cada línea también puede ser un array.
    events: List[Dict[str, Any]] = []
    for line_number, line in enumerate(text.splitlines(), start=1):
        line = line.strip().rstrip(",")
        if not line:
            continue
        try:
            parsed = json.loads(line)
        except json.JSONDecodeError as exc:
            print(f"[WARN] No se pudo parsear {source}:{line_number}: {exc}", file=sys.stderr)
            continue
        if isinstance(parsed, dict):
            events.append(parsed)
        elif isinstance(parsed, list):
            events.extend([x for x in parsed if isinstance(x, dict)])
    return events


def read_events_from_file(path: Path) -> List[Dict[str, Any]]:
    suffix = path.suffix.lower()
    if suffix == ".csv":
        return parse_csv(path)
    text = path.read_text(encoding="utf-8-sig", errors="replace")
    return parse_json_text(text, str(path))


def collect_files(paths: List[Path]) -> List[Path]:
    files: List[Path] = []
    for path in paths:
        if path.is_dir():
            for pattern in ("*.jsonl", "*.json", "*.csv", "*.log", "*.txt"):
                files.extend(path.rglob(pattern))
        elif path.is_file():
            files.append(path)
        else:
            matches = [Path(p) for p in sorted(glob.glob(str(path), recursive=True))]
            files.extend([p for p in matches if p.is_file()])
    # Quita duplicados preservando orden.
    seen = set()
    unique: List[Path] = []
    for file in files:
        resolved = file.resolve()
        if resolved not in seen:
            unique.append(file)
            seen.add(resolved)
    return unique


def pct(numerator: float, denominator: float) -> Optional[float]:
    if denominator == 0:
        return None
    return (numerator / denominator) * 100.0


def safe_mean(values: Iterable[Optional[float]]) -> Optional[float]:
    filtered = [v for v in values if v is not None and math.isfinite(float(v))]
    if not filtered:
        return None
    return float(statistics.mean(filtered))


def safe_median(values: Iterable[Optional[float]]) -> Optional[float]:
    filtered = [v for v in values if v is not None and math.isfinite(float(v))]
    if not filtered:
        return None
    return float(statistics.median(filtered))


def seconds_to_ms(value: Any) -> Optional[float]:
    v = as_float(value)
    if v is None:
        return None
    # Los eventos de Unity del proyecto guardan duracionSesion/tiempoCompletado en segundos.
    return v * 1000.0


def level_as_int(level: Any) -> Optional[int]:
    number = as_float(level)
    if number is None or not math.isfinite(number) or not float(number).is_integer():
        return None
    return int(number)


def has_estado_jugador_payload(event: Dict[str, Any]) -> bool:
    return event.get("estadoJugador") is not None or event.get("playerState") is not None


def get_route_position(event: Dict[str, Any]) -> Optional[Dict[str, float]]:
    """Devuelve una posición de jugador útil para recorrido, aunque no haya PlayerState explícito."""
    typ = get_event_type(event)
    if typ == "PlayerSpotted":
        return get_position(event, ("posicionJugador", "playerPosition", "posicion", "position"))
    if typ == "HeartbeatAttempt":
        return get_position(event, ("posicionJugador", "playerPosition", "posicion", "position"))
    if typ in {"PlayerState", "PlayerDeath", "FatigueTriggered", "FatigueEnded", "PlayerHidden", "ItemUsed", "ItemPicked"}:
        return get_position(event)
    return None


def is_route_candidate(event: Dict[str, Any], prefer_explicit_player_state: bool) -> bool:
    typ = get_event_type(event)
    if prefer_explicit_player_state:
        return typ == "PlayerState"
    # Si no hay PlayerState real, usamos eventos que ya guardan estado/posición de jugador
    # como aproximación del recorrido.
    return typ in {
        "PlayerState",
        "HeartbeatAttempt",
        "FatigueTriggered",
        "FatigueEnded",
        "PlayerDeath",
        "PlayerSpotted",
        "PlayerHidden",
        "ItemUsed",
        "ItemPicked",
    }


@dataclass
class StateSample:
    timestamp: int
    event: Dict[str, Any]


class TelemetryAnalyzer:
    def __init__(
        self,
        events: List[Dict[str, Any]],
        source_files: List[str],
        near_window_ms: int = 1500,
        after_spotted_window_ms: int = 5000,
        grid_size: int = 18,
    ) -> None:
        self.events = sorted(events, key=lambda e: (get_session_id(e), get_timestamp(e), get_id_event(e)))
        self.source_files = source_files
        self.near_window_ms = near_window_ms
        self.after_spotted_window_ms = after_spotted_window_ms
        self.grid_size = max(4, grid_size)
        self.by_session: Dict[str, List[Dict[str, Any]]] = defaultdict(list)
        for event in self.events:
            self.by_session[get_session_id(event)].append(event)
        self._inferred_level_completions: Optional[Dict[str, Dict[str, Any]]] = None

    def nearest_previous_state(self, session_events: List[Dict[str, Any]], index: int) -> Optional[Dict[str, Any]]:
        target_ts = get_timestamp(session_events[index])
        for j in range(index - 1, -1, -1):
            candidate = session_events[j]
            if get_event_type(candidate) != "PlayerState":
                continue
            delta = target_ts - get_timestamp(candidate)
            if delta < 0:
                continue
            if delta <= self.near_window_ms:
                return candidate
            return None
        return None

    def last_event_before(
        self,
        session_events: List[Dict[str, Any]],
        index: int,
        event_type: str,
        max_delta_ms: Optional[int] = None,
    ) -> Optional[Dict[str, Any]]:
        target_ts = get_timestamp(session_events[index])
        for j in range(index - 1, -1, -1):
            candidate = session_events[j]
            delta = target_ts - get_timestamp(candidate)
            if delta < 0:
                continue
            if max_delta_ms is not None and delta > max_delta_ms:
                return None
            if get_event_type(candidate) == event_type:
                return candidate
        return None

    def infer_level_completions(self) -> Dict[str, Dict[str, Any]]:
        """Infiere LevelComplete cuando una sesión arranca un nivel posterior.

        Ejemplo: LevelStart nivel 1 seguido de LevelStart nivel 2 implica que el
        último intento de nivel 1 terminó completado. No se infiere en reinicios
        del mismo nivel ni si ya hay LevelComplete explícito entre ambos starts.
        """
        if self._inferred_level_completions is not None:
            return self._inferred_level_completions

        inferred: Dict[str, Dict[str, Any]] = defaultdict(lambda: {
            "count": 0,
            "times_ms": [],
            "events": [],
        })

        for session_id, session_events in self.by_session.items():
            last_start: Optional[Dict[str, Any]] = None
            explicit_complete_since_last_start = False

            for event in session_events:
                typ = get_event_type(event)
                if typ == "LevelComplete" and last_start is not None:
                    if get_level(event) == get_level(last_start):
                        explicit_complete_since_last_start = True
                    continue

                if typ != "LevelStart":
                    continue

                if last_start is not None:
                    previous_level = get_level(last_start)
                    current_level = get_level(event)
                    previous_number = level_as_int(previous_level)
                    current_number = level_as_int(current_level)

                    if (
                        previous_number is not None
                        and current_number is not None
                        and current_number > previous_number
                        and not explicit_complete_since_last_start
                    ):
                        duration = get_timestamp(event) - get_timestamp(last_start)
                        if duration < 0:
                            duration = None
                        inferred[previous_level]["count"] += 1
                        if duration is not None:
                            inferred[previous_level]["times_ms"].append(duration)
                        inferred[previous_level]["events"].append({
                            "session": session_id,
                            "completed_level": previous_level,
                            "next_level": current_level,
                            "start_timestamp": get_timestamp(last_start),
                            "inferred_complete_timestamp": get_timestamp(event),
                            "duration_ms": duration,
                            "reason": "LevelStart de un nivel posterior",
                        })

                last_start = event
                explicit_complete_since_last_start = False

        self._inferred_level_completions = dict(inferred)
        return self._inferred_level_completions

    def state_payload_count(self) -> int:
        return sum(1 for event in self.events if has_estado_jugador_payload(event))

    def base_summary(self) -> Dict[str, Any]:
        type_counts = Counter(get_event_type(e) for e in self.events)
        timestamps = [get_timestamp(e) for e in self.events if get_timestamp(e) > 0]
        levels = sorted({get_level(e) for e in self.events}, key=level_sort_key)
        sessions = sorted(self.by_session.keys())
        return {
            "total_events": len(self.events),
            "source_files": self.source_files,
            "event_type_counts": dict(sorted(type_counts.items())),
            "sessions_count": len(sessions),
            "levels": levels,
            "time_span_ms": (max(timestamps) - min(timestamps)) if timestamps else None,
            "first_timestamp": min(timestamps) if timestamps else None,
            "last_timestamp": max(timestamps) if timestamps else None,
        }

    def level_metrics(self) -> Dict[str, Any]:
        levels: Dict[str, Any] = defaultdict(lambda: {
            "starts": 0,
            "completes": 0,
            "explicit_completes": 0,
            "inferred_completes": 0,
            "deaths": 0,
            "heartbeat_attempts": 0,
            "heartbeat_fails": 0,
            "spotted": 0,
            "fatigue": 0,
            "item_uses": 0,
            "hideout_entries": 0,
            "completion_times_ms": [],
            "explicit_completion_times_ms": [],
            "inferred_completion_times_ms": [],
        })
        for event in self.events:
            level = get_level(event)
            row = levels[level]
            typ = get_event_type(event)
            if typ == "LevelStart":
                row["starts"] += 1
            elif typ == "LevelComplete":
                row["completes"] += 1
                row["explicit_completes"] += 1
                ms = seconds_to_ms(event.get("tiempoCompletado"))
                if ms is not None:
                    row["completion_times_ms"].append(ms)
                    row["explicit_completion_times_ms"].append(ms)
            elif typ == "PlayerDeath":
                row["deaths"] += 1
            elif typ == "HeartbeatAttempt":
                row["heartbeat_attempts"] += 1
                if not as_bool(event.get("exito"), False):
                    row["heartbeat_fails"] += 1
            elif typ == "PlayerSpotted":
                row["spotted"] += 1
            elif typ == "FatigueTriggered":
                row["fatigue"] += 1
            elif typ == "ItemUsed":
                row["item_uses"] += 1
            elif typ == "PlayerHidden" and as_bool(event.get("entrando"), True):
                row["hideout_entries"] += 1

        inferred = self.infer_level_completions()
        for level, data in inferred.items():
            row = levels[level]
            count = int(data.get("count", 0))
            times = list(data.get("times_ms", []))
            row["completes"] += count
            row["inferred_completes"] += count
            row["completion_times_ms"].extend(times)
            row["inferred_completion_times_ms"].extend(times)

        output: Dict[str, Any] = {}
        for level, row in levels.items():
            times = row.pop("completion_times_ms")
            explicit_times = row.pop("explicit_completion_times_ms")
            inferred_times = row.pop("inferred_completion_times_ms")
            row["avg_completion_ms"] = safe_mean(times)
            row["avg_explicit_completion_ms"] = safe_mean(explicit_times)
            row["avg_inferred_completion_ms"] = safe_mean(inferred_times)
            row["heartbeat_fail_rate_pct"] = pct(row["heartbeat_fails"], row["heartbeat_attempts"])
            row["completion_rate_pct"] = pct(row["completes"], row["starts"])
            row["explicit_completion_rate_pct"] = pct(row["explicit_completes"], row["starts"])
            row["inferred_completion_rate_pct"] = pct(row["inferred_completes"], row["starts"])
            output[level] = row
        return dict(sorted(output.items(), key=lambda kv: level_sort_key(kv[0])))

    def heartbeat_metrics(self) -> Dict[str, Any]:
        attempts = [e for e in self.events if get_event_type(e) == "HeartbeatAttempt"]
        successes = [e for e in attempts if as_bool(e.get("exito"), False)]
        failures = [e for e in attempts if not as_bool(e.get("exito"), False)]
        green_all = [get_green_zone(e) for e in attempts]
        green_success = [get_green_zone(e) for e in successes]
        green_failure = [get_green_zone(e) for e in failures]

        bins = self.green_zone_bins(attempts)
        consecutive = []
        by_session = defaultdict(list)
        for event in attempts:
            by_session[get_session_id(event)].append(event)
        for session_events in by_session.values():
            current = 0
            for event in sorted(session_events, key=lambda e: (get_timestamp(e), get_id_event(e))):
                if not as_bool(event.get("exito"), False):
                    current += 1
                else:
                    if current:
                        consecutive.append(current)
                    current = 0
            if current:
                consecutive.append(current)

        low_bin = bins[0] if bins else None
        high_bin = bins[-1] if bins else None
        pressure_delta = None
        if low_bin and high_bin and low_bin.get("fail_rate_pct") is not None and high_bin.get("fail_rate_pct") is not None:
            pressure_delta = low_bin["fail_rate_pct"] - high_bin["fail_rate_pct"]

        return {
            "total_attempts": len(attempts),
            "successes": len(successes),
            "failures": len(failures),
            "fail_rate_pct": pct(len(failures), len(attempts)),
            "avg_green_zone": safe_mean(green_all),
            "avg_green_zone_success": safe_mean(green_success),
            "avg_green_zone_failure": safe_mean(green_failure),
            "median_green_zone_failure": safe_median(green_failure),
            "green_zone_bins": bins,
            "max_consecutive_failures": max(consecutive) if consecutive else 0,
            "avg_consecutive_failures": safe_mean(consecutive),
            "pressure_delta_fail_rate_pct": pressure_delta,
            "note": "El tamaño de zona verde se interpreta como proxy de distancia/presión. Si no equivale exactamente a distancia, la métrica valida la dificultad del pulso, no la proximidad real.",
        }

    def green_zone_bins(self, attempts: List[Dict[str, Any]], number_of_bins: int = 5) -> List[Dict[str, Any]]:
        values = [(get_green_zone(event), event) for event in attempts if get_green_zone(event) is not None]
        if not values:
            return []
        min_v = min(v for v, _ in values if v is not None)
        max_v = max(v for v, _ in values if v is not None)
        if math.isclose(min_v, max_v):
            total = len(values)
            fails = sum(1 for _, e in values if not as_bool(e.get("exito"), False))
            return [{
                "label": f"{min_v:.3g}",
                "min": min_v,
                "max": max_v,
                "attempts": total,
                "failures": fails,
                "fail_rate_pct": pct(fails, total),
            }]

        # Si parece normalizado, usamos rangos legibles 0-20%, etc. Si no, rangos min/max.
        start = 0.0 if min_v >= 0 and max_v <= 1.0 else min_v
        end = 1.0 if min_v >= 0 and max_v <= 1.0 else max_v
        width = (end - start) / number_of_bins
        bins: List[Dict[str, Any]] = []
        for i in range(number_of_bins):
            lo = start + i * width
            hi = end if i == number_of_bins - 1 else start + (i + 1) * width
            bucket = []
            for value, event in values:
                if value is None:
                    continue
                if i == number_of_bins - 1:
                    inside = lo <= value <= hi
                else:
                    inside = lo <= value < hi
                if inside:
                    bucket.append(event)
            failures = sum(1 for event in bucket if not as_bool(event.get("exito"), False))
            bins.append({
                "label": f"{lo:.2f}–{hi:.2f}",
                "min": lo,
                "max": hi,
                "attempts": len(bucket),
                "failures": failures,
                "fail_rate_pct": pct(failures, len(bucket)),
            })
        return bins

    def hideout_metrics(self) -> Dict[str, Any]:
        usage_by_id: Dict[str, Any] = defaultdict(lambda: {
            "entries": 0,
            "exits": 0,
            "type": "Desconocido",
            "level": "sin_nivel",
            "durations_ms": [],
            "used_with_enemy_near": 0,
            "used_after_spotted": 0,
        })
        total_entries = 0
        total_armario_entries = 0
        entries_with_enemy_near = 0
        entries_after_spotted = 0
        open_entries: Dict[Tuple[str, str], deque] = defaultdict(deque)
        inferred_enemy_near = 0
        direct_enemy_near = 0
        deaths_near_closet_not_hidden = 0

        for session_id, session_events in self.by_session.items():
            for i, event in enumerate(session_events):
                typ = get_event_type(event)
                if typ == "PlayerHidden":
                    entering = as_bool(event.get("entrando"), True)
                    hideout_id = str(event.get("idEscondite") or event.get("hideoutId") or "sin_id")
                    hideout_type = get_hideout_type(event)
                    row = usage_by_id[hideout_id]
                    row["type"] = hideout_type
                    row["level"] = get_level(event)
                    if entering:
                        total_entries += 1
                        if hideout_type == "Armario":
                            total_armario_entries += 1
                        row["entries"] += 1
                        open_entries[(session_id, hideout_id)].append(get_timestamp(event))

                        direct = event.get("cercaEnemigo")
                        if direct is not None:
                            enemy_near = as_bool(direct)
                            direct_enemy_near += 1
                        else:
                            state = self.nearest_previous_state(session_events, i)
                            enemy_near = bool(state and as_bool(state.get("cercaEnemigo"), False))
                            if state is not None:
                                inferred_enemy_near += 1
                        if enemy_near:
                            entries_with_enemy_near += 1
                            row["used_with_enemy_near"] += 1

                        spotted = self.last_event_before(session_events, i, "PlayerSpotted", self.after_spotted_window_ms)
                        if spotted is not None:
                            entries_after_spotted += 1
                            row["used_after_spotted"] += 1
                    else:
                        row["exits"] += 1
                        key = (session_id, hideout_id)
                        if open_entries[key]:
                            start_ts = open_entries[key].popleft()
                            duration = get_timestamp(event) - start_ts
                            if duration >= 0:
                                row["durations_ms"].append(duration)

                elif typ == "PlayerDeath":
                    state = self.nearest_previous_state(session_events, i)
                    death_state = get_state(event)
                    near_closet = bool(state and as_bool(state.get("cercaArmario"), False))
                    hidden = death_state == "Oculto" or (state and get_state(state) == "Oculto")
                    if near_closet and not hidden:
                        deaths_near_closet_not_hidden += 1

        rows = []
        for hideout_id, row in usage_by_id.items():
            durations = row.pop("durations_ms")
            entries = row["entries"]
            rows.append({
                "id": hideout_id,
                **row,
                "avg_duration_ms": safe_mean(durations),
                "median_duration_ms": safe_median(durations),
                "with_enemy_near_pct": pct(row["used_with_enemy_near"], entries),
                "used_after_spotted_pct": pct(row["used_after_spotted"], entries),
            })
        rows.sort(key=lambda r: (-r["entries"], str(r["id"])))
        return {
            "total_entries": total_entries,
            "armario_entries": total_armario_entries,
            "usage_by_hideout": rows,
            "avg_time_inside_ms": safe_mean([r["avg_duration_ms"] for r in rows]),
            "entries_with_enemy_near": entries_with_enemy_near,
            "entries_with_enemy_near_pct": pct(entries_with_enemy_near, total_entries),
            "entries_after_spotted": entries_after_spotted,
            "entries_after_spotted_pct": pct(entries_after_spotted, total_entries),
            "deaths_near_closet_not_hidden": deaths_near_closet_not_hidden,
            "direct_enemy_near_on_hidden_events": direct_enemy_near,
            "inferred_enemy_near_from_player_state": inferred_enemy_near,
            "unvisited_hideouts_available": False,
            "unvisited_hideouts_note": "No se puede calcular armarios no usados o menos usados si no existe un catálogo de todos los armarios del nivel. Con los eventos actuales solo se ven los armarios que aparecen en PlayerHidden.",
        }

    def fatigue_metrics(self) -> Dict[str, Any]:
        spotted_total = 0
        spotted_fatigued = 0
        spotted_by_state = Counter()
        death_fatigued = 0
        fatigue_episodes: List[Dict[str, Any]] = []

        for session_id, session_events in self.by_session.items():
            for i, event in enumerate(session_events):
                typ = get_event_type(event)
                if typ == "PlayerSpotted":
                    spotted_total += 1
                    state = get_state(event)
                    spotted_by_state[state] += 1
                    if state == "Fatigado":
                        spotted_fatigued += 1
                elif typ == "PlayerDeath":
                    state = get_state(event)
                    if state == "Fatigado":
                        death_fatigued += 1
                elif typ == "FatigueTriggered":
                    start_ts = get_timestamp(event)
                    terminal = None
                    for j in range(i + 1, len(session_events)):
                        candidate = session_events[j]
                        candidate_type = get_event_type(candidate)
                        if candidate_type in TERMINAL_TYPES:
                            terminal = candidate
                            break
                    if terminal is None:
                        outcome = "sin_evento_terminal"
                        time_to_terminal = None
                    else:
                        terminal_type = get_event_type(terminal)
                        time_to_terminal = max(0, get_timestamp(terminal) - start_ts)
                        if terminal_type == "PlayerDeath":
                            outcome = "muerte"
                        elif terminal_type == "LevelComplete":
                            outcome = "sobrevive_hasta_completar"
                        else:
                            outcome = "sobrevive_hasta_fin_sesion"
                    fatigue_episodes.append({
                        "session": session_id,
                        "level": get_level(event),
                        "timestamp": start_ts,
                        "outcome": outcome,
                        "time_to_terminal_ms": time_to_terminal,
                    })

        survival_count = sum(1 for ep in fatigue_episodes if ep["outcome"] != "muerte")
        death_times = [ep["time_to_terminal_ms"] for ep in fatigue_episodes if ep["outcome"] == "muerte"]
        return {
            "fatigue_events": len(fatigue_episodes),
            "spotted_total": spotted_total,
            "spotted_while_fatigued": spotted_fatigued,
            "spotted_while_fatigued_pct": pct(spotted_fatigued, spotted_total),
            "spotted_by_state": dict(spotted_by_state),
            "deaths_while_fatigued": death_fatigued,
            "survival_after_fatigue_pct": pct(survival_count, len(fatigue_episodes)),
            "avg_time_to_death_after_fatigue_ms": safe_mean(death_times),
            "episodes": fatigue_episodes[:200],
            "episodes_truncated": max(0, len(fatigue_episodes) - 200),
        }

    def item_metrics(self) -> Dict[str, Any]:
        picked_by_type = Counter()
        used_by_type = Counter()
        used_with_enemy_near = 0
        used_after_spotted = 0
        time_from_spotted_to_use: List[int] = []
        used_while_fatigued = 0
        direct_context_on_use = 0
        inferred_context_on_use = 0
        inventory_not_used_at_death_total = 0
        inventory_not_used_at_death_by_type = Counter()
        death_inventory_snapshots: List[Dict[str, Any]] = []
        total_uses = 0

        for session_id, session_events in self.by_session.items():
            inventory = Counter()
            for i, event in enumerate(session_events):
                typ = get_event_type(event)
                if typ == "ItemPicked":
                    item = get_item_type(event)
                    picked_by_type[item] += 1
                    inventory[item] += 1
                elif typ == "ItemUsed":
                    item = get_item_type(event)
                    used_by_type[item] += 1
                    total_uses += 1
                    if inventory[item] > 0:
                        inventory[item] -= 1

                    if event.get("cercaEnemigo") is not None or event.get("estadoJugador") is not None:
                        direct_context_on_use += 1
                    else:
                        inferred_context_on_use += 1

                    enemy_near = False
                    if event.get("cercaEnemigo") is not None:
                        enemy_near = as_bool(event.get("cercaEnemigo"), False)
                    else:
                        state_event = self.nearest_previous_state(session_events, i)
                        enemy_near = bool(state_event and as_bool(state_event.get("cercaEnemigo"), False))
                    if enemy_near:
                        used_with_enemy_near += 1

                    state = get_state(event)
                    if state == "Desconocido":
                        state_event = self.nearest_previous_state(session_events, i)
                        state = get_state(state_event) if state_event else "Desconocido"
                    if state == "Fatigado":
                        used_while_fatigued += 1

                    spotted = self.last_event_before(session_events, i, "PlayerSpotted", self.after_spotted_window_ms)
                    if spotted is not None:
                        used_after_spotted += 1
                        time_from_spotted_to_use.append(get_timestamp(event) - get_timestamp(spotted))
                elif typ == "PlayerDeath":
                    unused = {k: v for k, v in inventory.items() if v > 0}
                    total_unused = sum(unused.values())
                    if total_unused:
                        inventory_not_used_at_death_total += total_unused
                        inventory_not_used_at_death_by_type.update(unused)
                    death_inventory_snapshots.append({
                        "session": session_id,
                        "level": get_level(event),
                        "timestamp": get_timestamp(event),
                        "unused_inventory": unused,
                        "total_unused": total_unused,
                    })

        all_items = sorted(set(picked_by_type) | set(used_by_type) | set(inventory_not_used_at_death_by_type))
        by_type = []
        for item in all_items:
            picked = picked_by_type[item]
            used = used_by_type[item]
            by_type.append({
                "item": item,
                "picked": picked,
                "used": used,
                "use_rate_pct": pct(used, picked),
                "unused_at_death": inventory_not_used_at_death_by_type[item],
            })
        by_type.sort(key=lambda r: (-r["used"], r["item"]))
        return {
            "picked_total": sum(picked_by_type.values()),
            "used_total": sum(used_by_type.values()),
            "picked_by_type": dict(picked_by_type),
            "used_by_type": dict(used_by_type),
            "by_type": by_type,
            "used_with_enemy_near": used_with_enemy_near,
            "used_with_enemy_near_pct": pct(used_with_enemy_near, total_uses),
            "used_after_spotted": used_after_spotted,
            "used_after_spotted_pct": pct(used_after_spotted, total_uses),
            "median_ms_from_spotted_to_use": safe_median(time_from_spotted_to_use),
            "avg_ms_from_spotted_to_use": safe_mean(time_from_spotted_to_use),
            "used_while_fatigued": used_while_fatigued,
            "used_while_fatigued_pct": pct(used_while_fatigued, total_uses),
            "inventory_not_used_at_death_total": inventory_not_used_at_death_total,
            "inventory_not_used_at_death_by_type": dict(inventory_not_used_at_death_by_type),
            "death_inventory_snapshots": death_inventory_snapshots[:200],
            "direct_context_on_item_use": direct_context_on_use,
            "inferred_context_on_item_use": inferred_context_on_use,
            "note": "ItemUsed no incluye posición, estado ni cercanía con el evento C# actual; esas métricas se infieren desde el PlayerState anterior si existe.",
        }

    def choose_heatmap_axes(self, points: List[Dict[str, Any]]) -> Tuple[str, str]:
        """Elige plano del mapa. En juegos 3D suele ser X/Z; en 2D Unity suele dejar Z=0 y usar X/Y."""
        if not points:
            return "x", "z"

        def axis_range(axis: str) -> float:
            values = [as_float(point.get(axis)) for point in points]
            values = [value for value in values if value is not None and math.isfinite(value)]
            if len(values) < 2:
                return 0.0
            return max(values) - min(values)

        y_range = axis_range("y")
        z_range = axis_range("z")
        # Si Z casi no varía pero Y sí, el mapa está en 2D X/Y. Esto evita que los puntos
        # se dibujen como una línea horizontal en medio del canvas.
        if z_range <= 0.001 and y_range > 0.001:
            return "x", "y"
        return "x", "z"

    def add_projected_heatmap_coordinates(self, points: List[Dict[str, Any]], x_axis: str, y_axis: str) -> None:
        for point in points:
            point["map_x"] = point.get(x_axis, 0.0)
            point["map_y"] = point.get(y_axis, 0.0)
            point["map_x_axis"] = x_axis
            point["map_y_axis"] = y_axis

    def make_heatmap_point(
        self,
        event: Dict[str, Any],
        pos: Dict[str, float],
        point_type: str,
        route_segment: Optional[str] = None,
        route_inferred: bool = False,
    ) -> Dict[str, Any]:
        point = {
            "level": get_level(event),
            "raw_level": get_raw_level(event),
            "session": get_session_id(event),
            "timestamp": get_timestamp(event),
            "x": pos["x"],
            "y": pos["y"],
            "z": pos["z"],
            "event_type": get_event_type(event),
        }
        if point_type == "route":
            point["route_segment"] = route_segment or f"{get_session_id(event)}:0"
            point["route_inferred"] = route_inferred
            point["route_source"] = get_event_type(event)
        return point

    def heatmap_metrics(self) -> Dict[str, Any]:
        points: Dict[str, List[Dict[str, Any]]] = {"route": [], "death": [], "spotted": [], "hideout": [], "item_use": []}
        has_explicit_player_state = any(get_event_type(event) == "PlayerState" for event in self.events)

        # Recorrido: si existe PlayerState real, se usa solo ese evento. Si no existe,
        # se reconstruye de forma aproximada con eventos que guardan estado/posición.
        for session_id, session_events in self.by_session.items():
            segment = 0
            for event in session_events:
                typ = get_event_type(event)
                if typ == "LevelStart":
                    segment += 1

                if is_route_candidate(event, has_explicit_player_state):
                    pos = get_route_position(event)
                    if pos is not None:
                        points["route"].append(self.make_heatmap_point(
                            event,
                            pos,
                            "route",
                            route_segment=f"{session_id}:{segment}",
                            route_inferred=(typ != "PlayerState"),
                        ))

                if typ in {"PlayerDeath", "LevelComplete", "SessionEnd"}:
                    segment += 1

        for event in self.events:
            typ = get_event_type(event)
            if typ == "PlayerDeath":
                pos = get_position(event)
                if pos:
                    points["death"].append(self.make_heatmap_point(event, pos, "death"))
            elif typ == "PlayerSpotted":
                pos = get_position(event, ("posicionJugador", "playerPosition", "posicion", "position"))
                if pos:
                    points["spotted"].append(self.make_heatmap_point(event, pos, "spotted"))
            elif typ == "PlayerHidden":
                pos = get_position(event)
                if pos:
                    points["hideout"].append(self.make_heatmap_point(event, pos, "hideout"))
            elif typ == "ItemUsed":
                pos = get_position(event)
                if pos:
                    points["item_use"].append(self.make_heatmap_point(event, pos, "item_use"))

        all_points = [p for plist in points.values() for p in plist]
        x_axis, y_axis = self.choose_heatmap_axes(all_points)
        self.add_projected_heatmap_coordinates(all_points, x_axis, y_axis)

        bounds = pad_bounds(self.compute_bounds(all_points, x_axis=x_axis, y_axis=y_axis))
        bounds_by_level = {}
        for level in sorted({p["level"] for p in all_points}, key=level_sort_key):
            level_bounds = self.compute_bounds([p for p in all_points if p["level"] == level], x_axis=x_axis, y_axis=y_axis)
            level_bounds = pad_bounds(level_bounds)
            level_bounds = apply_level_bounds_override(level, level_bounds)
            bounds_by_level[level] = level_bounds
        grids = {name: self.build_grid(plist, bounds) for name, plist in points.items()}
        route_sources = Counter(point.get("route_source", "Unknown") for point in points["route"])
        return {
            "points": {name: plist[:5000] for name, plist in points.items()},
            "points_truncated": {name: max(0, len(plist) - 5000) for name, plist in points.items()},
            "bounds": bounds,
            "bounds_by_level": bounds_by_level,
            "level_world_bounds_overrides": LEVEL_WORLD_BOUNDS_OVERRIDES,
            "map_axes": {"x": x_axis, "y": y_axis},
            "route_source": "PlayerState" if has_explicit_player_state else "inferred_from_state_position_events",
            "route_source_counts": dict(route_sources),
            "grid_size": self.grid_size,
            "grids": grids,
        }

    def compute_bounds(
        self,
        points: List[Dict[str, Any]],
        x_axis: str = "x",
        y_axis: str = "z",
    ) -> Dict[str, Optional[float]]:
        if not points:
            return {"min_x": None, "max_x": None, "min_z": None, "max_z": None, "x_axis": x_axis, "y_axis": y_axis}
        xs = [as_float(p.get("map_x", p.get(x_axis))) for p in points]
        ys = [as_float(p.get("map_y", p.get(y_axis))) for p in points]
        xs = [v for v in xs if v is not None and math.isfinite(v)]
        ys = [v for v in ys if v is not None and math.isfinite(v)]
        if not xs or not ys:
            return {"min_x": None, "max_x": None, "min_z": None, "max_z": None, "x_axis": x_axis, "y_axis": y_axis}
        min_x, max_x = min(xs), max(xs)
        min_z, max_z = min(ys), max(ys)
        if math.isclose(min_x, max_x):
            min_x -= 1
            max_x += 1
        if math.isclose(min_z, max_z):
            min_z -= 1
            max_z += 1
        return {"min_x": min_x, "max_x": max_x, "min_z": min_z, "max_z": max_z, "x_axis": x_axis, "y_axis": y_axis}

    def build_grid(self, points: List[Dict[str, Any]], bounds: Dict[str, Optional[float]]) -> Dict[str, Any]:
        if not points or bounds["min_x"] is None:
            return {"by_level": {}, "all": []}
        min_x = float(bounds["min_x"])
        max_x = float(bounds["max_x"])
        min_z = float(bounds["min_z"])
        max_z = float(bounds["max_z"])
        width_x = max_x - min_x
        width_z = max_z - min_z

        def cell_for(point: Dict[str, Any]) -> Tuple[int, int]:
            px = as_float(point.get("map_x", point.get("x")), min_x) or min_x
            py = as_float(point.get("map_y", point.get("z")), min_z) or min_z
            gx = int(((px - min_x) / width_x) * self.grid_size)
            gz = int(((py - min_z) / width_z) * self.grid_size)
            return max(0, min(self.grid_size - 1, gx)), max(0, min(self.grid_size - 1, gz))

        counters: Dict[str, Counter] = defaultdict(Counter)
        all_counter: Counter = Counter()
        for point in points:
            cell = cell_for(point)
            counters[str(point["level"])][cell] += 1
            all_counter[cell] += 1

        def serialize(counter: Counter) -> List[Dict[str, int]]:
            return [{"x": x, "z": z, "count": count} for (x, z), count in sorted(counter.items())]

        return {
            "by_level": {level: serialize(counter) for level, counter in counters.items()},
            "all": serialize(all_counter),
        }

    def session_metrics(self) -> Dict[str, Any]:
        output: Dict[str, Any] = {}
        for session_id, events in self.by_session.items():
            timestamps = [get_timestamp(e) for e in events if get_timestamp(e) > 0]
            type_counts = Counter(get_event_type(e) for e in events)
            session_end = next((e for e in reversed(events) if get_event_type(e) == "SessionEnd"), None)
            explicit_duration_ms = seconds_to_ms(session_end.get("duracionSesion")) if session_end else None
            output[session_id] = {
                "total_events": len(events),
                "event_type_counts": dict(type_counts),
                "levels": sorted({get_level(e) for e in events}, key=level_sort_key),
                "duration_ms": explicit_duration_ms or ((max(timestamps) - min(timestamps)) if len(timestamps) >= 2 else None),
                "deaths": type_counts.get("PlayerDeath", 0),
                "spotted": type_counts.get("PlayerSpotted", 0),
                "heartbeat_attempts": type_counts.get("HeartbeatAttempt", 0),
                "fatigue": type_counts.get("FatigueTriggered", 0),
                "item_uses": type_counts.get("ItemUsed", 0),
                "hideout_entries": sum(1 for e in events if get_event_type(e) == "PlayerHidden" and as_bool(e.get("entrando"), True)),
            }
        return dict(sorted(output.items()))

    def data_quality(self, metrics: Dict[str, Any]) -> Dict[str, Any]:
        counts = metrics["summary"]["event_type_counts"]
        inferred_level_completions = self.infer_level_completions()
        inferred_level_complete_count = sum(int(v.get("count", 0)) for v in inferred_level_completions.values())
        estado_jugador_count = self.state_payload_count()
        route_source = metrics.get("heatmaps", {}).get("route_source")

        required = [
            "SessionStart", "SessionEnd", "LevelStart", "LevelComplete", "PlayerState",
            "PlayerDeath", "PlayerSpotted", "HeartbeatAttempt", "FatigueTriggered",
            "PlayerHidden", "ItemPicked", "ItemUsed",
        ]
        effective_counts = dict(counts)
        if inferred_level_complete_count > 0:
            effective_counts["LevelComplete"] = effective_counts.get("LevelComplete", 0) + inferred_level_complete_count
        if estado_jugador_count > 0:
            effective_counts["PlayerState"] = effective_counts.get("PlayerState", 0) + estado_jugador_count

        missing_events = [name for name in required if effective_counts.get(name, 0) == 0]
        warnings = []
        if counts.get("LevelFail", 0) == 0:
            warnings.append("No hay eventos LevelFail en los logs cargados. Si un intento termina mal pero no hay PlayerDeath, ese fallo de nivel no quedará representado.")
        if counts.get("LevelComplete", 0) == 0 and inferred_level_complete_count > 0:
            warnings.append("LevelComplete se ha inferido a partir de transiciones LevelStart hacia un nivel posterior; es válido para progreso, pero no sustituye al tiempo exacto de finalización.")
        elif counts.get("LevelComplete", 0) == 0:
            warnings.append("No hay LevelComplete explícito ni inferible en este lote.")
        if metrics["hideouts"].get("inferred_enemy_near_from_player_state", 0) > 0:
            warnings.append("Parte de la cercanía al enemigo en PlayerHidden se ha inferido desde el PlayerState anterior; es mejor guardar cercaEnemigo directamente en PlayerHidden.")
        if metrics["items"].get("inferred_context_on_item_use", 0) > 0:
            warnings.append("ItemUsed no trae estado/posición/cercaEnemigo en algunos registros; el análisis táctico de objetos depende parcialmente de inferencias temporales.")
        if counts.get("HeartbeatAttempt", 0) and metrics["heartbeat"].get("avg_green_zone") is None:
            warnings.append("Hay HeartbeatAttempt, pero no se ha encontrado tamañoZonaVerde/tamanoZonaVerde.")

        inferred_metrics = [
            "LevelComplete desde LevelStart de un nivel posterior" if inferred_level_complete_count else None,
            "PlayerState efectivo desde campos estadoJugador" if estado_jugador_count and counts.get("PlayerState", 0) == 0 else None,
            "Recorrido aproximado desde eventos con posición" if route_source == "inferred_from_state_position_events" else None,
            "PlayerHidden.cercaEnemigo desde PlayerState anterior" if metrics["hideouts"].get("inferred_enemy_near_from_player_state", 0) > 0 else None,
            "PlayerDeath cerca de armario no usado desde PlayerState anterior",
            "ItemUsed cerca de enemigo / fatigado desde PlayerState anterior" if metrics["items"].get("inferred_context_on_item_use", 0) > 0 else None,
            "Objetos no usados al morir desde balance ItemPicked - ItemUsed por sesión",
        ]
        inferred_metrics = [m for m in inferred_metrics if m]
        suggestions = []
        score = max(0, round(100 * (1 - len(missing_events) / len(required))))
        return {
            "required_events": required,
            "missing_events": missing_events,
            "coverage_score_pct": score,
            "warnings": warnings,
            "inferred_metrics": inferred_metrics,
            "inferred_events": {
                "LevelComplete": inferred_level_complete_count,
                "PlayerState_from_estadoJugador": estado_jugador_count,
            },
            "effective_event_counts": dict(sorted(effective_counts.items())),
            "suggestions": suggestions,
        }

    def hypothesis_results(self, metrics: Dict[str, Any]) -> Dict[str, Any]:
        h1 = self.evaluate_h1(metrics["heartbeat"])
        h2 = self.evaluate_h2(metrics["hideouts"])
        h3 = self.evaluate_h3(metrics["fatigue"])
        h4 = self.evaluate_h4(metrics["items"])
        return {"h1_heartbeat_pressure": h1, "h2_hideouts": h2, "h3_fatigue": h3, "h4_items": h4}

    def evaluate_h1(self, heartbeat: Dict[str, Any]) -> Dict[str, Any]:
        attempts = heartbeat.get("total_attempts", 0)
        delta = heartbeat.get("pressure_delta_fail_rate_pct")
        if attempts < 10 or delta is None:
            status = "pendiente"
            conclusion = "Faltan intentos de latido o rangos suficientes de zona verde para validar la hipótesis."
        elif delta >= 10:
            status = "validada"
            conclusion = "La tasa de fallo es claramente mayor con zona verde pequeña, coherente con mayor presión."
        elif delta >= 3:
            status = "parcial"
            conclusion = "La tendencia existe, pero la diferencia todavía es moderada."
        else:
            status = "no_validada"
            conclusion = "No aparece una relación clara entre zona verde pequeña y más fallos."
        return {
            "title": "Pulso bajo presión",
            "status": status,
            "main_metric": delta,
            "main_metric_label": "Δ fallo zona pequeña vs grande (p.p.)",
            "conclusion": conclusion,
        }

    def evaluate_h2(self, hideouts: Dict[str, Any]) -> Dict[str, Any]:
        entries = hideouts.get("total_entries", 0)
        near_pct = hideouts.get("entries_with_enemy_near_pct")
        spotted_pct = hideouts.get("entries_after_spotted_pct")
        avg_inside = hideouts.get("avg_time_inside_ms")
        if entries < 3:
            status = "pendiente"
            conclusion = "Faltan entradas a armarios/cajas para evaluar uso táctico."
        elif (near_pct or 0) >= 25 or (spotted_pct or 0) >= 20:
            status = "validada"
            conclusion = "Una proporción relevante de usos ocurre con amenaza cercana o poco después de ser detectado."
        elif avg_inside and avg_inside > 1000:
            status = "parcial"
            conclusion = "Hay uso real de escondites, pero no queda suficientemente ligado a la amenaza."
        else:
            status = "no_validada"
            conclusion = "Los escondites no parecen usarse como reacción clara a la amenaza con los datos actuales."
        return {
            "title": "Uso táctico de armarios",
            "status": status,
            "main_metric": near_pct,
            "secondary_metric": spotted_pct,
            "main_metric_label": "% usos con enemigo cerca",
            "conclusion": conclusion,
        }

    def evaluate_h3(self, fatigue: Dict[str, Any]) -> Dict[str, Any]:
        fatigue_events = fatigue.get("fatigue_events", 0)
        spotted_pct = fatigue.get("spotted_while_fatigued_pct")
        survival_pct = fatigue.get("survival_after_fatigue_pct")
        if fatigue_events < 3:
            status = "pendiente"
            conclusion = "Faltan episodios de fatiga para medir su efecto con fiabilidad."
        elif (survival_pct is not None and survival_pct < 50) or (spotted_pct is not None and spotted_pct >= 25):
            status = "validada"
            conclusion = "La fatiga se asocia a detección o baja supervivencia posterior."
        elif (spotted_pct or 0) > 0:
            status = "parcial"
            conclusion = "Hay detecciones durante fatiga, pero el efecto todavía no es fuerte."
        else:
            status = "no_validada"
            conclusion = "No se observa un impacto negativo claro de la fatiga."
        return {
            "title": "Fatiga como castigo",
            "status": status,
            "main_metric": spotted_pct,
            "secondary_metric": survival_pct,
            "main_metric_label": "% PlayerSpotted en fatiga",
            "conclusion": conclusion,
        }

    def evaluate_h4(self, items: Dict[str, Any]) -> Dict[str, Any]:
        uses = items.get("used_total", 0)
        near_pct = items.get("used_with_enemy_near_pct")
        after_spotted_pct = items.get("used_after_spotted_pct")
        fatigue_pct = items.get("used_while_fatigued_pct")
        if uses < 3:
            status = "pendiente"
            conclusion = "Faltan usos de objetos para evaluar patrón táctico."
        elif (near_pct or 0) >= 25 or (after_spotted_pct or 0) >= 20 or (fatigue_pct or 0) >= 20:
            status = "validada"
            conclusion = "Los objetos se usan de forma frecuente en amenaza, tras detección o durante fatiga."
        elif (near_pct or 0) > 0 or (after_spotted_pct or 0) > 0 or (fatigue_pct or 0) > 0:
            status = "parcial"
            conclusion = "Hay usos tácticos, pero no dominan el comportamiento."
        else:
            status = "no_validada"
            conclusion = "Los usos de objetos no aparecen ligados a amenaza o fatiga con los datos actuales."
        return {
            "title": "Uso táctico de objetos",
            "status": status,
            "main_metric": near_pct,
            "secondary_metric": after_spotted_pct,
            "main_metric_label": "% usos con enemigo cerca",
            "conclusion": conclusion,
        }

    def analyze(self) -> Dict[str, Any]:
        metrics: Dict[str, Any] = {
            "schema_version": "heartbeat_telemetry_report_v1",
            "summary": self.base_summary(),
            "levels": self.level_metrics(),
            "sessions": self.session_metrics(),
            "inferred_level_completions": self.infer_level_completions(),
            "heartbeat": self.heartbeat_metrics(),
            "hideouts": self.hideout_metrics(),
            "fatigue": self.fatigue_metrics(),
            "items": self.item_metrics(),
            "heatmaps": self.heatmap_metrics(),
            "parameters": {
                "near_window_ms": self.near_window_ms,
                "after_spotted_window_ms": self.after_spotted_window_ms,
                "grid_size": self.grid_size,
            },
        }
        metrics["data_quality"] = self.data_quality(metrics)
        metrics["hypotheses"] = self.hypothesis_results(metrics)
        return metrics


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Analiza logs de telemetría Heartbeat y genera un JSON para el dashboard.")
    parser.add_argument("paths", nargs="+", help="Archivos o carpetas con .jsonl, .json, .csv, .log o .txt")
    parser.add_argument("--json-out", default="resumen_telemetria.json", help="Ruta del JSON de salida")
    parser.add_argument("--near-window-ms", type=int, default=1500, help="Ventana para inferir contexto desde PlayerState anterior")
    parser.add_argument("--after-spotted-window-ms", type=int, default=5000, help="Ventana para considerar respuesta tras PlayerSpotted")
    parser.add_argument("--grid-size", type=int, default=18, help="Resolución de grids de calor")
    parser.add_argument("--pretty", action="store_true", help="Imprime también un resumen por consola")
    return parser


def print_pretty(report: Dict[str, Any]) -> None:
    summary = report["summary"]
    print("\n=== Resumen de telemetría Heartbeat ===")
    print(f"Eventos: {summary['total_events']}")
    print(f"Sesiones: {summary['sessions_count']}")
    print(f"Niveles: {', '.join(map(str, summary['levels'])) or 'N/A'}")
    print("\nHipótesis:")
    for key, hyp in report["hypotheses"].items():
        print(f"- {hyp['title']}: {hyp['status']} · {hyp['conclusion']}")
    warnings = report["data_quality"].get("warnings", [])
    if warnings:
        print("\nAvisos de calidad:")
        for warning in warnings:
            print(f"- {warning}")


def main(argv: Optional[List[str]] = None) -> int:
    parser = build_argument_parser()
    args = parser.parse_args(argv)

    input_paths = [Path(p) for p in args.paths]
    files = collect_files(input_paths)
    if not files:
        print("[ERROR] No se encontraron archivos de telemetría.", file=sys.stderr)
        return 2

    events: List[Dict[str, Any]] = []
    for file in files:
        try:
            file_events = read_events_from_file(file)
        except Exception as exc:  # noqa: BLE001 - herramienta CLI: no queremos romper todo por un archivo
            print(f"[WARN] No se pudo leer {file}: {exc}", file=sys.stderr)
            continue
        for event in file_events:
            event.setdefault("_source_file", str(file))
        events.extend(file_events)

    if not events:
        print("[ERROR] No se pudo extraer ningún evento válido.", file=sys.stderr)
        return 3

    analyzer = TelemetryAnalyzer(
        events,
        source_files=[str(f) for f in files],
        near_window_ms=args.near_window_ms,
        after_spotted_window_ms=args.after_spotted_window_ms,
        grid_size=args.grid_size,
    )
    report = analyzer.analyze()

    out_path = Path(args.json_out)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"OK: escrito {out_path} ({len(events)} eventos, {len(files)} archivo(s)).")
    if args.pretty:
        print_pretty(report)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
