"""Automate local translation screenshot verification for Caves of Qud."""

import argparse
import ctypes
import functools
import json
import plistlib
import re
import shutil
import subprocess
import sys
import tempfile
import time
from collections.abc import Callable
from ctypes import util
from dataclasses import dataclass
from datetime import UTC, datetime
from pathlib import Path

if __package__ in {None, ""}:
    _PROJECT_ROOT = Path(__file__).resolve().parents[1]
    _PROJECT_ROOT_STR = str(_PROJECT_ROOT)
    if _PROJECT_ROOT_STR not in sys.path:
        sys.path.insert(0, _PROJECT_ROOT_STR)

from scripts.triage.classifier import classify
from scripts.triage.log_parser import parse_log_text
from scripts.triage.models import LogEntry, LogEntryKind, TriageClassification, TriageResult

_PLAYER_LOG = Path.home() / "Library" / "Logs" / "Freehold Games" / "CavesOfQud" / "Player.log"
_BUILD_MARKER = "[QudJP] Build marker:"
_TITLE_READY_PROBE = "MainMenuLocalizationPatch"
_LOAD_READY_PROBE = "[QudJP] QudMenuBottomContextProbe/RefreshButtonsAfter/v1:"
_NON_ACTIONABLE_CLASSIFICATIONS = frozenset(
    {
        TriageClassification.PRESERVED_ENGLISH,
        TriageClassification.RUNTIME_NOISE,
    },
)
_LOAD_READY_FAILURE_PATTERNS: tuple[str, ...] = (
    "ChargenStructuredTextTranslator",
    "Choose Game Mode",
    "ERROR - Booting game",
    "XRL.Core.XRLCore.NewGame",
)
_COMBAT_EVIDENCE_PATTERNS: tuple[str, ...] = (
    "Do you really want to attack",
)
_COMBAT_EVIDENCE_REGEXES: tuple[re.Pattern[str], ...] = (
    re.compile(r"source='(?:\{\{[^']*\|)?You (?:critically )?(?:hit|crit|miss)\b"),
    re.compile(r"source='(?:The )?.+? (?:hits|crits) you .* damage"),
    re.compile(r"source='.+? takes \d+ damage from your "),
    re.compile(r"source='.+?(?:can't|cannot|fails? to|failed to|doesn't|don't) penetrate .+? armor"),
    re.compile(r"(?:source|translated)='.+?装甲を貫けない"),
    re.compile(r"family='\^You (?:critically )?(?:hit|crit|miss)\b"),
    re.compile(r"family='\^\(\?:The \)\?\(\.\+\) (?:hits|crits) you for"),
)
_DEFAULT_INVENTORY_PATTERNS: tuple[str, ...] = (
    "[QudJP] DescriptionInventoryActionProbe:",
    "[QudJP] InventoryLineReplacement/v1:",
    "[QudJP] InventoryLineReplacementStateNextFrame/v1:",
    "[QudJP] EquipmentLineProbe/v1:",
)
_MESSAGE_LOG_ROUTES = frozenset({"MessageLog", "MessageLogPatch", "EmitMessage"})
_ASCII_WORD_PATTERN = re.compile(r"[A-Za-z]{2,}")
_MARKUP_TOKEN_PATTERN = re.compile(
    r"\{\{[^|}]+\||\}\}|&&|\^\^|&[A-Za-z]|\^[A-Za-z]|<color=[^>]+>|</color>|=[A-Za-z0-9_.]+=",
)
_FINAL_OUTPUT_OBSERVATION_FIELDS = (
    "sink",
    "detail",
    "phase",
    "translation_status",
    "markup_status",
    "direct_marker_status",
    "source_text_sample",
    "stripped_text_sample",
    "translated_text_sample",
    "final_text_sample",
    "source_markup_spans",
    "final_markup_spans",
    "markup_span_status",
    "markup_semantic_status",
    "markup_semantic_flags",
    "source_visible_sha256",
    "final_visible_sha256",
    "payload_mode",
    "payload_excerpt",
    "payload_sha256",
)
_NON_ISSUE_MARKUP_SPAN_STATUSES = frozenset({None, "matched", "no_markup"})
_DEFAULT_INVENTORY_TAB_PAGE_RIGHTS = 8
_DEFAULT_INVENTORY_ITEM_SCAN_COUNT = 8
_DEFAULT_INVENTORY_ITEM_ACTION_ROW_OFFSET = 1
_DEFAULT_INVENTORY_ITEM_PANE_CHORD = "right"
_DEFAULT_ABILITIES_CHORD = "a"
_DEFAULT_ACTIVE_EFFECTS_CHORD = "x,e"
_DEFAULT_DEATH_ATTACK_COUNT = 30
_DEFAULT_DEATH_CONFIRM_KEY = "space"
_DEATH_EVIDENCE_PATTERNS: tuple[str, ...] = (
    "You died",
    "You are dead",
    "You were killed",
    "You have died",
)
_DEATH_EVIDENCE_REGEXES: tuple[re.Pattern[str], ...] = (
    re.compile(r"(?:source|translated)='[^']*(?:あなた|君|プレイヤー).*(?:死|倒れ)"),
)
_SPECIAL_KEY_CODES = {
    "backspace": 51,
    "backslash": 42,
    "delete": 51,
    "down": 125,
    "end": 119,
    "enter": 76,
    "escape": 53,
    "home": 115,
    "iso_section": 10,
    "jis_yen": 93,
    "left": 123,
    "oem102": 42,
    "pagedown": 121,
    "pageup": 116,
    "return": 36,
    "right": 124,
    "space": 49,
    "tab": 48,
    "up": 126,
    "numpad0": 82,
    "numpad1": 83,
    "numpad2": 84,
    "numpad3": 85,
    "numpad4": 86,
    "numpad5": 87,
    "numpad6": 88,
    "numpad7": 89,
    "numpad8": 91,
    "numpad9": 92,
    "numpadminus": 78,
    "numpadplus": 69,
}
_CHARACTER_KEY_CODES = {
    " ": 49,
    "a": 0,
    "b": 11,
    "c": 8,
    "d": 2,
    "e": 14,
    "f": 3,
    "g": 5,
    "h": 4,
    "i": 34,
    "j": 38,
    "k": 40,
    "l": 37,
    "m": 46,
    "n": 45,
    "o": 31,
    "p": 35,
    "q": 12,
    "r": 15,
    "s": 1,
    "t": 17,
    "u": 32,
    "v": 9,
    "w": 13,
    "x": 7,
    "y": 16,
    "z": 6,
}
_KEY_CHORD_MODIFIER_ALIASES = {
    "cmd": "command",
    "command": "command",
    "ctrl": "control",
    "control": "control",
    "option": "option",
    "shift": "shift",
}
_MODIFIER_FLAGS = {
    "command": 0x00100000,
    "control": 0x00040000,
    "option": 0x00080000,
    "shift": 0x00020000,
}
_OSASCRIPT_MODIFIER_NAMES = {
    "command": "command",
    "control": "control",
    "option": "option",
    "shift": "shift",
}
_MOUSE_EVENT_LEFT_DOWN = 1
_MOUSE_EVENT_LEFT_UP = 2
_MOUSE_EVENT_MOVE = 5
_HAMMERSPOON_APP_CANDIDATES = (
    Path("/Applications/Hammerspoon.app"),
    Path.home() / "Applications" / "Hammerspoon.app",
    Path("/Applications/Setapp/Hammerspoon.app"),
)
_COQ_BUNDLE_IDENTIFIER = "com.FreeholdGames.CavesOfQud"


class _CGPoint(ctypes.Structure):
    _fields_ = [("x", ctypes.c_double), ("y", ctypes.c_double)]


def _default_screenshot_path() -> Path:
    timestamp = datetime.now(UTC).strftime("%Y%m%dT%H%M%SZ")
    return Path(tempfile.gettempdir()) / f"qudjp-inventory-{timestamp}.png"


def _default_flow_screenshot_dir() -> Path:
    timestamp = datetime.now(UTC).strftime("%Y%m%dT%H%M%SZ")
    return Path(tempfile.gettempdir()) / f"qudjp-l3-final-smoke-{timestamp}"


def _default_save_root() -> Path:
    return Path.home() / "Library" / "Application Support" / "com.FreeholdGames.CavesOfQud" / "Synced" / "Saves"


def _find_matching_patterns(text: str, patterns: tuple[str, ...]) -> list[str]:
    return [pattern for pattern in patterns if pattern in text]


def _read_log_delta(path: Path, offset: int) -> str:
    with path.open("rb") as handle:
        size = path.stat().st_size
        handle.seek(0 if offset > size else offset)
        return handle.read().decode("utf-8", errors="replace")


def _read_log_range(path: Path, start_offset: int, end_offset: int) -> str:
    if not path.exists():
        return ""
    size = path.stat().st_size
    start = 0 if start_offset > size else max(start_offset, 0)
    end = min(max(end_offset, start), size)
    with path.open("rb") as handle:
        handle.seek(start)
        return handle.read(end - start).decode("utf-8", errors="replace")


def _current_log_offset(path: Path) -> int:
    if not path.exists():
        return 0
    return path.stat().st_size


