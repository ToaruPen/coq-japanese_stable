"""Static safety contracts for the standalone Windows Harmony updater."""

from __future__ import annotations

import re
from pathlib import Path

import pytest

_ASSET_DIR = Path("steam/harmony-patch")
_INSTALL_CMD = _ASSET_DIR / "Install Harmony 2.4.2.cmd"
_RESTORE_CMD = _ASSET_DIR / "Restore Game Harmony.cmd"
_UPDATER = _ASSET_DIR / "QudJP-Harmony-2.4.2.ps1"
_README = _ASSET_DIR / "README-ja.txt"

_GAME_HASH = "0de0118c8f1d4408de389ca33b46d2ff7778f3a8541b430cae729ec913d899c7"
_PAYLOAD_HASH = "77e6901ecc606aec66c2a972782a3779e4f50c037d2d165eb7ececdd4d8f794d"
_BACKUP_NAME = "0Harmony.dll.qudjp-backup-before-2.4.2"
_GAME_SUFFIX = r"Caves of Qud\CoQ_Data\Managed\0Harmony.dll"


def _text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def _function_text(script: str, function_name: str) -> str:
    match = re.search(
        rf"(?ms)^function {re.escape(function_name)} \{{\n(.*?)(?=^function |\Z)",
        script,
    )
    assert match is not None, function_name
    return match.group(1)


def _assert_in_order(text: str, *needles: str) -> None:
    position = -1
    for needle in needles:
        next_position = text.find(needle, position + 1)
        assert next_position != -1, needle
        position = next_position


@pytest.mark.parametrize("asset", [_INSTALL_CMD, _RESTORE_CMD, _UPDATER])
def test_windows_harmony_updater_assets_exist(asset: Path) -> None:
    """The tracked package inputs contain both launchers and their shared script."""
    assert asset.is_file(), asset


@pytest.mark.parametrize(
    ("wrapper", "operation"),
    [(_INSTALL_CMD, "Install"), (_RESTORE_CMD, "Restore")],
)
def test_cmd_wrappers_expose_only_the_fixed_operation(wrapper: Path, operation: str) -> None:
    """Launchers cannot forward caller-controlled PowerShell arguments."""
    text = _text(wrapper)
    expected_call = (
        f'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0QudJP-Harmony-2.4.2.ps1" -Operation {operation}'
    )

    assert expected_call in text
    assert "%*" not in text
    assert "-TargetDll" not in text
    assert "-Yes" not in text
    powershell_lines = [line for line in text.splitlines() if line.lower().startswith("powershell")]
    assert powershell_lines == [expected_call]


def test_shared_script_has_a_narrow_powershell_51_entrypoint() -> None:
    """The shared entrypoint accepts only a validated operation and a DLL path."""
    script = _text(_UPDATER)
    normalized = re.sub(r"\s+", " ", script)

    assert "#requires -Version 5.1" in script
    assert re.search(
        r"param\(\s*\[ValidateSet\('Install',\s*'Restore'\)\]\s*\[string\]\$Operation,"
        r"\s*\[string\]\$TargetDll\s*\)",
        normalized,
    )
    assert "Invoke-Expression" not in script
    assert "ScriptBlock" not in script


def test_shared_script_pins_only_the_reviewed_transition() -> None:
    """Both endpoint hashes, the backup name, and the exact game suffix are pinned."""
    script = _text(_UPDATER)

    assert _GAME_HASH in script
    assert _PAYLOAD_HASH in script
    assert _BACKUP_NAME in script
    assert _GAME_SUFFIX in script
    assert "EndsWith($GameDllSuffix, [StringComparison]::OrdinalIgnoreCase)" in script


def test_target_discovery_covers_steam_libraries_and_manual_fallback() -> None:
    """Registry and VDF discovery fall back to selecting the exact game DLL."""
    script = _text(_UPDATER)

    for needle in (
        r"HKCU:\SOFTWARE\Valve\Steam",
        r"HKLM:\SOFTWARE\WOW6432Node\Valve\Steam",
        "InstallPath",
        "libraryfolders.vdf",
        "steamapps\\common\\Caves of Qud",
        "Microsoft.Win32.OpenFileDialog",
        "0Harmony.dll",
        "Resolve-ValidatedTargetDll",
    ):
        assert needle in script


