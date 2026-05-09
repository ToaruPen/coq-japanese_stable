"""Tests for GameTypeResolver target-name validation."""

from __future__ import annotations

import json
from typing import TYPE_CHECKING

from scripts import check_game_type_resolver_names

if TYPE_CHECKING:
    from pathlib import Path

    import pytest


def test_validate_game_type_resolver_calls_accepts_literal_and_const_targets(tmp_path: Path) -> None:
    """Resolver calls match decompiled full type names, including local const aliases."""
    repo_root = tmp_path / "repo"
    patch_root = repo_root / "Mods" / "QudJP" / "Assemblies" / "src" / "Patches"
    patch_root.mkdir(parents=True)
    patch_source = patch_root / "XDidYTranslationPatch.cs"
    patch_source.write_text(
        """
namespace QudJP.Patches;

internal static class XDidYTranslationPatch
{
    private const string TheTypeName = "XRL.The";

    private static readonly Type? MessagingType =
        GameTypeResolver.FindType("XRL.World.Capabilities.Messaging", "Messaging");
    private static readonly Type? TheType =
        GameTypeResolver.FindType(TheTypeName, "The");
}
""",
        encoding="utf-8",
    )
    decompiled_root = tmp_path / "decompiled"
    (decompiled_root / "XRL").mkdir(parents=True)
    (decompiled_root / "XRL.World.Capabilities").mkdir(parents=True)
    (decompiled_root / "XRL" / "The.cs").write_text(
        """
namespace XRL;

public static class The
{
}
""",
        encoding="utf-8",
    )
    (decompiled_root / "XRL.World.Capabilities" / "Messaging.cs").write_text(
        """
namespace XRL.World.Capabilities;

public static class Messaging
{
}
""",
        encoding="utf-8",
    )

    result = check_game_type_resolver_names.validate_game_type_resolver_calls(
        source_root=patch_root,
        decompiled_root=decompiled_root,
        repo_root=repo_root,
    )

    assert result.checked == 2
    assert result.unresolved == ()
    assert result.mismatches == ()


def test_validate_game_type_resolver_calls_accepts_qualified_const_targets(tmp_path: Path) -> None:
    """Resolver calls match decompiled full type names through same-file qualified const aliases."""
    repo_root = tmp_path / "repo"
    patch_root = repo_root / "Mods" / "QudJP" / "Assemblies" / "src" / "Patches"
    patch_root.mkdir(parents=True)
    (patch_root / "GrammarPatch.cs").write_text(
        """
namespace QudJP.Patches;

internal static class GrammarPatchTarget
{
    internal const string TypeName = "XRL.Language.Grammar";
}

internal static class GrammarPatchHelpers
{
    private static Type? ResolveTargetType()
    {
        return GameTypeResolver.FindType(GrammarPatchTarget.TypeName, simpleTypeName: "Grammar");
    }
}
""",
        encoding="utf-8",
    )
    decompiled_root = tmp_path / "decompiled"
    (decompiled_root / "XRL.Language").mkdir(parents=True)
    (decompiled_root / "XRL.Language" / "Grammar.cs").write_text(
        """
namespace XRL.Language;

public static class Grammar
{
}
""",
        encoding="utf-8",
    )

    result = check_game_type_resolver_names.validate_game_type_resolver_calls(
        source_root=patch_root,
        decompiled_root=decompiled_root,
        repo_root=repo_root,
    )

    assert result.checked == 1
    assert result.unresolved == ()
    assert result.mismatches == ()


def test_validate_game_type_resolver_calls_accepts_reordered_named_arguments(tmp_path: Path) -> None:
    """Named FindType arguments are checked even when simpleTypeName comes first."""
    repo_root = tmp_path / "repo"
    patch_root = repo_root / "Mods" / "QudJP" / "Assemblies" / "src" / "Patches"
    patch_root.mkdir(parents=True)
    (patch_root / "Patch.cs").write_text(
        """
namespace QudJP.Patches;

internal static class Patch
{
    private static readonly Type? TheType =
        GameTypeResolver.FindType(simpleTypeName: "The", fullTypeName: "XRL.The");
}
""",
        encoding="utf-8",
    )
    decompiled_root = tmp_path / "decompiled"
    (decompiled_root / "XRL").mkdir(parents=True)
    (decompiled_root / "XRL" / "The.cs").write_text(
        """
namespace XRL;

public static class The
{
}
""",
        encoding="utf-8",
    )

    result = check_game_type_resolver_names.validate_game_type_resolver_calls(
        source_root=patch_root,
        decompiled_root=decompiled_root,
        repo_root=repo_root,
    )

    assert result.checked == 1
    assert result.unresolved == ()
    assert result.mismatches == ()