def _wait_for_log_matches(
    path: Path,
    offset: int,
    timeout: float,
    matcher: Callable[[str], list[str]],
) -> tuple[list[str], str]:
    deadline = time.monotonic() + timeout
    latest = ""
    while time.monotonic() < deadline:
        latest = _read_log_delta(path, offset)
        matches = matcher(latest)
        if matches:
            return matches, latest
        time.sleep(1.0)
    return [], latest


def _io_console_locked_from_root(root: dict[str, object]) -> bool:
    value = root.get("IOConsoleLocked")
    return bool(value)


def _wait_for_patterns(path: Path, offset: int, patterns: tuple[str, ...], timeout: float) -> tuple[list[str], str]:
    return _wait_for_log_matches(path, offset, timeout, lambda text: _find_matching_patterns(text, patterns))


def _title_ready_matches(text: str) -> list[str]:
    if _TITLE_READY_PROBE in text:
        return [_TITLE_READY_PROBE]
    return []


def _wait_for_title_ready(path: Path, offset: int, timeout: float) -> tuple[list[str], str]:
    return _wait_for_log_matches(path, offset, timeout, _title_ready_matches)


def _load_ready_matches(text: str) -> list[str]:
    matches: list[str] = []
    if _LOAD_READY_PROBE in text:
        matches.append(_LOAD_READY_PROBE)

    for line in text.splitlines():
        if "[QudJP] Translator: missing key" not in line:
            continue
        if "MainMenuLocalizationPatch" in line:
            continue
        matches.append("[QudJP] Translator:<non-main-menu>")
        break

    return matches


def _load_ready_failure_matches(text: str) -> list[str]:
    return _find_matching_patterns(text, _LOAD_READY_FAILURE_PATTERNS)


def _combat_evidence_matches(text: str) -> list[str]:
    matches = _find_matching_patterns(text, _COMBAT_EVIDENCE_PATTERNS)
    for pattern in _COMBAT_EVIDENCE_REGEXES:
        if pattern.search(text):
            matches.append(pattern.pattern)
    return matches


def _death_evidence_matches(text: str) -> list[str]:
    matches = _find_matching_patterns(text, _DEATH_EVIDENCE_PATTERNS)
    for pattern in _DEATH_EVIDENCE_REGEXES:
        if pattern.search(text):
            matches.append(pattern.pattern)
    return matches


def _wait_for_load_ready(path: Path, offset: int, timeout: float) -> tuple[list[str], str]:
    deadline = time.monotonic() + timeout
    latest = ""
    while time.monotonic() < deadline:
        latest = _read_log_delta(path, offset)
        failure_matches = _load_ready_failure_matches(latest)
        if failure_matches:
            _raise_load_ready_failed(failure_matches)
        matches = _load_ready_matches(latest)
        if matches:
            return matches, latest
        time.sleep(1.0)
    return [], latest


def _raise_load_ready_failed(matches: list[str]) -> None:
    msg = (
        "Observed a New Game / chargen path while waiting for a loaded-save signal before final-smoke: "
        + ", ".join(matches)
    )
    raise RuntimeError(msg)


def _key_code_for(key: str) -> int:
    normalized_key = key.lower()
    if normalized_key in _SPECIAL_KEY_CODES:
        return _SPECIAL_KEY_CODES[normalized_key]

    if len(normalized_key) == 1 and normalized_key in _CHARACTER_KEY_CODES:
        return _CHARACTER_KEY_CODES[normalized_key]

    msg = f"Unsupported key: {key}"
    raise ValueError(msg)


def _parse_key_chord(chord: str) -> tuple[str, tuple[str, ...]]:
    parts = [part.strip().lower() for part in chord.split("+") if part.strip()]
    if not parts:
        msg = "Key chord must not be empty."
        raise ValueError(msg)

    raw_modifiers = parts[:-1]
    key = parts[-1]
    modifiers: list[str] = []
    for raw_modifier in raw_modifiers:
        if raw_modifier not in _KEY_CHORD_MODIFIER_ALIASES:
            msg = f"Unsupported key chord modifier: {raw_modifier}"
            raise ValueError(msg)
        modifiers.append(_KEY_CHORD_MODIFIER_ALIASES[raw_modifier])

    normalized_modifiers = tuple(modifiers)
    _key_code_for(key)
    _modifier_mask(normalized_modifiers)
    return key, normalized_modifiers


def _parse_key_sequence(sequence: str) -> tuple[str, ...]:
    chords = tuple(chord.strip() for chord in sequence.split(",") if chord.strip())
    if not chords:
        msg = "Key sequence must contain at least one key chord."
        raise ValueError(msg)

    for chord in chords:
        _parse_key_chord(chord)
    return chords


def _attack_sequences(args: argparse.Namespace) -> tuple[tuple[str, ...], ...]:
    raw_sequences = args.attack_sequence
    if not raw_sequences:
        return (_parse_key_sequence(args.attack_chord),)
    return tuple(_parse_key_sequence(sequence) for sequence in raw_sequences)


def _modifier_mask(modifiers: tuple[str, ...]) -> int:
    invalid_modifiers = [modifier for modifier in modifiers if modifier not in _MODIFIER_FLAGS]
    if invalid_modifiers:
        msg = f"Unsupported modifier(s): {', '.join(invalid_modifiers)}"
        raise ValueError(msg)

    mask = 0
    for modifier in modifiers:
        mask |= _MODIFIER_FLAGS[modifier]
    return mask


@functools.lru_cache(maxsize=1)
def _application_services() -> ctypes.CDLL:
    path = util.find_library("ApplicationServices")
    if path is None:
        msg = "ApplicationServices framework not found."
        raise RuntimeError(msg)

    library = ctypes.cdll.LoadLibrary(path)
    library.CGEventCreateKeyboardEvent.restype = ctypes.c_void_p
    library.CGEventCreateKeyboardEvent.argtypes = [ctypes.c_void_p, ctypes.c_uint16, ctypes.c_bool]
    library.CGEventCreateMouseEvent.restype = ctypes.c_void_p
    library.CGEventCreateMouseEvent.argtypes = [ctypes.c_void_p, ctypes.c_uint32, _CGPoint, ctypes.c_uint32]
    library.CGEventSetFlags.argtypes = [ctypes.c_void_p, ctypes.c_uint64]
    library.CGEventPost.argtypes = [ctypes.c_uint32, ctypes.c_void_p]
    library.CFRelease.argtypes = [ctypes.c_void_p]
    return library


def _build_focus_script(pid: int) -> str:
    return (
        'tell application "System Events"\n'
        f"  set frontmost of first application process whose unix id is {pid} to true\n"
        "end tell"
    )


def _build_activate_application_script(bundle_identifier: str) -> str:
    return f'tell application id "{_escape_osascript_string(bundle_identifier)}" to activate'


def _build_system_events_key_code_script(key: str, modifiers: tuple[str, ...] = ()) -> str:
    key_code = _key_code_for(key)
    invalid_modifiers = [modifier for modifier in modifiers if modifier not in _OSASCRIPT_MODIFIER_NAMES]
    if invalid_modifiers:
        msg = f"Unsupported modifier(s): {', '.join(invalid_modifiers)}"
        raise ValueError(msg)

    script = f'tell application "System Events" to key code {key_code}'
    if not modifiers:
        return script

    modifier_terms = [f"{_OSASCRIPT_MODIFIER_NAMES[modifier]} down" for modifier in modifiers]
    if len(modifier_terms) == 1:
        return f"{script} using {modifier_terms[0]}"
    return f"{script} using {{{', '.join(modifier_terms)}}}"


def _escape_osascript_string(text: str) -> str:
    return text.replace("\\", "\\\\").replace('"', '\\"')


def _build_hammerspoon_focus_lua(pid: int, output_path: Path) -> str:
    escaped_output = output_path.as_posix().replace("\\", "\\\\").replace('"', '\\"')
    return f"""local outPath = \"{escaped_output}\"
local lines = {{}}
local function add(...)
  local parts = {{}}
  for i = 1, select("#", ...) do
    parts[#parts + 1] = tostring(select(i, ...))
  end
  lines[#lines + 1] = table.concat(parts, " ")
end
local ok, err = pcall(function()
  local wins = hs.window.allWindows()
  local target = nil
  for _, win in ipairs(wins) do
    local app = win:application()
    if app and app:pid() == {pid} then
      target = win
      break
    end
  end
  add("found=", tostring(target ~= nil))
  if target then
    local focused = target:focus()
    add("focus_result=", tostring(focused))
    hs.timer.usleep(700000)
    local front = hs.application.frontmostApplication()
    local focusedWindow = hs.window.focusedWindow()
    add("front=", front and front:name() or "nil")
    add("focused_title=", focusedWindow and (focusedWindow:title() or "") or "nil")
  end
end)
if not ok then
  add("error=", err)
end
local file = assert(io.open(outPath, "w"))
file:write(table.concat(lines, "\\n"))
file:close()
"""


def _parse_hammerspoon_focus_output(text: str) -> dict[str, str]:
    result: dict[str, str] = {}
    for line in text.splitlines():
        if "=" not in line:
            continue
        key, value = line.split("=", 1)
        result[key.strip()] = value.strip()
    return result


def _project_root() -> Path:
    return Path(__file__).resolve().parent.parent


