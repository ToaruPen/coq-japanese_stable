"""Tests for the translation_checker module."""

import subprocess
from pathlib import Path

import pytest

from scripts.translation_checker import (
    _attack_sequences,
    _build_activate_application_script,
    _build_final_smoke_popup_stages,
    _build_final_smoke_stage_names,
    _build_focus_script,
    _build_hammerspoon_focus_lua,
    _build_launch_game_command,
    _build_system_events_key_code_script,
    _combat_evidence_matches,
    _escape_osascript_string,
    _find_matching_patterns,
    _flow_requires_load_ready,
    _flow_screenshot_path,
    _io_console_locked_from_root,
    _key_code_for,
    _load_ready_failure_matches,
    _load_ready_matches,
    _main,
    _modifier_mask,
    _parse_args,
    _parse_hammerspoon_focus_output,
    _parse_key_chord,
    _parse_key_sequence,
    _read_log_delta,
    _should_send_title_load_inputs,
    _stabilize_game_focus,
    _title_navigation_delay,
)


class TestInputMappings:
    def test_maps_special_key_codes(self) -> None:
        assert _key_code_for("return") == 36
        assert _key_code_for("up") == 126
        assert _key_code_for("numpad6") == 88
        assert _key_code_for("backslash") == 42
        assert _key_code_for("oem102") == 42
        assert _key_code_for("jis_yen") == 93

    def test_maps_character_key_codes(self) -> None:
        assert _key_code_for("a") == 0
        assert _key_code_for("b") == 11
        assert _key_code_for("f") == 3
        assert _key_code_for("h") == 4
        assert _key_code_for("m") == 46
        assert _key_code_for("p") == 35
        assert _key_code_for("q") == 12
        assert _key_code_for("x") == 7
        assert _key_code_for(" ") == 49

    def test_parses_key_chord_aliases(self) -> None:
        assert _parse_key_chord("ctrl+f") == ("f", ("control",))
        assert _parse_key_chord("shift+i") == ("i", ("shift",))
        assert _parse_key_chord("return") == ("return", ())

    def test_parses_comma_separated_key_sequence(self) -> None:
        assert _parse_key_sequence("backslash, right") == ("backslash", "right")

    def test_attack_sequences_default_to_attack_chord(self) -> None:
        args = _parse_args([])
        assert _attack_sequences(args) == (("backslash", "right"),)

    def test_attack_sequences_can_try_force_attack_direction_fallback(self) -> None:
        args = _parse_args(
            [
                "--attack-sequence",
                "ctrl+numpad6",
                "--attack-sequence",
                "backslash,right",
            ],
        )
        assert _attack_sequences(args) == (("ctrl+numpad6",), ("backslash", "right"))

    def test_rejects_unknown_modifier(self) -> None:
        with pytest.raises(ValueError, match="Unsupported modifier"):
            _modifier_mask(("hyper",))

    def test_builds_modifier_mask(self) -> None:
        assert _modifier_mask(("command", "shift")) > 0

    def test_builds_focus_only_script(self) -> None:
        script = _build_focus_script(55)
        assert "application process whose unix id is 55" in script
        assert "frontmost" in script

    def test_builds_activate_application_script(self) -> None:
        script = _build_activate_application_script("com.example.Game")
        assert script == 'tell application id "com.example.Game" to activate'

    def test_builds_system_events_key_code_script(self) -> None:
        script = _build_system_events_key_code_script("numpad6", ("control",))
        assert script == 'tell application "System Events" to key code 88 using control down'


class TestFindMatchingPatterns:
    def test_returns_only_present_patterns(self) -> None:
        text = "alpha beta gamma"
        matches = _find_matching_patterns(text, ("beta", "delta", "alpha"))
        assert matches == ["beta", "alpha"]