def test_script_refuses_to_mutate_a_running_game() -> None:
    """The updater detects CoQ and never attempts to terminate it."""
    script = _text(_UPDATER)
    process_guard = _function_text(script, "Assert-CoQNotRunning")

    assert "Get-Process -Name 'CoQ' -ErrorAction SilentlyContinue" in process_guard
    assert "throw" in process_guard.lower()
    assert "Stop-Process" not in script
    assert "Kill(" not in script


def test_script_never_self_elevates_from_the_user_writable_package() -> None:
    """A protected game directory fails closed instead of relaunching package code."""
    script = _text(_UPDATER)
    main = _function_text(script, "Invoke-Updater")

    assert "Test-DirectoryWritable" in main
    assert "Start-Process" not in script
    assert "Restart-Elevated" not in script
    assert "WindowsPrincipal" not in script
    assert "powershell.exe" not in script.lower()
    assert "Install Harmony 2.4.2.cmd" in main
    assert "Restore Game Harmony.cmd" in main
    assert "管理者として実行" in main


def test_readme_requires_manual_explorer_elevation() -> None:
    """Users are told to close the failed run and elevate only the fixed wrapper."""
    readme = _text(_README)

    assert "自動的に管理者権限で再実行しません" in readme
    assert "この画面を閉じ" in readme
    assert "Install Harmony 2.4.2.cmd" in readme
    assert "Restore Game Harmony.cmd" in readme
    assert "管理者として実行" in readme


def test_install_requires_literal_confirmation_and_validates_before_mutation() -> None:
    """Install accepts only INSTALL after validating payload and supported game state."""
    script = _text(_UPDATER)
    install = _function_text(script, "Install-Harmony")
    confirmation = _function_text(script, "Request-LiteralConfirmation")
    main = _function_text(script, "Invoke-Updater")

    assert "Read-Host" in confirmation
    assert "-cne $ExpectedLiteral" in confirmation
    assert "'INSTALL'" in install
    assert "'RESTORE'" in script
    assert "-Yes" not in script
    _assert_in_order(main, "Assert-FileHash -Path $payloadPath", "Resolve-TargetDll", "Assert-CoQNotRunning")
    _assert_in_order(
        install,
        "Get-FileSha256 -Path $ResolvedTarget",
        "$PayloadSha256",
        "$SupportedGameSha256",
        "Request-LiteralConfirmation -ExpectedLiteral 'INSTALL'",
        "Copy-VerifiedOriginalBackup",
        "Replace-WithVerifiedFile",
        "Assert-FileHash -Path $ResolvedTarget -ExpectedHash $PayloadSha256",
    )


def test_install_fast_path_requires_a_verified_restorable_backup() -> None:
    """Installed payload is success only while the known original can be restored."""
    script = _text(_UPDATER)
    install = _function_text(script, "Install-Harmony")

    _assert_in_order(
        install,
        "$backupPath = Join-Path",
        "if ($currentHash -eq $PayloadSha256)",
        "Enter-MutationLock -TargetPath $ResolvedTarget",
        "Assert-FileHash -Path $ResolvedTarget -ExpectedHash $PayloadSha256",
        "Assert-FileHash -Path $backupPath -ExpectedHash $SupportedGameSha256",
        "Steam",
        "インストール済みファイルの整合性を確認",
        "return",
    )


def test_install_publishes_only_a_closed_verified_temporary_backup() -> None:
    """The final backup name is populated only by a no-overwrite atomic move."""
    script = _text(_UPDATER)
    backup = _function_text(script, "Copy-VerifiedOriginalBackup")

    assert "Split-Path -Parent $BackupPath" in backup
    assert "Join-Path $backupDirectory" in backup
    assert "[guid]::NewGuid()" in backup
    assert "[IO.FileMode]::CreateNew" in backup
    assert "[IO.FileShare]::None" in backup
    assert ".CopyTo(" in backup
    assert ".Flush(" in backup
    assert "catch [IO.IOException]" in backup
    _assert_in_order(
        backup,
        "$temporaryPath = Join-Path $backupDirectory",
        "[IO.FileMode]::CreateNew",
        ".CopyTo(",
        ".Flush(",
        ".Dispose()",
        "Assert-FileHash -Path $temporaryPath -ExpectedHash $SupportedGameSha256",
        "[IO.File]::Move($temporaryPath, $BackupPath)",
        "catch [IO.IOException]",
        "Test-Path -LiteralPath $BackupPath",
        "Assert-FileHash -Path $BackupPath -ExpectedHash $SupportedGameSha256",
        "finally",
        "Remove-Item -LiteralPath $temporaryPath",
    )
    assert "Copy-Item" not in backup
    assert "Remove-Item -LiteralPath $BackupPath" not in script
    assert "Move-Item" not in backup