def test_validate_game_type_resolver_calls_reports_wrong_reordered_named_full_type_name(tmp_path: Path) -> None:
    """A stale fullTypeName is reported when named FindType arguments are reordered."""
    repo_root = tmp_path / "repo"
    patch_root = repo_root / "Mods" / "QudJP" / "Assemblies" / "src" / "Patches"
    patch_root.mkdir(parents=True)
    (patch_root / "Patch.cs").write_text(
        """
namespace QudJP.Patches;

internal static class Patch
{
    private static readonly Type? TheType =
        GameTypeResolver.FindType(simpleTypeName: "The", fullTypeName: "XRL.World.The");
}
""",
        encoding="utf-8",
    )
    decompiled_root = tmp_path / "decompiled"
    (decompiled_root / "XRL").mkdir(parents=True)
    (decompiled_root / "XRL" / "The.cs").write_text(
        """
namespace XRL;

public static class The
{
}
""",
        encoding="utf-8",
    )

    result = check_game_type_resolver_names.validate_game_type_resolver_calls(
        source_root=patch_root,
        decompiled_root=decompiled_root,
        repo_root=repo_root,
    )

    assert result.checked == 1
    assert result.unresolved == ()
    assert len(result.mismatches) == 1
    assert result.mismatches[0].full_type_name == "XRL.World.The"
    assert result.mismatches[0].candidates == ("XRL.The",)


def test_validate_game_type_resolver_calls_reports_wrong_qualified_const_target(tmp_path: Path) -> None:
    """A stale qualified const alias is reported instead of being skipped."""
    repo_root = tmp_path / "repo"
    patch_root = repo_root / "Mods" / "QudJP" / "Assemblies" / "src" / "Patches"
    patch_root.mkdir(parents=True)
    (patch_root / "GrammarPatch.cs").write_text(
        """
namespace QudJP.Patches;

internal static class GrammarPatchTarget
{
    internal const string TypeName = "XRL.World.Grammar";
}

internal static class GrammarPatchHelpers
{
    private static Type? ResolveTargetType()
    {
        return GameTypeResolver.FindType(GrammarPatchTarget.TypeName, simpleTypeName: "Grammar");
    }
}
""",
        encoding="utf-8",
    )
    decompiled_root = tmp_path / "decompiled"
    (decompiled_root / "XRL.Language").mkdir(parents=True)
    (decompiled_root / "XRL.Language" / "Grammar.cs").write_text(
        """
namespace XRL.Language;

public static class Grammar
{
}
""",
        encoding="utf-8",
    )

    result = check_game_type_resolver_names.validate_game_type_resolver_calls(
        source_root=patch_root,
        decompiled_root=decompiled_root,
        repo_root=repo_root,
    )

    assert result.checked == 1
    assert result.unresolved == ()
    assert len(result.mismatches) == 1
    assert result.mismatches[0].full_type_name == "XRL.World.Grammar"
    assert result.mismatches[0].candidates == ("XRL.Language.Grammar",)


def test_validate_game_type_resolver_calls_resolves_unqualified_const_in_enclosing_type(tmp_path: Path) -> None:
    """Unqualified const references use the caller's type scope instead of another class's same-name const."""
    repo_root = tmp_path / "repo"
    patch_root = repo_root / "Mods" / "QudJP" / "Assemblies" / "src" / "Patches"
    patch_root.mkdir(parents=True)
    (patch_root / "Patch.cs").write_text(
        """
namespace QudJP.Patches;

internal static class FirstPatchTarget
{
    private const string TypeName = "XRL.The";

    private static Type? Resolve()
    {
        return GameTypeResolver.FindType(TypeName, "The");
    }
}

internal static class SecondPatchTarget
{
    private const string TypeName = "XRL.World.The";
}
""",
        encoding="utf-8",
    )
    decompiled_root = tmp_path / "decompiled"
    (decompiled_root / "XRL").mkdir(parents=True)
    (decompiled_root / "XRL" / "The.cs").write_text(
        """
namespace XRL;

public static class The
{
}
""",
        encoding="utf-8",
    )

    result = check_game_type_resolver_names.validate_game_type_resolver_calls(
        source_root=patch_root,
        decompiled_root=decompiled_root,
        repo_root=repo_root,
    )

    assert result.checked == 1
    assert result.unresolved == ()
    assert result.mismatches == ()