def _ensure_supported_environment() -> None:
    if sys.platform != "darwin":
        msg = "translation_checker.py is supported on macOS only."
        raise RuntimeError(msg)

    missing_tools = [tool for tool in ("osascript", "screencapture") if shutil.which(tool) is None]
    if missing_tools:
        msg = f"Required tool(s) not found in PATH: {', '.join(missing_tools)}"
        raise RuntimeError(msg)

    if not _PLAYER_LOG.parent.is_dir():
        msg = f"Player.log directory not found: {_PLAYER_LOG.parent}"
        raise FileNotFoundError(msg)


def _ensure_unlocked_console() -> None:
    result = subprocess.run(["/usr/sbin/ioreg", "-a", "-n", "Root"], capture_output=True, check=True)
    root = plistlib.loads(result.stdout)
    if _io_console_locked_from_root(root):
        msg = "macOS console session is locked. Unlock the Mac before running translation_checker.py."
        raise RuntimeError(msg)


def _locate_hammerspoon_app() -> Path | None:
    for path in _HAMMERSPOON_APP_CANDIDATES:
        if path.exists():
            return path
    return None


def _run_subprocess(command: list[str]) -> subprocess.CompletedProcess[str]:
    return subprocess.run(command, capture_output=True, text=True, check=True)  # noqa: S603 -- trusted repo-local or system command


def _run_osascript(script: str) -> subprocess.CompletedProcess[str]:
    return _run_subprocess(["osascript", "-e", script])


def _sync_mod(*, skip_sync: bool) -> None:
    if skip_sync:
        return

    script = _project_root() / "scripts" / "sync_mod.py"
    _run_subprocess([sys.executable, str(script)])


def _build_launch_game_command() -> list[str]:
    script = _project_root() / "scripts" / "launch_rosetta.sh"
    return [str(script)]


def _launch_game() -> subprocess.Popen[bytes]:
    return subprocess.Popen(  # noqa: S603 -- trusted repo-local launcher script
        _build_launch_game_command(),
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )


def _send_key(pid: int, key: str, modifiers: tuple[str, ...] = ()) -> None:
    library = _application_services()
    key_code = _key_code_for(key)
    modifier_mask = _modifier_mask(modifiers)

    _stabilize_game_focus(pid)
    time.sleep(0.2)
    for is_key_down in (True, False):
        event = library.CGEventCreateKeyboardEvent(None, key_code, is_key_down)
        if not event:
            msg = f"Failed to create keyboard event for key: {key}"
            raise RuntimeError(msg)
        if modifier_mask:
            library.CGEventSetFlags(event, modifier_mask)
        library.CGEventPost(0, event)
        library.CFRelease(event)
        time.sleep(0.05)


def _send_key_osascript(_pid: int, key: str, modifiers: tuple[str, ...] = ()) -> None:
    _activate_game_application()
    time.sleep(0.2)
    _run_osascript(_build_system_events_key_code_script(key, modifiers))


def _send_key_with_backend(pid: int, key: str, modifiers: tuple[str, ...], input_backend: str) -> None:
    if input_backend == "cgevent":
        _send_key(pid, key, modifiers)
        return
    if input_backend == "osascript":
        _send_key_osascript(pid, key, modifiers)
        return
    msg = f"Unsupported input backend: {input_backend}"
    raise ValueError(msg)


def _send_key_chord(pid: int, chord: str, input_backend: str = "cgevent") -> None:
    key, modifiers = _parse_key_chord(chord)
    _send_key_with_backend(pid, key, modifiers, input_backend)


def _focus_process(pid: int) -> None:
    script = _build_focus_script(pid)
    _run_osascript(script)


def _activate_game_application() -> None:
    _run_osascript(_build_activate_application_script(_COQ_BUNDLE_IDENTIFIER))


def _open_hammerspoon_console() -> None:
    app_path = _locate_hammerspoon_app()
    if app_path is None:
        msg = "Hammerspoon.app is not installed."
        raise RuntimeError(msg)

    _run_subprocess(["open", str(app_path)])
    time.sleep(0.5)
    _run_osascript(
        'tell application "System Events" to tell process "Hammerspoon" '
        'to click menu item "Console..." of menu 1 of menu bar item "File" of menu bar 1'
    )
    time.sleep(0.5)


def _close_hammerspoon_console() -> None:
    try:
        _run_osascript(
            'tell application "System Events" to tell process "Hammerspoon" '
            'to click menu item "Close" of menu 1 of menu bar item "Window" of menu bar 1'
        )
    except subprocess.CalledProcessError:
        return


def _run_hammerspoon_lua(lua_source: str, output_path: Path, timeout: float = 5.0) -> str:
    _open_hammerspoon_console()
    with tempfile.NamedTemporaryFile(
        "w", suffix=".lua", prefix="qudjp-hs-", delete=False, encoding="utf-8"
    ) as lua_file:
        lua_file.write(lua_source)
        lua_path = Path(lua_file.name)

    output_path.unlink(missing_ok=True)
    command = f'dofile("{_escape_osascript_string(lua_path.as_posix())}")'

    try:
        set_value_script = (
            'tell application "System Events" to tell process "Hammerspoon" '
            'to tell window "Hammerspoon Console" '
            f'to set value of text field 1 to "{_escape_osascript_string(command)}"'
        )
        _run_osascript(set_value_script)
        _run_osascript(
            'tell application "System Events" to tell process "Hammerspoon" '
            'to tell window "Hammerspoon Console" to click text field 1'
        )
        time.sleep(0.3)
        _run_osascript('tell application "System Events" to key code 36')

        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            if output_path.exists():
                return output_path.read_text(encoding="utf-8")
            time.sleep(0.1)

        msg = f"Timed out waiting for Hammerspoon output at {output_path}."
        raise RuntimeError(msg)
    finally:
        lua_path.unlink(missing_ok=True)
        output_path.unlink(missing_ok=True)
        _close_hammerspoon_console()


def _focus_process_with_hammerspoon(pid: int) -> None:
    output_path = Path(tempfile.mkstemp(prefix="qudjp-hs-focus-", suffix=".txt")[1])
    output_path.unlink(missing_ok=True)
    lua_source = _build_hammerspoon_focus_lua(pid, output_path)
    try:
        result = _run_hammerspoon_lua(lua_source, output_path)
        parsed = _parse_hammerspoon_focus_output(result)
        if parsed.get("found") != "true":
            msg = f"Hammerspoon could not find CoQ window for pid {pid}. Output: {result}"
            raise RuntimeError(msg)
        if parsed.get("front") not in {"CoQ", "CavesOfQud"}:
            msg = f"Hammerspoon did not bring CoQ frontmost. Output: {result}"
            raise RuntimeError(msg)
    finally:
        output_path.unlink(missing_ok=True)


def _stabilize_game_focus(pid: int) -> None:
    try:
        _activate_game_application()
    except subprocess.CalledProcessError:
        pass
    else:
        return

    if _locate_hammerspoon_app() is not None:
        try:
            _focus_process_with_hammerspoon(pid)
        except (RuntimeError, subprocess.CalledProcessError):
            pass
        else:
            return

    _focus_process(pid)


def _click_point(pid: int, x: int, y: int) -> None:
    library = _application_services()
    point = _CGPoint(float(x), float(y))

    _stabilize_game_focus(pid)
    time.sleep(0.2)
    for event_type in (_MOUSE_EVENT_MOVE, _MOUSE_EVENT_LEFT_DOWN, _MOUSE_EVENT_LEFT_UP):
        event = library.CGEventCreateMouseEvent(None, event_type, point, 0)
        if not event:
            msg = f"Failed to create mouse event at ({x}, {y})."
            raise RuntimeError(msg)
        library.CGEventPost(0, event)
        library.CFRelease(event)
        time.sleep(0.05)


