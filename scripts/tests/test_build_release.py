"""Tests for the build_release module."""

import json
import zipfile
from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

from scripts.build_release import (
    build_dll,
    build_release,
    collect_localization_files,
    create_zip,
    main,
    read_version,
)


class TestReadVersion:
    """Tests for read_version."""

    def test_reads_version_from_manifest(self, tmp_path: Path) -> None:
        """Version string is extracted from the Version field."""
        manifest = tmp_path / "manifest.json"
        manifest.write_text(json.dumps({"Version": "1.2.3"}), encoding="utf-8")
        assert read_version(manifest) == "1.2.3"

    def test_reads_dev_version(self, tmp_path: Path) -> None:
        """Dev version strings like '0.1.0-dev' are returned as-is."""
        manifest = tmp_path / "manifest.json"
        manifest.write_text(json.dumps({"Version": "0.1.0-dev"}), encoding="utf-8")
        assert read_version(manifest) == "0.1.0-dev"

    def test_missing_manifest_raises(self, tmp_path: Path) -> None:
        """FileNotFoundError is raised when manifest.json does not exist."""
        with pytest.raises(FileNotFoundError, match=r"manifest\.json not found"):
            read_version(tmp_path / "nonexistent.json")

    def test_missing_version_key_raises(self, tmp_path: Path) -> None:
        """KeyError is raised when the Version key is absent."""
        manifest = tmp_path / "manifest.json"
        manifest.write_text(json.dumps({"Id": "QudJP"}), encoding="utf-8")
        with pytest.raises(KeyError):
            read_version(manifest)

    def test_empty_version_raises(self, tmp_path: Path) -> None:
        """ValueError is raised when the Version field is empty."""
        manifest = tmp_path / "manifest.json"
        manifest.write_text(json.dumps({"Version": ""}), encoding="utf-8")
        with pytest.raises(ValueError, match="Version field is empty"):
            read_version(manifest)


class TestCollectLocalizationFiles:
    """Tests for collect_localization_files."""

    def test_collects_xml_files(self, tmp_path: Path) -> None:
        """XML files are collected recursively."""
        loc = tmp_path / "Localization"
        loc.mkdir()
        (loc / "Creatures.jp.xml").write_text("<objects/>", encoding="utf-8")
        sub = loc / "ObjectBlueprints"
        sub.mkdir()
        (sub / "Items.jp.xml").write_text("<objects/>", encoding="utf-8")

        files = collect_localization_files(loc)
        names = {f.name for f in files}
        assert "Creatures.jp.xml" in names
        assert "Items.jp.xml" in names

    def test_collects_json_files(self, tmp_path: Path) -> None:
        """JSON files are collected alongside XML files."""
        loc = tmp_path / "Localization"
        dicts = loc / "Dictionaries"
        dicts.mkdir(parents=True)
        (dicts / "ui.json").write_text("{}", encoding="utf-8")

        files = collect_localization_files(loc)
        names = {f.name for f in files}
        assert "ui.json" in names

    def test_excludes_non_xml_json(self, tmp_path: Path) -> None:
        """Non-XML/JSON files are not collected."""
        loc = tmp_path / "Localization"
        loc.mkdir()
        (loc / "readme.txt").write_text("ignore me", encoding="utf-8")
        (loc / "data.csv").write_text("a,b", encoding="utf-8")

        files = collect_localization_files(loc)
        names = {f.name for f in files}
        assert "readme.txt" not in names
        assert "data.csv" not in names

    def test_missing_directory_raises(self, tmp_path: Path) -> None:
        """FileNotFoundError is raised when the directory does not exist."""
        with pytest.raises(FileNotFoundError, match="Localization directory not found"):
            collect_localization_files(tmp_path / "nonexistent")

    def test_returns_sorted_list(self, tmp_path: Path) -> None:
        """Returned list is sorted."""
        loc = tmp_path / "Localization"
        loc.mkdir()
        (loc / "z_last.xml").write_text("<x/>", encoding="utf-8")
        (loc / "a_first.xml").write_text("<x/>", encoding="utf-8")

        files = collect_localization_files(loc)
        assert files == sorted(files)