def test_mutation_window_uses_a_cross_session_target_directory_lock() -> None:
    """Both operations hold one exclusive Managed-directory lock file."""
    script = _text(_UPDATER)
    enter_lock = _function_text(script, "Enter-MutationLock")
    exit_lock = _function_text(script, "Exit-MutationLock")
    install = _function_text(script, "Install-Harmony")
    restore = _function_text(script, "Restore-GameHarmony")

    assert "Local\\" not in script
    assert "System.Threading.Mutex" not in script
    assert "$MutationLockName" in script
    assert "Split-Path -Parent $TargetPath" in enter_lock
    assert "Join-Path $targetDirectory $MutationLockName" in enter_lock
    assert "[IO.FileMode]::OpenOrCreate" in enter_lock
    assert "[IO.FileAccess]::ReadWrite" in enter_lock
    assert "[IO.FileShare]::None" in enter_lock
    assert "TotalSeconds -ge 30" in enter_lock
    assert "Start-Sleep" in enter_lock
    assert ".Dispose()" in exit_lock
    assert "Remove-Item -LiteralPath $lockPath" not in script
    _assert_in_order(
        install,
        "Request-LiteralConfirmation -ExpectedLiteral 'INSTALL'",
        "Enter-MutationLock -TargetPath $ResolvedTarget",
        "Assert-FileHash -Path $ResolvedTarget -ExpectedHash $SupportedGameSha256",
        "Assert-CoQNotRunning",
        "Copy-VerifiedOriginalBackup",
        "Replace-WithVerifiedFile",
        "Exit-MutationLock",
    )
    _assert_in_order(
        restore,
        "Request-LiteralConfirmation -ExpectedLiteral 'RESTORE'",
        "Enter-MutationLock -TargetPath $ResolvedTarget",
        "Assert-FileHash -Path $ResolvedTarget -ExpectedHash $PayloadSha256",
        "Assert-FileHash -Path $backupPath -ExpectedHash $SupportedGameSha256",
        "Assert-CoQNotRunning",
        "Replace-WithVerifiedFile",
        "Exit-MutationLock",
    )
    for mutation in (install, restore):
        assert "finally" in mutation
        assert "Exit-MutationLock" in mutation


def test_install_uses_verified_sibling_temp_and_rolls_back_on_failure() -> None:
    """Replacement stays in Managed and restores only a verified original backup."""
    script = _text(_UPDATER)
    replace = _function_text(script, "Replace-WithVerifiedFile")
    install = _function_text(script, "Install-Harmony")
    rollback = _function_text(script, "Restore-VerifiedBackupOnFailure")

    assert "Split-Path -Parent $TargetPath" in replace
    assert "Join-Path $targetDirectory" in replace
    _assert_in_order(replace, "Copy-Item", "Assert-FileHash -Path $temporaryPath", "Move-Item")
    assert "catch" in install
    assert "Restore-VerifiedBackupOnFailure" in install
    _assert_in_order(
        rollback,
        "Assert-FileHash -Path $BackupPath -ExpectedHash $SupportedGameSha256",
        "Replace-WithVerifiedFile",
        "Assert-FileHash -Path $TargetPath -ExpectedHash $SupportedGameSha256",
    )


def test_restore_accepts_only_verified_payload_and_supported_backup() -> None:
    """Restore is a strict 2.4.2-to-supported-original transition and keeps backup."""
    script = _text(_UPDATER)
    restore = _function_text(script, "Restore-GameHarmony")

    _assert_in_order(
        restore,
        "Assert-FileHash -Path $ResolvedTarget -ExpectedHash $PayloadSha256",
        "Assert-FileHash -Path $backupPath -ExpectedHash $SupportedGameSha256",
        "Request-LiteralConfirmation -ExpectedLiteral 'RESTORE'",
        "Replace-WithVerifiedFile",
        "Assert-FileHash -Path $ResolvedTarget -ExpectedHash $SupportedGameSha256",
    )
    assert "$BackupName" in restore
    assert "Remove-Item -LiteralPath $backupPath" not in restore