def _capture_screenshot(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    _run_subprocess(["screencapture", "-x", str(path)])


def _slugify_stage_name(label: str) -> str:
    slug = re.sub(r"[^a-z0-9]+", "-", label.lower()).strip("-")
    return slug or "stage"


def _flow_screenshot_path(output_dir: Path, index: int, label: str) -> Path:
    return output_dir / f"{index:02d}-{_slugify_stage_name(label)}.png"


def _capture_flow_screenshot(output_dir: Path, index: int, label: str) -> Path:
    path = _flow_screenshot_path(output_dir, index, label)
    _capture_screenshot(path)
    return path


def _build_final_smoke_inventory_drilldown_stage_names(
    *,
    tab_page_rights: int = _DEFAULT_INVENTORY_TAB_PAGE_RIGHTS,
    item_scan_count: int = _DEFAULT_INVENTORY_ITEM_SCAN_COUNT,
) -> list[str]:
    return [
        "inventory-tab-00-initial",
        "inventory-display-options",
        "inventory-item-00-selected",
        "inventory-item-00-actions",
        *[f"inventory-item-scan-{index:02d}" for index in range(1, item_scan_count + 1)],
        *[f"inventory-tab-page-right-{index:02d}" for index in range(1, tab_page_rights + 1)],
    ]


def _build_final_smoke_popup_stages() -> tuple[tuple[str, str, str], ...]:
    return (
        ("popup-system-menu", "escape", "escape"),
        ("popup-point-of-interest", "backspace", "escape"),
        ("popup-look-direction", "shift+l", "escape"),
        ("popup-help", "p", "p"),
        ("popup-fire-no-ranged-weapon", "shift+f", "space"),
    )


def _build_final_smoke_character_screen_stages(
    args: argparse.Namespace,
) -> tuple[tuple[str, tuple[str, ...], tuple[str, ...]], ...]:
    return (
        ("abilities-screen", _parse_key_sequence(args.abilities_chord), ("escape",)),
        ("active-effects-screen", _parse_key_sequence(args.active_effects_chord), ("escape", "escape")),
    )


def _build_final_smoke_stage_names(
    *,
    tab_page_rights: int = _DEFAULT_INVENTORY_TAB_PAGE_RIGHTS,
    item_scan_count: int = _DEFAULT_INVENTORY_ITEM_SCAN_COUNT,
) -> list[str]:
    return [
        "after-load",
        *_build_final_smoke_inventory_drilldown_stage_names(
            tab_page_rights=tab_page_rights,
            item_scan_count=item_scan_count,
        ),
        "abilities-screen",
        "active-effects-screen",
        *[label for label, _, _ in _build_final_smoke_popup_stages()],
        "npc-conversation",
        "attack-or-confirmation",
        "message-log-after-attack",
        "death-or-combat-end",
        "message-log-after-death",
    ]


def _flow_requires_load_ready(flow: str) -> bool:
    return flow in {"final-smoke", "combat-smoke"}


def _should_send_title_load_inputs(*, manual_load: bool) -> bool:
    return not manual_load


def _title_navigation_delay(title_ready_matches: list[str], fallback_wait: float, post_ready_wait: float) -> float:
    if title_ready_matches:
        return post_ready_wait
    return fallback_wait


def _load_save_metadata(save_dir: Path) -> dict[str, object] | None:
    metadata_path = save_dir / "Primary.json"
    if not metadata_path.is_file():
        return None
    with metadata_path.open(encoding="utf-8") as handle:
        data = json.load(handle)
    if isinstance(data, dict):
        return data
    return None


def _save_metadata_is_qudjp(metadata: dict[str, object]) -> bool:
    mods = metadata.get("ModsEnabled")
    return isinstance(mods, list) and "QudJP" in mods


def _discover_latest_qudjp_save_dir(save_root: Path) -> Path | None:
    if not save_root.is_dir():
        return None

    candidates: list[Path] = []
    for child in save_root.iterdir():
        if not child.is_dir():
            continue
        metadata = _load_save_metadata(child)
        if metadata is not None and _save_metadata_is_qudjp(metadata):
            candidates.append(child)

    if not candidates:
        return None

    return max(candidates, key=lambda path: (path / "Primary.json").stat().st_mtime)


def _backup_save_dir(save_dir: Path, backup_dir: Path) -> None:
    if backup_dir.exists():
        msg = f"Save backup directory already exists: {backup_dir}"
        raise FileExistsError(msg)
    shutil.copytree(save_dir, backup_dir)


def _restore_save_dir(save_dir: Path, backup_dir: Path) -> None:
    save_dir.mkdir(parents=True, exist_ok=True)
    for source_path in backup_dir.iterdir():
        destination_path = save_dir / source_path.name
        if source_path.is_dir():
            shutil.copytree(source_path, destination_path, dirs_exist_ok=True)
        else:
            shutil.copy2(source_path, destination_path)


def _start_caffeinate() -> subprocess.Popen[bytes] | None:
    caffeinate_path = shutil.which("caffeinate")
    if caffeinate_path is None:
        return None
    return subprocess.Popen(  # noqa: S603 -- trusted macOS system command resolved with shutil.which
        [caffeinate_path, "-dimsu"],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )


def _stop_process(process: subprocess.Popen[bytes] | None, timeout: float = 3.0) -> None:
    if process is None or process.poll() is not None:
        return
    process.terminate()
    try:
        process.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=timeout)


def _wait_for_exit(process: subprocess.Popen[bytes], timeout: float) -> bool:
    try:
        process.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        return False
    return True


def _stop_game(
    process: subprocess.Popen[bytes],
    quit_confirm_key: str,
    quit_timeout: float,
    input_backend: str,
) -> None:
    if process.poll() is not None:
        return

    try:
        _send_key_with_backend(process.pid, "q", ("command",), input_backend)
    except subprocess.CalledProcessError:
        return
    time.sleep(1.0)
    if process.poll() is not None:
        return

    if quit_confirm_key:
        try:
            _send_key_with_backend(process.pid, quit_confirm_key, (), input_backend)
        except subprocess.CalledProcessError:
            return
        time.sleep(1.0)

    if _wait_for_exit(process, quit_timeout):
        return

    process.terminate()
    if _wait_for_exit(process, 5.0):
        return

    process.kill()
    process.wait(timeout=5.0)