def test_validate_game_type_resolver_calls_reports_wrong_full_type_name(tmp_path: Path) -> None:
    """A simple-name fallback candidate does not hide a wrong full type name."""
    repo_root = tmp_path / "repo"
    patch_root = repo_root / "Mods" / "QudJP" / "Assemblies" / "src" / "Patches"
    patch_root.mkdir(parents=True)
    (patch_root / "Patch.cs").write_text(
        """
namespace QudJP.Patches;

internal static class Patch
{
    private static readonly Type? TheType =
        GameTypeResolver.FindType("XRL.World.The", "The");
}
""",
        encoding="utf-8",
    )
    decompiled_root = tmp_path / "decompiled"
    (decompiled_root / "XRL").mkdir(parents=True)
    (decompiled_root / "XRL" / "The.cs").write_text(
        """
namespace XRL;

public static class The
{
}
""",
        encoding="utf-8",
    )

    result = check_game_type_resolver_names.validate_game_type_resolver_calls(
        source_root=patch_root,
        decompiled_root=decompiled_root,
        repo_root=repo_root,
    )

    assert result.checked == 1
    assert len(result.mismatches) == 1
    mismatch = result.mismatches[0]
    assert mismatch.full_type_name == "XRL.World.The"
    assert mismatch.simple_type_name == "The"
    assert mismatch.candidates == ("XRL.The",)


def test_main_writes_json_and_returns_failure_for_mismatch(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """The CLI reports machine-readable mismatches for local preflight use."""
    repo_root = tmp_path / "repo"
    patch_root = repo_root / "patches"
    patch_root.mkdir(parents=True)
    (patch_root / "Patch.cs").write_text(
        'GameTypeResolver.FindType("XRL.World.The", "The");\n',
        encoding="utf-8",
    )
    decompiled_root = tmp_path / "decompiled"
    (decompiled_root / "XRL").mkdir(parents=True)
    (decompiled_root / "XRL" / "The.cs").write_text("namespace XRL; public static class The {}\n", encoding="utf-8")
    output = tmp_path / "result.json"

    exit_code = check_game_type_resolver_names.main(
        [
            "--source-root",
            str(patch_root),
            "--decompiled-root",
            str(decompiled_root),
            "--repo-root",
            str(repo_root),
            "--output",
            str(output),
        ],
    )

    payload = json.loads(output.read_text(encoding="utf-8"))
    captured = capsys.readouterr()
    assert exit_code == 1
    assert payload["checked"] == 1
    assert payload["mismatches"][0]["full_type_name"] == "XRL.World.The"
    assert "non_ok=1" in captured.out


def test_main_reports_zero_ok_for_unresolved_only_case(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Unresolved static arguments do not make the printed ok count negative."""
    repo_root = tmp_path / "repo"
    patch_root = repo_root / "patches"
    patch_root.mkdir(parents=True)
    (patch_root / "Patch.cs").write_text(
        'GameTypeResolver.FindType(Targets.TypeName, "The");\n',
        encoding="utf-8",
    )
    decompiled_root = tmp_path / "decompiled"
    decompiled_root.mkdir()
    output = tmp_path / "result.json"

    exit_code = check_game_type_resolver_names.main(
        [
            "--source-root",
            str(patch_root),
            "--decompiled-root",
            str(decompiled_root),
            "--repo-root",
            str(repo_root),
            "--output",
            str(output),
        ],
    )

    payload = json.loads(output.read_text(encoding="utf-8"))
    captured = capsys.readouterr()
    assert exit_code == 1
    assert payload["checked"] == 0
    assert len(payload["unresolved"]) == 1
    assert "checked=0 ok=0 non_ok=1" in captured.out
