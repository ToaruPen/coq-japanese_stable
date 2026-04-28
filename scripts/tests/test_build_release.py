"""Tests for the build_release module."""

import json
import zipfile
from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

from scripts.build_release import (
    RELEASE_VERSION,
    build_dll,
    build_release,
    collect_localization_files,
    create_zip,
    main,
    read_preview_image_path,
    read_version,
)

PROJECT_ROOT = Path(__file__).resolve().parents[2]
LOCALIZATION_DOC_NAMES = ("AGENTS.md", "CLAUDE.md", "README.md")


class TestReadVersion:
    """Tests for read_version."""

    def test_reads_version_from_manifest(self, tmp_path: Path) -> None:
        """Version string is extracted from the Version field."""
        manifest = tmp_path / "manifest.json"
        manifest.write_text(json.dumps({"Version": "1.2.3"}), encoding="utf-8")
        assert read_version(manifest) == "1.2.3"

    def test_rejects_dev_version_suffix(self, tmp_path: Path) -> None:
        """Version strings must be simple semver accepted by the game."""
        manifest = tmp_path / "manifest.json"
        manifest.write_text(json.dumps({"Version": "0.1.0-dev"}), encoding="utf-8")
        with pytest.raises(ValueError, match="simple semver") as exc_info:
            read_version(manifest)
        assert str(manifest) in str(exc_info.value)
        assert "0.1.0-dev" in str(exc_info.value)

    def test_rejects_non_semver_version(self, tmp_path: Path) -> None:
        """Non-semver version strings are rejected before release packaging."""
        manifest = tmp_path / "manifest.json"
        manifest.write_text(json.dumps({"Version": "v1"}), encoding="utf-8")
        with pytest.raises(ValueError, match="simple semver"):
            read_version(manifest)

    def test_missing_manifest_raises(self, tmp_path: Path) -> None:
        """FileNotFoundError is raised when manifest.json does not exist."""
        with pytest.raises(FileNotFoundError, match=r"manifest\.json not found"):
            read_version(tmp_path / "nonexistent.json")

    def test_missing_version_key_raises(self, tmp_path: Path) -> None:
        """ValueError is raised when the Version key is absent."""
        manifest = tmp_path / "manifest.json"
        manifest.write_text(json.dumps({"Id": "QudJP"}), encoding="utf-8")
        with pytest.raises(ValueError, match="Version field is missing"):
            read_version(manifest)

    def test_empty_version_raises(self, tmp_path: Path) -> None:
        """ValueError is raised when the Version field is empty."""
        manifest = tmp_path / "manifest.json"
        manifest.write_text(json.dumps({"Version": ""}), encoding="utf-8")
        with pytest.raises(ValueError, match="Version field is empty"):
            read_version(manifest)

    def test_whitespace_only_version_raises(self, tmp_path: Path) -> None:
        """Whitespace-only Version is treated as empty."""
        manifest = tmp_path / "manifest.json"
        manifest.write_text(json.dumps({"Version": "   "}), encoding="utf-8")
        with pytest.raises(ValueError, match="Version field is empty"):
            read_version(manifest)


class TestProjectManifest:
    """Tests for the checked-in release manifest."""

    def test_manifest_version_is_release_semver(self) -> None:
        """Checked-in manifest uses the current release setup version."""
        manifest = PROJECT_ROOT / "Mods" / "QudJP" / "manifest.json"
        assert read_version(manifest) == RELEASE_VERSION

    def test_preview_image_points_to_checked_in_asset(self) -> None:
        """Checked-in manifest points at the committed Workshop preview asset."""
        manifest = PROJECT_ROOT / "Mods" / "QudJP" / "manifest.json"
        data = json.loads(manifest.read_text(encoding="utf-8"))
        assert data["PreviewImage"] == "preview.png"
        assert read_preview_image_path(manifest) == PROJECT_ROOT / "Mods" / "QudJP" / "preview.png"