class TestCreateZip:
    """Tests for create_zip."""

    def _make_inputs(
        self,
        tmp_path: Path,
    ) -> tuple[Path, Path, Path, Path, list[Path], list[Path]]:
        """Create minimal file tree for create_zip tests."""
        manifest = tmp_path / "manifest.json"
        manifest.write_text(json.dumps({"Version": "0.1.0"}), encoding="utf-8")

        dll = tmp_path / "QudJP.dll"
        dll.write_bytes(b"\x00\x01\x02")

        loc_dir = tmp_path / "Localization"
        loc_dir.mkdir()
        xml_file = loc_dir / "Creatures.jp.xml"
        xml_file.write_text("<objects/>", encoding="utf-8")
        json_file = loc_dir / "ui.json"
        json_file.write_text("{}", encoding="utf-8")

        license_file = tmp_path / "LICENSE"
        license_file.write_text("MIT License", encoding="utf-8")
        notice_file = tmp_path / "NOTICE.md"
        notice_file.write_text("# NOTICE", encoding="utf-8")

        output = tmp_path / "dist" / "QudJP-v0.1.0.zip"
        return output, manifest, dll, loc_dir, [xml_file, json_file], [
            license_file,
            notice_file,
        ]

    def test_creates_zip_file(self, tmp_path: Path) -> None:
        """A ZIP file is created at the specified output path."""
        output, manifest, dll, loc_dir, loc_files, legal_files = self._make_inputs(
            tmp_path,
        )
        create_zip(
            output,
            manifest,
            dll,
            loc_dir,
            loc_files,
            legal_files=legal_files,
        )
        assert output.exists()

    def test_zip_contains_manifest(self, tmp_path: Path) -> None:
        """ZIP contains QudJP/manifest.json."""
        output, manifest, dll, loc_dir, loc_files, legal_files = self._make_inputs(
            tmp_path,
        )
        create_zip(
            output,
            manifest,
            dll,
            loc_dir,
            loc_files,
            legal_files=legal_files,
        )
        with zipfile.ZipFile(output) as zf:
            assert "QudJP/manifest.json" in zf.namelist()

    def test_zip_contains_dll(self, tmp_path: Path) -> None:
        """ZIP contains QudJP/Assemblies/QudJP.dll."""
        output, manifest, dll, loc_dir, loc_files, legal_files = self._make_inputs(
            tmp_path,
        )
        create_zip(
            output,
            manifest,
            dll,
            loc_dir,
            loc_files,
            legal_files=legal_files,
        )
        with zipfile.ZipFile(output) as zf:
            assert "QudJP/Assemblies/QudJP.dll" in zf.namelist()

    def test_zip_contains_localization_files(self, tmp_path: Path) -> None:
        """ZIP contains localization XML and JSON under QudJP/Localization/."""
        output, manifest, dll, loc_dir, loc_files, legal_files = self._make_inputs(
            tmp_path,
        )
        create_zip(
            output,
            manifest,
            dll,
            loc_dir,
            loc_files,
            legal_files=legal_files,
        )
        with zipfile.ZipFile(output) as zf:
            names = zf.namelist()
        assert "QudJP/Localization/Creatures.jp.xml" in names
        assert "QudJP/Localization/ui.json" in names

    def test_zip_root_is_qudjp(self, tmp_path: Path) -> None:
        """All ZIP entries start with QudJP/ prefix."""
        output, manifest, dll, loc_dir, loc_files, legal_files = self._make_inputs(
            tmp_path,
        )
        members = create_zip(
            output,
            manifest,
            dll,
            loc_dir,
            loc_files,
            legal_files=legal_files,
        )
        for member in members:
            assert member.startswith("QudJP/"), f"Bad prefix: {member}"

    def test_returns_member_list(self, tmp_path: Path) -> None:
        """create_zip returns the list of archive member names."""
        output, manifest, dll, loc_dir, loc_files, legal_files = self._make_inputs(
            tmp_path,
        )
        members = create_zip(
            output,
            manifest,
            dll,
            loc_dir,
            loc_files,
            legal_files=legal_files,
        )
        assert isinstance(members, list)
        assert len(members) == 6

    def test_creates_parent_dirs(self, tmp_path: Path) -> None:
        """Output parent directories are created if they do not exist."""
        _output, manifest, dll, loc_dir, loc_files, legal_files = self._make_inputs(
            tmp_path,
        )
        deep_output = tmp_path / "a" / "b" / "c" / "out.zip"
        create_zip(
            deep_output,
            manifest,
            dll,
            loc_dir,
            loc_files,
            legal_files=legal_files,
        )
        assert deep_output.exists()

    def test_zip_contains_font_files(self, tmp_path: Path) -> None:
        """ZIP contains font files from the Fonts/ directory."""
        output, manifest, dll, loc_dir, loc_files, legal_files = self._make_inputs(
            tmp_path,
        )
        fonts_dir = manifest.parent / "Fonts"
        fonts_dir.mkdir()
        (fonts_dir / "TestFont.otf").write_bytes(b"\x00\x01\x02\x03")
        (fonts_dir / "OFL.txt").write_text("SIL Open Font License", encoding="utf-8")

        members = create_zip(
            output,
            manifest,
            dll,
            loc_dir,
            loc_files,
            legal_files=legal_files,
        )
        with zipfile.ZipFile(output) as zf:
            names = zf.namelist()
        assert "QudJP/Fonts/TestFont.otf" in names
        assert "QudJP/Fonts/OFL.txt" in names
        assert "QudJP/Fonts/TestFont.otf" in members
        assert "QudJP/Fonts/OFL.txt" in members

    def test_zip_contains_compliance_files(self, tmp_path: Path) -> None:
        """ZIP contains LICENSE and NOTICE.md at the archive root."""
        output, manifest, dll, loc_dir, loc_files, legal_files = self._make_inputs(
            tmp_path,
        )

        members = create_zip(
            output,
            manifest,
            dll,
            loc_dir,
            loc_files,
            legal_files=legal_files,
        )
        with zipfile.ZipFile(output) as zf:
            names = zf.namelist()

        assert "QudJP/LICENSE" in names
        assert "QudJP/NOTICE.md" in names
        assert "QudJP/LICENSE" in members
        assert "QudJP/NOTICE.md" in members

    def test_zip_raises_when_missing_compliance_files(self, tmp_path: Path) -> None:
        """Missing compliance files raise FileNotFoundError when required."""
        output, manifest, dll, loc_dir, loc_files, legal_files = self._make_inputs(
            tmp_path,
        )
        legal_files[1].unlink()

        with pytest.raises(FileNotFoundError, match="Missing required compliance file"):
            create_zip(
                output,
                manifest,
                dll,
                loc_dir,
                loc_files,
                legal_files=legal_files,
            )