class TestFinalSmokeFlowHelpers:
    def test_manual_load_skips_scripted_title_input(self) -> None:
        assert _should_send_title_load_inputs(manual_load=False) is True
        assert _should_send_title_load_inputs(manual_load=True) is False

    def test_title_navigation_waits_after_ready_signal(self) -> None:
        assert _title_navigation_delay(["MainMenuLocalizationPatch"], fallback_wait=12.0, post_ready_wait=1.0) == 1.0

    def test_title_navigation_uses_fallback_without_ready_signal(self) -> None:
        assert _title_navigation_delay([], fallback_wait=12.0, post_ready_wait=1.0) == 12.0

    def test_final_smoke_requires_confirmed_load_ready_signal(self) -> None:
        assert _flow_requires_load_ready("final-smoke") is True
        assert _flow_requires_load_ready("combat-smoke") is True
        assert _flow_requires_load_ready("inventory") is False

    def test_flow_screenshot_path_is_numbered_and_slugged(self, tmp_path: Path) -> None:
        path = _flow_screenshot_path(tmp_path, 2, "NPC conversation")
        assert path == tmp_path / "02-npc-conversation.png"

    def test_final_smoke_stage_names_cover_user_requested_runtime_checks(self) -> None:
        stage_names = _build_final_smoke_stage_names()
        assert "after-load" in stage_names
        assert "inventory-inventory" in stage_names
        assert "popup-system-menu" in stage_names
        assert "npc-conversation" in stage_names
        assert "attack-or-confirmation" in stage_names
        assert "message-log-after-attack" in stage_names

    def test_final_smoke_help_toggle_closes_with_same_key(self) -> None:
        popup_stages = {
            label: (open_chord, close_chord)
            for label, open_chord, close_chord in _build_final_smoke_popup_stages()
        }
        assert popup_stages["popup-help"] == ("p", "p")


class TestHammerspoonHelpers:
    def test_escapes_osascript_text(self) -> None:
        assert _escape_osascript_string('a"b\\c') == 'a\\"b\\\\c'

    def test_builds_hammerspoon_focus_lua_with_pid_and_path(self, tmp_path: Path) -> None:
        lua = _build_hammerspoon_focus_lua(123, tmp_path / "out.txt")
        assert "app:pid() == 123" in lua
        assert "out.txt" in lua
        assert 'file:write(table.concat(lines, "\\n"))' in lua

    def test_parses_hammerspoon_focus_output(self) -> None:
        parsed = _parse_hammerspoon_focus_output("found= true\nfront= CoQ\nfocused_title= CavesOfQud\n")
        assert parsed == {"found": "true", "front": "CoQ", "focused_title": "CavesOfQud"}

    def test_stabilize_game_focus_uses_application_activation_first(
        self,
        monkeypatch: pytest.MonkeyPatch,
    ) -> None:
        calls: list[str] = []

        monkeypatch.setattr(
            "scripts.translation_checker._locate_hammerspoon_app",
            lambda: Path("/Applications/Hammerspoon.app"),
        )
        monkeypatch.setattr("scripts.translation_checker._activate_game_application", lambda: calls.append("activate"))
        monkeypatch.setattr(
            "scripts.translation_checker._focus_process_with_hammerspoon",
            lambda pid: calls.append(f"hammerspoon:{pid}"),
        )
        monkeypatch.setattr("scripts.translation_checker._focus_process", lambda pid: calls.append(f"focus:{pid}"))

        _stabilize_game_focus(123)

        assert calls == ["activate"]

    def test_stabilize_game_focus_uses_hammerspoon_when_activation_fails(
        self,
        monkeypatch: pytest.MonkeyPatch,
    ) -> None:
        calls: list[str] = []

        monkeypatch.setattr(
            "scripts.translation_checker._locate_hammerspoon_app",
            lambda: Path("/Applications/Hammerspoon.app"),
        )

        def fail_with_called_process_error(_pid: int) -> None:
            raise subprocess.CalledProcessError(returncode=1, cmd=["osascript"], stderr="denied")

        def fail_activation() -> None:
            raise subprocess.CalledProcessError(returncode=1, cmd=["osascript"], stderr="denied")

        monkeypatch.setattr("scripts.translation_checker._activate_game_application", fail_activation)
        monkeypatch.setattr(
            "scripts.translation_checker._focus_process_with_hammerspoon",
            lambda pid: calls.append(f"hammerspoon:{pid}"),
        )
        monkeypatch.setattr("scripts.translation_checker._focus_process", lambda pid: calls.append(f"focus:{pid}"))

        _stabilize_game_focus(123)

        assert calls == ["hammerspoon:123"]

    def test_stabilize_game_focus_falls_back_to_process_focus_when_helpers_fail(
        self,
        monkeypatch: pytest.MonkeyPatch,
    ) -> None:
        calls: list[str] = []

        monkeypatch.setattr(
            "scripts.translation_checker._locate_hammerspoon_app",
            lambda: Path("/Applications/Hammerspoon.app"),
        )

        def fail_without_pid() -> None:
            raise subprocess.CalledProcessError(returncode=1, cmd=["osascript"], stderr="denied")

        def fail_with_pid(_pid: int) -> None:
            raise subprocess.CalledProcessError(returncode=1, cmd=["osascript"], stderr="denied")

        monkeypatch.setattr("scripts.translation_checker._activate_game_application", fail_without_pid)
        monkeypatch.setattr("scripts.translation_checker._focus_process_with_hammerspoon", fail_with_pid)
        monkeypatch.setattr("scripts.translation_checker._focus_process", lambda pid: calls.append(f"focus:{pid}"))

        _stabilize_game_focus(123)

        assert calls == ["focus:123"]