def _parse_args(argv: list[str] | None = None) -> argparse.Namespace:  # noqa: PLR0915 -- CLI options stay together.
    parser = argparse.ArgumentParser(
        description=(
            "Deploy QudJP, launch Caves of Qud via Rosetta, and capture translation verification screenshots."
        ),
    )
    parser.add_argument(
        "--skip-sync",
        action="store_true",
        help="Skip `python scripts/sync_mod.py` before launching the game.",
    )
    parser.add_argument(
        "--screenshot-path",
        type=Path,
        default=_default_screenshot_path(),
        help="Write the screenshot to this path instead of a temp-file path.",
    )
    parser.add_argument(
        "--flow",
        choices=("inventory", "final-smoke", "combat-smoke"),
        default="inventory",
        help="Run inventory, the broader L3 final-smoke scenario, or the focused combat-smoke scenario.",
    )
    parser.add_argument(
        "--manual-load",
        action="store_true",
        help=(
            "Launch through Rosetta and wait for the user or Computer Use to load a save. "
            "Skips scripted title-menu input."
        ),
    )
    parser.add_argument(
        "--flow-screenshot-dir",
        type=Path,
        default=_default_flow_screenshot_dir(),
        help="Directory for final-smoke screenshots.",
    )
    parser.add_argument(
        "--flow-step-wait",
        type=float,
        default=1.0,
        help="Seconds to wait after each final-smoke key action.",
    )
    parser.add_argument(
        "--flow-screenshot-wait",
        type=float,
        default=3.0,
        help="Seconds to wait immediately before each flow screenshot so TMP text can settle into translations.",
    )
    parser.add_argument(
        "--input-backend",
        choices=("cgevent", "osascript"),
        default="cgevent",
        help=(
            "Keyboard input backend. Use osascript when macOS/Unity drops Control-modified Computer Use "
            "or CGEvent chords."
        ),
    )
    parser.add_argument(
        "--poi-travel-wait",
        type=float,
        default=4.0,
        help="Seconds to wait after selecting a point of interest in final-smoke.",
    )
    parser.add_argument(
        "--save-dir",
        type=Path,
        help="Optional Caves of Qud save directory to back up before final-smoke.",
    )
    parser.add_argument(
        "--save-backup-dir",
        type=Path,
        help="Optional backup directory for final-smoke save restoration.",
    )
    parser.add_argument(
        "--restore-save",
        action=argparse.BooleanOptionalAction,
        default=True,
        help="Restore the backed-up save after final-smoke exits.",
    )
    parser.add_argument(
        "--npc-poi-key",
        default="d",
        help=(
            "Point-of-interest picker key used to move near the NPC before conversation. "
            "Empty string skips POI travel."
        ),
    )
    parser.add_argument(
        "--npc-talk-key",
        default="c",
        help="Key chord used to start talk direction selection in final-smoke.",
    )
    parser.add_argument(
        "--npc-talk-direction",
        default="right",
        help="Direction key chord used to select the NPC after talk starts.",
    )
    parser.add_argument(
        "--attack-chord",
        default="backslash,right",
        help=(
            "Default combat/log attempt sequence when --attack-sequence is not provided. "
            "Comma-separate multiple key chords for force-attack direction input."
        ),
    )
    parser.add_argument(
        "--attack-sequence",
        action="append",
        help=(
            "Comma-separated key chords used for one combat/log attempt. "
            "Can be repeated to try fallback force-attack routes in one run, "
            "for example: --attack-sequence ctrl+numpad6 --attack-sequence backslash,right."
        ),
    )
    parser.add_argument(
        "--attack-confirm-key",
        default="",
        help="Key to confirm an attack warning popup. Empty string captures the warning without confirming.",
    )
    parser.add_argument(
        "--message-log-chord",
        default="ctrl+m",
        help="Key chord used to open message history after attacking. Empty string captures the visible sidebar only.",
    )
    parser.add_argument(
        "--death-attack-count",
        type=int,
        default=_DEFAULT_DEATH_ATTACK_COUNT,
        help=(
            "Maximum repeated attack count after the first attack. "
            "Use 0 to skip death/combat-end message-log evidence."
        ),
    )
    parser.add_argument(
        "--death-confirm-key",
        default=_DEFAULT_DEATH_CONFIRM_KEY,
        help=(
            "Key sent after each repeated death attack to dismiss combat warning popups, "
            "such as low-health warnings. Empty string skips the extra confirmation."
        ),
    )
    parser.add_argument(
        "--require-combat-evidence",
        action="store_true",
        help=(
            "Fail final-smoke when attack inputs produce no attack prompt, hit, miss, or damage evidence "
            "in Player.log."
        ),
    )
    parser.add_argument(
        "--launch-timeout",
        type=float,
        default=90.0,
        help="Seconds to wait for the QudJP build marker after launch.",
    )
    parser.add_argument(
        "--title-ready-wait",
        type=float,
        default=12.0,
        help="Fallback seconds to wait before title-screen input if no title-ready log signal appears.",
    )
    parser.add_argument(
        "--title-ready-post-wait",
        type=float,
        default=1.0,
        help="Seconds to wait after a title-ready log signal before title-screen input.",
    )
    parser.add_argument(
        "--title-ready-timeout",
        type=float,
        default=30.0,
        help="Seconds to wait for a title-ready log signal before falling back to title-ready-wait.",
    )
    parser.add_argument(
        "--menu-up-presses",
        type=int,
        default=0,
        help="How many times to press Up before selecting from the title menu.",
    )
    parser.add_argument(
        "--menu-down-presses",
        type=int,
        default=0,
        help="How many times to press Down before selecting Continue from the title menu.",
    )
    parser.add_argument(
        "--menu-navigation-interval",
        type=float,
        default=0.2,
        help="Seconds to wait between title-menu navigation key presses.",
    )
    parser.add_argument(
        "--menu-up-key",
        default="up",
        help="Key used for title-menu upward navigation (default: up).",
    )
    parser.add_argument(
        "--menu-down-key",
        default="down",
        help="Key used for title-menu downward navigation (default: down).",
    )
    parser.add_argument(
        "--continue-key",
        default="c",
        help="Key to press at the main menu to select Continue (default: c, the in-game Continue shortcut).",
    )
    parser.add_argument(
        "--continue-presses",
        type=int,
        default=3,
        help="How many times to press the continue key at the main menu to tolerate dropped title-screen input.",
    )
    parser.add_argument(
        "--continue-interval",
        type=float,
        default=1.0,
        help="Seconds to wait between continue key presses.",
    )
    parser.add_argument(
        "--continue-click-x",
        type=int,
        help="Optional screen x coordinate to click instead of using the continue key.",
    )
    parser.add_argument(
        "--continue-click-y",
        type=int,
        help="Optional screen y coordinate to click instead of using the continue key.",
    )
    parser.add_argument(
        "--save-select-key",
        default="space",
        help="Key to select the first save after Continue opens the save picker.",
    )
    parser.add_argument(
        "--save-select-presses",
        type=int,
        default=1,
        help="How many times to press the save-select key after opening Continue.",
    )
    parser.add_argument(
        "--load-wait",
        type=float,
        default=12.0,
        help="Fallback seconds to wait before inventory if no load-ready log signal appears.",
    )
    parser.add_argument(
        "--load-ready-timeout",
        type=float,
        default=45.0,
        help="Seconds to wait for a post-Continue world-ready log signal before falling back to load-wait.",
    )
    parser.add_argument(
        "--inventory-key",
        default="i",
        help="Key to press to open inventory (default: i). Use an empty string to skip sending it.",
    )
    parser.add_argument(
        "--inventory-timeout",
        type=float,
        default=12.0,
        help="Seconds to wait for inventory-related probe lines after opening inventory.",
    )
    parser.add_argument(
        "--inventory-tab-page-rights",
        type=int,
        default=_DEFAULT_INVENTORY_TAB_PAGE_RIGHTS,
        help="How many Page Right steps to capture after opening inventory in final-smoke.",
    )
    parser.add_argument(
        "--inventory-item-scan-count",
        type=int,
        default=_DEFAULT_INVENTORY_ITEM_SCAN_COUNT,
        help="How many selected inventory rows to capture after opening the first item action menu.",
    )
    parser.add_argument(
        "--inventory-item-action-row-offset",
        type=int,
        default=_DEFAULT_INVENTORY_ITEM_ACTION_ROW_OFFSET,
        help="How many Down presses to reach the first inventory item before opening its action menu.",
    )
    parser.add_argument(
        "--inventory-item-pane-chord",
        default=_DEFAULT_INVENTORY_ITEM_PANE_CHORD,
        help="Key chord used to move focus to the inventory item list before item drilldown.",
    )
    parser.add_argument(
        "--abilities-chord",
        default=_DEFAULT_ABILITIES_CHORD,
        help="Key chord used to open the Manage Abilities screen during final-smoke.",
    )
    parser.add_argument(
        "--active-effects-chord",
        default=_DEFAULT_ACTIVE_EFFECTS_CHORD,
        help=(
            "Comma-separated key chord sequence used to open active effects during final-smoke. "
            "Defaults to opening attributes, then the status effects action."
        ),
    )
    parser.add_argument(
        "--inventory-pattern",
        action="append",
        help=(
            "Additional Player.log pattern to treat as evidence that inventory opened. "
            "Can be repeated. Defaults to DescriptionInventoryActionProbe and InventoryLineReplacement/v1."
        ),
    )
    parser.add_argument(
        "--quit-confirm-key",
        default="y",
        help="Key to confirm the quit prompt after Cmd+Q (default: y). Use an empty string to skip.",
    )
    parser.add_argument(
        "--quit-timeout",
        type=float,
        default=15.0,
        help="Seconds to wait for a graceful shutdown before terminate/kill.",
    )
    return parser.parse_args(argv)


def _build_result(
    screenshot_path: Path,
    load_ready_matches: list[str],
    inventory_matches: list[str],
    log_delta: str,
) -> dict[str, object]:
    excerpt_lines = log_delta.splitlines()[-40:]
    return {
        "build_marker_found": True,
        "load_ready_found": bool(load_ready_matches),
        "load_ready_matches": load_ready_matches,
        "inventory_probe_found": bool(inventory_matches),
        "inventory_probe_matches": inventory_matches,
        "player_log_path": str(_PLAYER_LOG),
        "screenshot_path": str(screenshot_path),
        "log_excerpt": excerpt_lines,
    }


def _raise_build_marker_timeout(timeout: float) -> None:
    msg = (
        f"Timed out after {timeout:.1f}s waiting for '{_BUILD_MARKER}' in {_PLAYER_LOG}. "
        "Check Player.log and verify the mod bootstrapped under Rosetta."
    )
    raise RuntimeError(msg)


def _raise_missing_continue_click_coordinate() -> None:
    msg = "Both --continue-click-x and --continue-click-y are required together."
    raise ValueError(msg)


def _raise_load_ready_required_timeout(timeout: float) -> None:
    msg = (
        f"Timed out after {timeout:.1f}s waiting for a loaded-save signal before final-smoke. "
        "The scenario was stopped before screenshots were captured to avoid false evidence. "
        "Verify the game was launched through scripts/launch_rosetta.sh, then adjust title/load input options."
    )
    raise RuntimeError(msg)


def _perform_title_navigation(process: subprocess.Popen[bytes], args: argparse.Namespace, title_offset: int) -> None:
    title_ready_matches, _ = _wait_for_title_ready(_PLAYER_LOG, title_offset, args.title_ready_timeout)
    time.sleep(_title_navigation_delay(title_ready_matches, args.title_ready_wait, args.title_ready_post_wait))

    for _ in range(args.menu_up_presses):
        _send_key_chord(process.pid, args.menu_up_key, args.input_backend)
        time.sleep(args.menu_navigation_interval)

    for _ in range(args.menu_down_presses):
        _send_key_chord(process.pid, args.menu_down_key, args.input_backend)
        time.sleep(args.menu_navigation_interval)


def _continue_from_title(process: subprocess.Popen[bytes], args: argparse.Namespace) -> None:
    if args.continue_click_x is not None or args.continue_click_y is not None:
        if args.continue_click_x is None or args.continue_click_y is None:
            _raise_missing_continue_click_coordinate()
        _click_point(process.pid, args.continue_click_x, args.continue_click_y)
    else:
        for index in range(args.continue_presses):
            _send_key_chord(process.pid, args.continue_key, args.input_backend)
            if index + 1 < args.continue_presses:
                time.sleep(args.continue_interval)

    for _ in range(args.save_select_presses):
        time.sleep(args.continue_interval)
        _send_key_chord(process.pid, args.save_select_key, args.input_backend)


@dataclass
class _StageCapture:
    label: str
    screenshot_path: str
    log_start_offset: int
    log_end_offset: int


@dataclass
class _FlowCapture:
    pid: int
    output_dir: Path
    screenshot_paths: list[str]
    stage_captures: list[_StageCapture]
    wait: float
    screenshot_wait: float
    input_backend: str
    index: int = 1

    def send(self, chord: str, wait: float | None = None) -> None:
        if not chord:
            return
        _send_key_chord(self.pid, chord, self.input_backend)
        time.sleep(self.wait if wait is None else wait)

    def send_sequence(self, sequence: tuple[str, ...]) -> None:
        for chord in sequence:
            self.send(chord)

    def screenshot(self, label: str, log_start_offset: int | None = None) -> None:
        start_offset = _current_log_offset(_PLAYER_LOG) if log_start_offset is None else log_start_offset
        if self.screenshot_wait > 0:
            time.sleep(self.screenshot_wait)
        _stabilize_game_focus(self.pid)
        path = _capture_flow_screenshot(self.output_dir, self.index, label)
        end_offset = _current_log_offset(_PLAYER_LOG)
        self.screenshot_paths.append(str(path))
        self.stage_captures.append(
            _StageCapture(
                label=label,
                screenshot_path=str(path),
                log_start_offset=start_offset,
                log_end_offset=end_offset,
            ),
        )
        self.index += 1

    def close_popup(self) -> None:
        _send_key_with_backend(self.pid, "escape", (), self.input_backend)
        time.sleep(self.wait)

    def inventory_stage(self, label: str, chord: str) -> None:
        stage_start_offset = _current_log_offset(_PLAYER_LOG)
        self.send(chord)
        self.screenshot(label, stage_start_offset)
        self.close_popup()

    def popup_stage(self, label: str, chord: str, close_chord: str) -> None:
        stage_start_offset = _current_log_offset(_PLAYER_LOG)
        self.send(chord)
        self.screenshot(label, stage_start_offset)
        self.send(close_chord)

    def screen_stage(self, label: str, open_sequence: tuple[str, ...], close_sequence: tuple[str, ...]) -> None:
        stage_start_offset = _current_log_offset(_PLAYER_LOG)
        self.send_sequence(open_sequence)
        self.screenshot(label, stage_start_offset)
        self.send_sequence(close_sequence)