class TestBuildReleaseImport:
    """Smoke test: module imports without error."""

    def test_module_imports(self) -> None:
        """build_release module can be imported without side effects."""
        import scripts.build_release as br  # noqa: PLC0415

        assert callable(br.build_release)
        assert callable(br.main)

    def test_main_returns_int_on_error(self) -> None:
        """main() returns 1 when project root cannot be found."""
        with patch("scripts.build_release._find_project_root") as mock_root:
            mock_root.side_effect = FileNotFoundError("no root")
            result = main()
        assert isinstance(result, int)
        assert result == 1

    def test_build_release_propagates_file_not_found(self) -> None:
        """build_release raises FileNotFoundError when project root is missing."""
        with patch("scripts.build_release._find_project_root") as mock_root:
            mock_root.side_effect = FileNotFoundError("no root")
            with pytest.raises(FileNotFoundError):
                build_release()

    def test_build_dll_raises_on_missing_dll(self, tmp_path: Path) -> None:
        """build_dll raises FileNotFoundError when DLL is absent after build."""
        mock_result = MagicMock(returncode=0)
        with (
            patch("scripts.build_release.subprocess.run", return_value=mock_result),
            pytest.raises(FileNotFoundError, match="DLL not found after build"),
        ):
            build_dll(tmp_path)