class TestLaunchHelpers:
    def test_launch_game_command_uses_rosetta_launcher(self) -> None:
        command = _build_launch_game_command()
        assert command[0].endswith("scripts/launch_rosetta.sh")


class TestConsoleLockHelpers:
    def test_detects_locked_console(self) -> None:
        assert _io_console_locked_from_root({"IOConsoleLocked": True}) is True

    def test_detects_unlocked_console(self) -> None:
        assert _io_console_locked_from_root({"IOConsoleLocked": False}) is False


class TestReadLogDelta:
    def test_reads_only_bytes_after_offset(self, tmp_path: Path) -> None:
        log_path = tmp_path / "Player.log"
        log_path.write_text("line1\nline2\nline3\n", encoding="utf-8")
        offset = len(b"line1\n")
        assert _read_log_delta(log_path, offset) == "line2\nline3\n"

    def test_resets_to_start_when_file_rotates(self, tmp_path: Path) -> None:
        log_path = tmp_path / "Player.log"
        log_path.write_text("new\n", encoding="utf-8")
        assert _read_log_delta(log_path, 999) == "new\n"


class TestLoadReadyMatches:
    def test_detects_bottom_context_probe(self) -> None:
        text = "[QudJP] QudMenuBottomContextProbe/RefreshButtonsAfter/v1: buttons=2"
        assert _load_ready_matches(text) == ["[QudJP] QudMenuBottomContextProbe/RefreshButtonsAfter/v1:"]

    def test_detects_non_main_menu_translator_context(self) -> None:
        text = "[QudJP] Translator: missing key 'x' (context: UITextSkinTranslationPatch)"
        assert _load_ready_matches(text) == ["[QudJP] Translator:<non-main-menu>"]

    def test_ignores_translator_initialization_summary(self) -> None:
        text = "[QudJP] Translator: loaded 7902 unique entries from 60 file(s)"
        assert _load_ready_matches(text) == []

    def test_ignores_main_menu_translator_context(self) -> None:
        text = "[QudJP] Translator: missing key 'x' (context: MainMenuLocalizationPatch)"
        assert _load_ready_matches(text) == []

    def test_detects_new_game_instead_of_loaded_save_as_failure(self) -> None:
        text = (
            "[QudJP] DynamicTextProbe/v1: route='ChargenStructuredTextTranslator' "
            "source='Choose Game Mode'\n"
            "ERROR - Booting game :System.NullReferenceException"
        )
        assert _load_ready_failure_matches(text) == [
            "ChargenStructuredTextTranslator",
            "Choose Game Mode",
            "ERROR - Booting game",
        ]