def _run_inventory_drilldown(capture: _FlowCapture, args: argparse.Namespace) -> None:
    stage_start_offset = _current_log_offset(_PLAYER_LOG)
    capture.send(args.inventory_key)
    capture.screenshot("inventory-tab-00-initial", stage_start_offset)

    _capture_inventory_display_options(capture)
    _capture_inventory_first_item_actions(
        capture,
        args.inventory_item_action_row_offset,
        args.inventory_item_pane_chord,
    )
    _capture_inventory_item_scan(capture, args.inventory_item_scan_count)
    _capture_inventory_tabs_by_page_right(capture, args.inventory_tab_page_rights)

    capture.send("escape")


def _capture_inventory_display_options(capture: _FlowCapture) -> None:
    stage_start_offset = _current_log_offset(_PLAYER_LOG)
    capture.send("tab")
    capture.screenshot("inventory-display-options", stage_start_offset)
    capture.send("escape")


def _capture_inventory_first_item_actions(capture: _FlowCapture, row_offset: int, item_pane_chord: str) -> None:
    stage_start_offset = _current_log_offset(_PLAYER_LOG)
    capture.send(item_pane_chord)
    for _ in range(row_offset):
        capture.send("down")
    capture.screenshot("inventory-item-00-selected", stage_start_offset)

    action_stage_start_offset = _current_log_offset(_PLAYER_LOG)
    capture.send("enter")
    capture.screenshot("inventory-item-00-actions", action_stage_start_offset)
    capture.send("escape")


def _capture_inventory_item_scan(capture: _FlowCapture, item_scan_count: int) -> None:
    for index in range(1, item_scan_count + 1):
        stage_start_offset = _current_log_offset(_PLAYER_LOG)
        capture.send("down")
        capture.screenshot(f"inventory-item-scan-{index:02d}", stage_start_offset)


def _capture_inventory_tabs_by_page_right(capture: _FlowCapture, tab_page_rights: int) -> None:
    for index in range(1, tab_page_rights + 1):
        stage_start_offset = _current_log_offset(_PLAYER_LOG)
        capture.send("end")
        capture.screenshot(f"inventory-tab-page-right-{index:02d}", stage_start_offset)


def _build_verification_report(stage_captures: list[_StageCapture], log_path: Path) -> dict[str, object]:
    stages = [_build_stage_verification(stage, log_path) for stage in stage_captures]
    return {
        "generated_at": datetime.now(UTC).isoformat(),
        "player_log_path": str(log_path),
        "summary": _build_verification_summary(stages),
        "stages": stages,
    }


def _write_verification_report(report: dict[str, object], output_dir: Path) -> Path:
    report_path = output_dir / "verification_report.json"
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    return report_path


def _build_stage_verification(stage: _StageCapture, log_path: Path) -> dict[str, object]:
    log_text = _read_log_range(log_path, stage.log_start_offset, stage.log_end_offset)
    entries = parse_log_text(log_text)
    results = [classify(entry) for entry in entries]
    markup_issues = _markup_issue_candidates(entries)
    summary = _build_stage_summary(results)
    summary["markup_color_tag_issue_candidates"] = len(markup_issues)
    return {
        "stage": stage.label,
        "screenshot_path": stage.screenshot_path,
        "log_start_offset": stage.log_start_offset,
        "log_end_offset": stage.log_end_offset,
        "summary": summary,
        "translated_events": [_serialize_result(result) for result in _translated_results(results)],
        "missing_key_candidates": [_serialize_result(result) for result in _missing_key_candidates(results)],
        "message_pattern_gaps": [_serialize_result(result) for result in _message_pattern_gaps(results)],
        "sink_observed_untranslated_candidates": [
            _serialize_result(result) for result in _sink_observed_untranslated_candidates(results)
        ],
        "final_output_observations": [_serialize_result(result) for result in _final_output_observations(results)],
        "message_log_candidates": [_serialize_result(result) for result in _message_log_candidates(results)],
        "markup_color_tag_issue_candidates": markup_issues,
        "preserved_english": [_serialize_result(result) for result in _preserved_english_results(results)],
    }


def _build_stage_summary(results: list[TriageResult]) -> dict[str, object]:
    counts = {
        "translated_events": len(_translated_results(results)),
        "missing_key_candidates": len(_missing_key_candidates(results)),
        "message_pattern_gaps": len(_message_pattern_gaps(results)),
        "sink_observed_untranslated_candidates": len(_sink_observed_untranslated_candidates(results)),
        "final_output_observations": len(_final_output_observations(results)),
        "message_log_candidates": len(_message_log_candidates(results)),
        "preserved_english": len(_preserved_english_results(results)),
    }
    return {
        **counts,
        "by_route": _count_results_by_route(results),
    }


def _build_verification_summary(stages: list[dict[str, object]]) -> dict[str, object]:
    totals: dict[str, int] = {
        "stage_count": len(stages),
        "translated_events": 0,
        "missing_key_candidates": 0,
        "message_pattern_gaps": 0,
        "sink_observed_untranslated_candidates": 0,
        "final_output_observations": 0,
        "message_log_candidates": 0,
        "markup_color_tag_issue_candidates": 0,
        "preserved_english": 0,
    }
    by_stage: dict[str, dict[str, int]] = {}
    by_route: dict[str, int] = {}
    for stage in stages:
        stage_name = str(stage["stage"])
        stage_summary = stage["summary"]
        if not isinstance(stage_summary, dict):
            continue
        by_stage[stage_name] = _int_counts(stage_summary)
        for key in totals:
            if key == "stage_count":
                continue
            totals[key] += int(stage_summary.get(key, 0))
        stage_routes = stage_summary.get("by_route", {})
        if isinstance(stage_routes, dict):
            for route, count in stage_routes.items():
                by_route[str(route)] = by_route.get(str(route), 0) + int(count)

    return {**totals, "by_stage": by_stage, "by_route": dict(sorted(by_route.items()))}


def _translated_results(results: list[TriageResult]) -> list[TriageResult]:
    return [
        result
        for result in results
        if result.entry.kind == LogEntryKind.DYNAMIC_TEXT_PROBE and result.entry.changed is True
    ]


def _missing_key_candidates(results: list[TriageResult]) -> list[TriageResult]:
    return [
        result
        for result in results
        if result.entry.kind == LogEntryKind.MISSING_KEY
        and result.classification not in _NON_ACTIONABLE_CLASSIFICATIONS
    ]


def _message_pattern_gaps(results: list[TriageResult]) -> list[TriageResult]:
    return [result for result in results if result.entry.kind == LogEntryKind.NO_PATTERN]


def _sink_observed_untranslated_candidates(results: list[TriageResult]) -> list[TriageResult]:
    return [
        result
        for result in results
        if result.entry.kind == LogEntryKind.SINK_OBSERVE
        and result.classification not in _NON_ACTIONABLE_CLASSIFICATIONS
        and _has_ascii_word_candidate(result.entry.text)
    ]


def _final_output_observations(results: list[TriageResult]) -> list[TriageResult]:
    return [result for result in results if result.entry.kind == LogEntryKind.FINAL_OUTPUT_PROBE]


def _message_log_candidates(results: list[TriageResult]) -> list[TriageResult]:
    return [
        result
        for result in results
        if result.entry.kind != LogEntryKind.FINAL_OUTPUT_PROBE
        and (result.entry.kind == LogEntryKind.NO_PATTERN or result.entry.route in _MESSAGE_LOG_ROUTES)
    ]


def _preserved_english_results(results: list[TriageResult]) -> list[TriageResult]:
    return [result for result in results if result.classification == TriageClassification.PRESERVED_ENGLISH]


def _has_ascii_word_candidate(text: str) -> bool:
    return _ASCII_WORD_PATTERN.search(text) is not None