class TestReadPreviewImagePath:
    """Tests for manifest PreviewImage handling."""

    def test_empty_preview_image_returns_none(self, tmp_path: Path) -> None:
        """Unset PreviewImage remains optional for test fixtures."""
        manifest = tmp_path / "manifest.json"
        manifest.write_text(json.dumps({"PreviewImage": ""}), encoding="utf-8")
        assert read_preview_image_path(manifest) is None

    def test_reads_existing_preview_image(self, tmp_path: Path) -> None:
        """A relative mod-local PreviewImage resolves to an existing file."""
        manifest = tmp_path / "manifest.json"
        preview = tmp_path / "preview.png"
        preview.write_bytes(b"png")
        manifest.write_text(json.dumps({"PreviewImage": "preview.png"}), encoding="utf-8")
        assert read_preview_image_path(manifest) == preview

    def test_rejects_escaping_preview_image_path(self, tmp_path: Path) -> None:
        """PreviewImage cannot escape the mod directory."""
        manifest = tmp_path / "manifest.json"
        manifest.write_text(json.dumps({"PreviewImage": "../preview.png"}), encoding="utf-8")
        with pytest.raises(ValueError, match="relative mod-local path"):
            read_preview_image_path(manifest)

    def test_rejects_absolute_preview_image_path(self, tmp_path: Path) -> None:
        """PreviewImage must be relative to the mod directory."""
        manifest = tmp_path / "manifest.json"
        preview = tmp_path / "preview.png"
        manifest.write_text(json.dumps({"PreviewImage": str(preview)}), encoding="utf-8")
        with pytest.raises(ValueError, match="relative mod-local path"):
            read_preview_image_path(manifest)

    def test_missing_preview_image_raises(self, tmp_path: Path) -> None:
        """A non-empty PreviewImage must point at a real file."""
        manifest = tmp_path / "manifest.json"
        manifest.write_text(json.dumps({"PreviewImage": "preview.png"}), encoding="utf-8")
        with pytest.raises(FileNotFoundError, match="PreviewImage file not found"):
            read_preview_image_path(manifest)


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

    def test_collects_txt_files(self, tmp_path: Path) -> None:
        """TXT localization assets are collected recursively."""
        loc = tmp_path / "Localization"
        corpus = loc / "Corpus"
        corpus.mkdir(parents=True)
        (loc / "Text.jp.txt").write_text("main text", encoding="utf-8")
        (corpus / "Thought-Forms-excerpt.jp.txt").write_text(
            "corpus text",
            encoding="utf-8",
        )

        files = collect_localization_files(loc)
        names = {f.name for f in files}
        assert "Text.jp.txt" in names
        assert "Thought-Forms-excerpt.jp.txt" in names

    def test_excludes_docs_and_unrecognized_files(self, tmp_path: Path) -> None:
        """LLM-facing docs and unrecognized files are not collected."""
        loc = tmp_path / "Localization"
        loc.mkdir()
        for doc_name in LOCALIZATION_DOC_NAMES:
            (loc / doc_name).write_text("# docs", encoding="utf-8")
        (loc / "data.csv").write_text("a,b", encoding="utf-8")

        files = collect_localization_files(loc)
        names = {f.name for f in files}
        for doc_name in LOCALIZATION_DOC_NAMES:
            assert doc_name not in names
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

    def test_zip_contains_collected_txt_localization_files_without_docs(
        self,
        tmp_path: Path,
    ) -> None:
        """ZIP uses collected TXT localization assets without LLM-facing docs."""
        output, manifest, dll, loc_dir, _loc_files, legal_files = self._make_inputs(
            tmp_path,
        )
        (loc_dir / "Text.jp.txt").write_text("main text", encoding="utf-8")
        for doc_name in LOCALIZATION_DOC_NAMES:
            (loc_dir / doc_name).write_text("# docs", encoding="utf-8")
        loc_files = collect_localization_files(loc_dir)

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

        assert "QudJP/Localization/Text.jp.txt" in names
        for doc_name in LOCALIZATION_DOC_NAMES:
            assert f"QudJP/Localization/{doc_name}" not in names

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

    def test_zip_contains_preview_image_when_manifest_references_it(self, tmp_path: Path) -> None:
        """ZIP contains the Workshop preview image referenced by manifest.json."""
        output, manifest, dll, loc_dir, loc_files, legal_files = self._make_inputs(
            tmp_path,
        )
        preview = manifest.parent / "preview.png"
        preview.write_bytes(b"png")
        manifest.write_text(json.dumps({"Version": "0.1.0", "PreviewImage": "preview.png"}), encoding="utf-8")

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

        assert "QudJP/preview.png" in names
        assert "QudJP/preview.png" in members

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

    def test_build_release_names_zip_from_manifest_version(self, tmp_path: Path) -> None:
        """Release ZIP output path includes the manifest version."""
        mod_dir = tmp_path / "Mods" / "QudJP"
        loc_dir = mod_dir / "Localization"
        mod_dir.mkdir(parents=True)
        loc_dir.mkdir()
        manifest = mod_dir / "manifest.json"
        manifest.write_text(json.dumps({"Version": "1.2.3"}), encoding="utf-8")
        dll = mod_dir / "Assemblies" / "QudJP.dll"
        dll.parent.mkdir()
        dll.write_bytes(b"dll")
        license_file = tmp_path / "LICENSE"
        license_file.write_text("license", encoding="utf-8")
        notice_file = tmp_path / "NOTICE.md"
        notice_file.write_text("notice", encoding="utf-8")

        with (
            patch("scripts.build_release._find_project_root", return_value=tmp_path),
            patch("scripts.build_release.build_dll", return_value=dll),
            patch("scripts.build_release.collect_localization_files", return_value=[]),
            patch("scripts.build_release.create_zip", return_value=[]) as create_zip_mock,
        ):
            build_release()

        create_zip_mock.assert_called_once()
        assert create_zip_mock.call_args.args[0] == tmp_path / "dist" / "QudJP-v1.2.3.zip"

    def test_build_dll_raises_on_missing_dll(self, tmp_path: Path) -> None:
        """build_dll raises FileNotFoundError when DLL is absent after build."""
        mock_result = MagicMock(returncode=0)
        with (
            patch("scripts.build_release.subprocess.run", return_value=mock_result),
            pytest.raises(FileNotFoundError, match="DLL not found after build"),
        ):
            build_dll(tmp_path)