class TestCombatEvidenceMatches:
    def test_detects_attack_confirmation_popup(self) -> None:
        text = "[QudJP] DynamicTextProbe/v1: source='Do you really want to attack タム?'"
        assert _combat_evidence_matches(text) == ["Do you really want to attack"]

    def test_detects_player_hit_message(self) -> None:
        text = (
            "[QudJP] DynamicTextProbe/v1: route='MessagePatternTranslator' "
            "family='^You hit (.+) for (\\d+) damage[.!]?$' "
            "source='You hit snapjaw for 7 damage.'"
        )
        assert _combat_evidence_matches(text)

    def test_detects_player_miss_message_with_markup(self) -> None:
        text = (
            "[QudJP] SinkObserve/v1: sink='MessageLogPatch' "
            "source='{{G|You miss snapjaw.}}'"
        )
        assert _combat_evidence_matches(text)

    def test_detects_non_penetrating_attack_message(self) -> None:
        text = (
            "[QudJP] DynamicTextProbe/v1: route='MessagePatternTranslator' "
            "source='You don't penetrate Tam's armor with your flaming bronze dagger. [2]'"
        )
        assert _combat_evidence_matches(text)

    def test_detects_translated_non_penetrating_attack_message(self) -> None:
        text = (
            "[QudJP] DynamicTextProbe/v1: route='GameObjectEmitMessageTranslationPatch' "
            "translated='燃え盛る 青銅の短剣ではタム、ドロマド商団の装甲を貫けない。[2]'"
        )
        assert _combat_evidence_matches(text)

    def test_ignores_non_combat_runtime_messages(self) -> None:
        text = (
            "[QudJP] DynamicTextProbe/v1: source='You pass by ウォーターヴァイン.'\n"
            "[QudJP] DynamicTextProbe/v1: source='You have no missile weapon equipped!'\n"
            "[QudJP] DynamicTextProbe/v1: source='Save and Quit'"
        )
        assert _combat_evidence_matches(text) == []


class TestParseArgs:
    def test_defaults_match_observed_title_menu_flow(self) -> None:
        args = _parse_args([])
        assert args.menu_up_presses == 0
        assert args.menu_down_presses == 0
        assert args.menu_up_key == "up"
        assert args.menu_down_key == "down"
        assert args.continue_key == "c"
        assert args.continue_presses == 3
        assert args.save_select_key == "space"
        assert args.save_select_presses == 1
        assert args.continue_click_x is None
        assert args.continue_click_y is None
        assert args.manual_load is False
        assert args.input_backend == "cgevent"
        assert args.npc_poi_key == "d"
        assert args.attack_chord == "backslash,right"
        assert args.attack_sequence is None
        assert args.attack_confirm_key == ""
        assert args.require_combat_evidence is False
        assert args.load_ready_timeout == 45.0
        assert args.title_ready_wait == 12.0
        assert args.title_ready_post_wait == 1.0

    def test_manual_load_flag_is_parsed(self) -> None:
        args = _parse_args(["--manual-load", "--load-ready-timeout", "300"])
        assert args.manual_load is True
        assert args.load_ready_timeout == 300.0

    def test_require_combat_evidence_flag_is_parsed(self) -> None:
        args = _parse_args(["--require-combat-evidence"])
        assert args.require_combat_evidence is True

    def test_combat_smoke_flow_is_parsed(self) -> None:
        args = _parse_args(["--flow", "combat-smoke"])
        assert args.flow == "combat-smoke"


class TestMainErrorHandling:
    def test_reports_existing_save_backup_as_clean_error(
        self,
        tmp_path: Path,
        monkeypatch: pytest.MonkeyPatch,
        capsys: pytest.CaptureFixture[str],
    ) -> None:
        log_path = tmp_path / "Player.log"
        log_path.write_text("", encoding="utf-8")

        def raise_existing_backup(_args: object) -> tuple[None, None, None]:
            msg = "Save backup directory already exists: /tmp/qudjp/save-backup"
            raise FileExistsError(msg)

        def skip_sync_mod(*, skip_sync: bool) -> None:
            _ = skip_sync

        monkeypatch.setattr("scripts.translation_checker._PLAYER_LOG", log_path)
        monkeypatch.setattr("scripts.translation_checker._ensure_supported_environment", lambda: None)
        monkeypatch.setattr("scripts.translation_checker._ensure_unlocked_console", lambda: None)
        monkeypatch.setattr("scripts.translation_checker._validate_runtime_args", lambda _args: None)
        monkeypatch.setattr("scripts.translation_checker._sync_mod", skip_sync_mod)
        monkeypatch.setattr("scripts.translation_checker._prepare_final_smoke_runtime", raise_existing_backup)

        exit_code = _main(["--skip-sync", "--flow", "final-smoke"])

        captured = capsys.readouterr()
        assert exit_code == 1
        assert "Error: Save backup directory already exists" in captured.err