def _markup_issue_candidates(entries: list[LogEntry]) -> list[dict[str, object]]:
    candidates: list[dict[str, object]] = []
    for entry in entries:
        if entry.kind == LogEntryKind.DYNAMIC_TEXT_PROBE and entry.translated_text is not None:
            source_markup = _markup_signature(entry.text)
            translated_markup = _markup_signature(entry.translated_text)
            if source_markup == translated_markup:
                continue
            candidates.append(
                {
                    "text": entry.text,
                    "translated_text": entry.translated_text,
                    "route": entry.route,
                    "kind": entry.kind.value,
                    "line_number": entry.line_number,
                    "source_markup": source_markup,
                    "translated_markup": translated_markup,
                },
            )
        elif entry.kind == LogEntryKind.FINAL_OUTPUT_PROBE:
            source_text = entry.source_text_sample or entry.text
            final_text = entry.final_text_sample or ""
            source_markup = _markup_signature(source_text)
            final_markup = _markup_signature(final_text)
            token_signatures_match = source_markup == final_markup
            span_status_is_issue = entry.markup_span_status not in _NON_ISSUE_MARKUP_SPAN_STATUSES
            semantic_status_is_issue = entry.markup_semantic_status == "drift"
            if token_signatures_match and not span_status_is_issue and not semantic_status_is_issue:
                continue
            candidates.append(
                {
                    "text": entry.text,
                    "final_text": final_text,
                    "route": entry.route,
                    "kind": entry.kind.value,
                    "line_number": entry.line_number,
                    "translation_status": entry.translation_status,
                    "markup_status": entry.markup_status,
                    "markup_span_status": entry.markup_span_status,
                    "markup_semantic_status": entry.markup_semantic_status,
                    "markup_semantic_flags": entry.markup_semantic_flags,
                    "direct_marker_status": entry.direct_marker_status,
                    "source_markup": source_markup,
                    "final_markup": final_markup,
                    "source_markup_spans": entry.source_markup_spans,
                    "final_markup_spans": entry.final_markup_spans,
                },
            )
    return candidates


def _markup_signature(text: str) -> list[str]:
    return _MARKUP_TOKEN_PATTERN.findall(text)


def _count_results_by_route(results: list[TriageResult]) -> dict[str, int]:
    counts: dict[str, int] = {}
    for result in results:
        counts[result.entry.route] = counts.get(result.entry.route, 0) + 1
    return dict(sorted(counts.items()))


def _int_counts(summary: dict[object, object]) -> dict[str, int]:
    return {str(key): int(value) for key, value in summary.items() if isinstance(value, int)}


def _serialize_result(result: TriageResult) -> dict[str, object]:
    entry = result.entry
    payload: dict[str, object] = {
        "text": entry.text,
        "route": entry.route,
        "kind": entry.kind.value,
        "line_number": entry.line_number,
        "classification": result.classification.value,
        "reason": result.reason,
    }
    if entry.hits is not None:
        payload["hits"] = entry.hits
    if entry.family is not None:
        payload["family"] = entry.family
    if entry.translated_text is not None:
        payload["translated_text"] = entry.translated_text
    if entry.changed is not None:
        payload["changed"] = entry.changed
    if entry.kind == LogEntryKind.FINAL_OUTPUT_PROBE:
        for field_name in _FINAL_OUTPUT_OBSERVATION_FIELDS:
            value = getattr(entry, field_name)
            if value is not None:
                payload[field_name] = value
    if result.slot_evidence:
        payload["slot_evidence"] = result.slot_evidence
    return payload


def _confirm_attack_if_requested(capture: _FlowCapture, confirm_key: str) -> None:
    if not confirm_key:
        return
    _send_key_with_backend(capture.pid, confirm_key, (), capture.input_backend)
    time.sleep(capture.wait)


def _capture_message_log(capture: _FlowCapture, message_log_chord: str, label: str) -> None:
    stage_start_offset = _current_log_offset(_PLAYER_LOG)
    if message_log_chord:
        capture.send(message_log_chord)
    capture.screenshot(label, stage_start_offset)
    if message_log_chord:
        capture.close_popup()


def _capture_message_log_after_attack(capture: _FlowCapture, message_log_chord: str) -> None:
    _capture_message_log(capture, message_log_chord, "message-log-after-attack")


def _capture_message_log_after_death(capture: _FlowCapture, message_log_chord: str) -> None:
    _capture_message_log(capture, message_log_chord, "message-log-after-death")


def _send_attack_sequence(
    capture: _FlowCapture,
    sequence: tuple[str, ...],
    attempt_index: int | None = None,
    *,
    capture_steps: bool = True,
) -> None:
    for chord_index, chord in enumerate(sequence):
        stage_start_offset = _current_log_offset(_PLAYER_LOG)
        capture.send(chord)
        is_last_chord = chord_index + 1 == len(sequence)
        if capture_steps and not is_last_chord:
            if attempt_index is None:
                label = f"attack-sequence-step-{chord_index + 1}"
            else:
                label = f"attack-attempt-{attempt_index}-step-{chord_index + 1}"
            capture.screenshot(label, stage_start_offset)


def _repeat_death_attacks(
    capture: _FlowCapture,
    attack_sequence: tuple[str, ...],
    confirm_key: str,
    death_confirm_key: str,
    count: int,
) -> tuple[list[str], str]:
    pre_death_offset = _current_log_offset(_PLAYER_LOG)
    death_evidence_matches: list[str] = []
    for _ in range(count):
        _send_attack_sequence(capture, attack_sequence, capture_steps=False)
        _confirm_attack_if_requested(capture, confirm_key)
        _confirm_attack_if_requested(capture, death_confirm_key)
        death_log_delta = _read_log_delta(_PLAYER_LOG, pre_death_offset)
        death_evidence_matches = _death_evidence_matches(death_log_delta)
        if death_evidence_matches:
            break

    if count:
        capture.screenshot("death-or-combat-end", pre_death_offset)

    death_log_delta = _read_log_delta(_PLAYER_LOG, pre_death_offset)
    return _death_evidence_matches(death_log_delta), death_log_delta


def _perform_final_smoke_attack_check(
    capture: _FlowCapture,
    args: argparse.Namespace,
) -> tuple[list[str], str, tuple[str, ...], list[str], str]:
    pre_attack_offset = _current_log_offset(_PLAYER_LOG)
    attack_sequences = _attack_sequences(args)
    successful_sequence = attack_sequences[-1]
    combat_evidence_matches: list[str] = []

    for attempt_index, attack_sequence in enumerate(attack_sequences, start=1):
        successful_sequence = attack_sequence
        stage_start_offset = _current_log_offset(_PLAYER_LOG)
        _send_attack_sequence(capture, attack_sequence, attempt_index if len(attack_sequences) > 1 else None)
        label = "attack-or-confirmation"
        if len(attack_sequences) > 1:
            label = f"attack-attempt-{attempt_index}-result"
        capture.screenshot(label, stage_start_offset)
        _confirm_attack_if_requested(capture, args.attack_confirm_key)

        combat_log_delta = _read_log_delta(_PLAYER_LOG, pre_attack_offset)
        combat_evidence_matches = _combat_evidence_matches(combat_log_delta)
        if combat_evidence_matches:
            break

    if args.death_attack_count:
        death_evidence_matches, death_log_delta = _repeat_death_attacks(
            capture,
            successful_sequence,
            args.attack_confirm_key,
            args.death_confirm_key,
            args.death_attack_count,
        )
        _capture_message_log_after_death(capture, args.message_log_chord)
    else:
        _capture_message_log_after_attack(capture, args.message_log_chord)
        death_evidence_matches = []
        death_log_delta = ""

    combat_log_delta = _read_log_delta(_PLAYER_LOG, pre_attack_offset)
    return (
        _combat_evidence_matches(combat_log_delta),
        combat_log_delta,
        successful_sequence,
        death_evidence_matches,
        death_log_delta,
    )


def _run_combat_smoke_flow(
    process: subprocess.Popen[bytes],
    args: argparse.Namespace,
    load_ready_matches: list[str],
) -> dict[str, object]:
    output_dir = args.flow_screenshot_dir
    output_dir.mkdir(parents=True, exist_ok=True)
    screenshot_paths: list[str] = []
    stage_captures: list[_StageCapture] = []
    capture = _FlowCapture(
        process.pid,
        output_dir,
        screenshot_paths,
        stage_captures,
        args.flow_step_wait,
        args.flow_screenshot_wait,
        args.input_backend,
    )

    capture.screenshot("after-load")
    if args.npc_poi_key:
        capture.send("backspace")
        capture.send(args.npc_poi_key, args.poi_travel_wait)
    capture.screenshot("before-combat")

    (
        combat_evidence_matches,
        combat_log_delta,
        successful_attack_sequence,
        death_evidence_matches,
        death_log_delta,
    ) = _perform_final_smoke_attack_check(
        capture,
        args,
    )
    if not combat_evidence_matches:
        _raise_missing_combat_evidence(output_dir)

    verification_report = _build_verification_report(stage_captures, _PLAYER_LOG)
    verification_report_path = _write_verification_report(verification_report, output_dir)
    return {
        "build_marker_found": True,
        "load_ready_found": bool(load_ready_matches),
        "load_ready_matches": load_ready_matches,
        "player_log_path": str(_PLAYER_LOG),
        "flow": "combat-smoke",
        "screenshot_dir": str(output_dir),
        "screenshot_paths": screenshot_paths,
        "verification_report_path": str(verification_report_path),
        "verification_report": verification_report,
        "combat_evidence_found": True,
        "combat_evidence_matches": combat_evidence_matches,
        "death_attack_count": args.death_attack_count,
        "death_confirm_key": args.death_confirm_key,
        "death_evidence_found": bool(death_evidence_matches),
        "death_evidence_matches": death_evidence_matches,
        "attack_sequences": [list(sequence) for sequence in _attack_sequences(args)],
        "successful_attack_sequence": list(successful_attack_sequence),
        "combat_log_excerpt": combat_log_delta.splitlines()[-40:],
        "death_log_excerpt": death_log_delta.splitlines()[-40:],
    }


def _run_final_smoke_flow(
    process: subprocess.Popen[bytes],
    args: argparse.Namespace,
    load_ready_matches: list[str],
) -> dict[str, object]:
    output_dir = args.flow_screenshot_dir
    output_dir.mkdir(parents=True, exist_ok=True)
    screenshot_paths: list[str] = []
    stage_captures: list[_StageCapture] = []
    capture = _FlowCapture(
        process.pid,
        output_dir,
        screenshot_paths,
        stage_captures,
        args.flow_step_wait,
        args.flow_screenshot_wait,
        args.input_backend,
    )

    capture.screenshot("after-load")

    _run_inventory_drilldown(capture, args)

    for label, open_sequence, close_sequence in _build_final_smoke_character_screen_stages(args):
        capture.screen_stage(label, open_sequence, close_sequence)

    for label, chord, close_chord in _build_final_smoke_popup_stages():
        capture.popup_stage(label, chord, close_chord)

    if args.npc_poi_key:
        capture.send("backspace")
        capture.send(args.npc_poi_key, args.poi_travel_wait)

    npc_stage_start_offset = _current_log_offset(_PLAYER_LOG)
    capture.send(args.npc_talk_key)
    capture.send(args.npc_talk_direction)
    capture.screenshot("npc-conversation", npc_stage_start_offset)
    capture.close_popup()

    (
        combat_evidence_matches,
        combat_log_delta,
        successful_attack_sequence,
        death_evidence_matches,
        death_log_delta,
    ) = _perform_final_smoke_attack_check(
        capture,
        args,
    )
    if args.require_combat_evidence and not combat_evidence_matches:
        _raise_missing_combat_evidence(output_dir)

    verification_report = _build_verification_report(stage_captures, _PLAYER_LOG)
    verification_report_path = _write_verification_report(verification_report, output_dir)
    return {
        "build_marker_found": True,
        "load_ready_found": bool(load_ready_matches),
        "load_ready_matches": load_ready_matches,
        "player_log_path": str(_PLAYER_LOG),
        "flow": "final-smoke",
        "flow_stage_names": _build_final_smoke_stage_names(
            tab_page_rights=args.inventory_tab_page_rights,
            item_scan_count=args.inventory_item_scan_count,
        ),
        "screenshot_dir": str(output_dir),
        "screenshot_paths": screenshot_paths,
        "verification_report_path": str(verification_report_path),
        "verification_report": verification_report,
        "death_attack_count": args.death_attack_count,
        "death_confirm_key": args.death_confirm_key,
        "combat_evidence_found": bool(combat_evidence_matches),
        "combat_evidence_matches": combat_evidence_matches,
        "death_evidence_found": bool(death_evidence_matches),
        "death_evidence_matches": death_evidence_matches,
        "attack_sequences": [list(sequence) for sequence in _attack_sequences(args)],
        "successful_attack_sequence": list(successful_attack_sequence),
        "combat_log_excerpt": combat_log_delta.splitlines()[-40:],
        "death_log_excerpt": death_log_delta.splitlines()[-40:],
    }


def _raise_invalid_death_attack_count() -> None:
    msg = "--death-attack-count must be 0 or greater."
    raise ValueError(msg)


def _raise_invalid_nonnegative_option(option: str) -> None:
    msg = f"--{option} must be 0 or greater."
    raise ValueError(msg)


def _raise_missing_combat_evidence(output_dir: Path) -> None:
    msg = (
        "No combat evidence was observed after final-smoke attack input. "
        f"Screenshots were still captured in {output_dir}. "
        "The run should not be treated as a combat verification."
    )
    raise RuntimeError(msg)


def _validate_runtime_args(args: argparse.Namespace) -> None:
    if args.death_attack_count < 0:
        _raise_invalid_death_attack_count()
    if args.inventory_tab_page_rights < 0:
        _raise_invalid_nonnegative_option("inventory-tab-page-rights")
    if args.inventory_item_scan_count < 0:
        _raise_invalid_nonnegative_option("inventory-item-scan-count")
    if args.inventory_item_action_row_offset < 0:
        _raise_invalid_nonnegative_option("inventory-item-action-row-offset")
    if args.inventory_item_pane_chord:
        _parse_key_chord(args.inventory_item_pane_chord)
    if args.abilities_chord:
        _parse_key_sequence(args.abilities_chord)
    if args.active_effects_chord:
        _parse_key_sequence(args.active_effects_chord)
    _attack_sequences(args)


def _prepare_final_smoke_runtime(
    args: argparse.Namespace,
) -> tuple[Path | None, Path | None, subprocess.Popen[bytes] | None]:
    if args.flow not in {"final-smoke", "combat-smoke"}:
        return None, None, None

    save_dir = args.save_dir or _discover_latest_qudjp_save_dir(_default_save_root())
    save_backup_dir = None
    if save_dir is not None:
        save_backup_dir = args.save_backup_dir or (args.flow_screenshot_dir / "save-backup")
        _backup_save_dir(save_dir, save_backup_dir)

    return save_dir, save_backup_dir, _start_caffeinate()


def _run_inventory_flow(
    process: subprocess.Popen[bytes],
    args: argparse.Namespace,
    load_ready_matches: list[str],
    inventory_patterns: tuple[str, ...],
) -> dict[str, object]:
    pre_inventory_offset = _current_log_offset(_PLAYER_LOG)
    if args.inventory_key:
        _stabilize_game_focus(process.pid)
        _send_key_chord(process.pid, args.inventory_key, args.input_backend)
    inventory_matches, log_delta = _wait_for_patterns(
        _PLAYER_LOG,
        pre_inventory_offset,
        inventory_patterns,
        args.inventory_timeout,
    )
    _stabilize_game_focus(process.pid)
    _capture_screenshot(args.screenshot_path)
    return _build_result(args.screenshot_path, load_ready_matches, inventory_matches, log_delta)


def _run_loaded_flow(
    process: subprocess.Popen[bytes],
    args: argparse.Namespace,
    load_ready_matches: list[str],
    inventory_patterns: tuple[str, ...],
) -> dict[str, object]:
    if args.flow == "combat-smoke":
        return _run_combat_smoke_flow(process, args, load_ready_matches)
    if args.flow == "final-smoke":
        return _run_final_smoke_flow(process, args, load_ready_matches)
    return _run_inventory_flow(process, args, load_ready_matches, inventory_patterns)


def _main(argv: list[str] | None = None) -> int:
    args = _parse_args(argv)
    inventory_patterns = tuple(args.inventory_pattern or _DEFAULT_INVENTORY_PATTERNS)
    initial_offset = _current_log_offset(_PLAYER_LOG)
    process: subprocess.Popen[bytes] | None = None
    caffeinate_process: subprocess.Popen[bytes] | None = None
    save_dir: Path | None = None
    save_backup_dir: Path | None = None
    result: dict[str, object] | None = None

    try:
        _ensure_supported_environment()
        _ensure_unlocked_console()
        _validate_runtime_args(args)
        _sync_mod(skip_sync=args.skip_sync)
        save_dir, save_backup_dir, caffeinate_process = _prepare_final_smoke_runtime(args)

        process = _launch_game()

        build_matches, _ = _wait_for_patterns(
            _PLAYER_LOG,
            initial_offset,
            (_BUILD_MARKER,),
            args.launch_timeout,
        )
        if not build_matches:
            _raise_build_marker_timeout(args.launch_timeout)

        title_offset = _current_log_offset(_PLAYER_LOG)
        _stabilize_game_focus(process.pid)
        if _should_send_title_load_inputs(manual_load=args.manual_load):
            _perform_title_navigation(process, args, title_offset)
            _continue_from_title(process, args)

        post_continue_offset = _current_log_offset(_PLAYER_LOG)
        load_ready_matches, _ = _wait_for_load_ready(_PLAYER_LOG, post_continue_offset, args.load_ready_timeout)
        if not load_ready_matches:
            if _flow_requires_load_ready(args.flow):
                _raise_load_ready_required_timeout(args.load_ready_timeout)
            time.sleep(args.load_wait)

        result = _run_loaded_flow(process, args, load_ready_matches, inventory_patterns)
    except (FileExistsError, FileNotFoundError, RuntimeError, subprocess.CalledProcessError, ValueError) as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1
    finally:
        if process is not None:
            _stop_game(process, args.quit_confirm_key, args.quit_timeout, args.input_backend)
        _stop_process(caffeinate_process)
        if (
            args.flow in {"final-smoke", "combat-smoke"}
            and args.restore_save
            and save_dir is not None
            and save_backup_dir is not None
        ):
            try:
                _restore_save_dir(save_dir, save_backup_dir)
            except OSError as exc:
                print(f"Error: failed to restore save from {save_backup_dir} to {save_dir}: {exc}", file=sys.stderr)  # noqa: T201

    print(json.dumps(result, ensure_ascii=True, indent=2))  # noqa: T201
    return 0


if __name__ == "__main__":
    sys.exit(_main())
