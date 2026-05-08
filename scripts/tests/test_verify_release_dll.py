from __future__ import annotations

import zipfile
from typing import TYPE_CHECKING

from scripts.verify_release_dll import verify_release_dll

if TYPE_CHECKING:
    from pathlib import Path

_REQUIRED_MARKER_PAYLOAD = b"\0".join(
    [
        b"Unity.TextMeshPro",
        b"TextMeshProUguiFontPatch",
        b"TmpInputFieldFontPatch",
        b"InventoryLineFontFixer",
        b"DelayedInventoryLineRepairScheduler",
        b"ShouldPreserveActiveReplacementForTests",
    ],
)

_FORBIDDEN_VERBOSE_PROBE_MARKERS = [
    b"[QudJP] NewProbe/v1:",
    b"[QudJP] FutureProbe/v1:",
    b"[QudJP] SinkObserve/v1:",
    b"[QudJP] Translator: missing key",
    b"no pattern for",
]


def test_verify_release_dll_accepts_required_markers(tmp_path: Path) -> None:
    """Accept a DLL that contains all required runtime markers."""
    dll = tmp_path / "QudJP.dll"
    dll.write_bytes(_REQUIRED_MARKER_PAYLOAD)

    assert verify_release_dll(dll) == []


def test_verify_release_dll_reports_missing_markers(tmp_path: Path) -> None:
    """Report every missing required runtime marker."""
    dll = tmp_path / "QudJP.dll"
    dll.write_bytes(b"Unity.TextMeshPro")

    assert verify_release_dll(dll) == [
        "TextMeshProUguiFontPatch",
        "TmpInputFieldFontPatch",
        "InventoryLineFontFixer",
        "DelayedInventoryLineRepairScheduler",
        "ShouldPreserveActiveReplacementForTests",
    ]


def test_verify_release_dll_reports_dev_only_probe_markers(tmp_path: Path) -> None:
    """Reject release DLLs that contain dev-only probe patch markers."""
    dll = tmp_path / "QudJP.dll"
    dll.write_bytes(
        _REQUIRED_MARKER_PAYLOAD
        + b"\0"
        + b"\0".join(
            [
                b"BaseLineWithTooltipStartTooltipPatch",
                b"SelectableTextMenuItemProbePatch",
            ],
        ),
    )

    assert verify_release_dll(dll) == [
        "forbidden dev marker: BaseLineWithTooltipStartTooltipPatch",
        "forbidden dev marker: SelectableTextMenuItemProbePatch",
    ]


def test_verify_release_dll_reports_verbose_probe_log_markers(tmp_path: Path) -> None:
    """Reject release DLLs that contain direct verbose probe log markers."""
    dll = tmp_path / "QudJP.dll"
    dll.write_bytes(
        _REQUIRED_MARKER_PAYLOAD
        + b"\0"
        + b"\0".join(_FORBIDDEN_VERBOSE_PROBE_MARKERS),
    )

    assert verify_release_dll(dll) == [
        "forbidden dev marker: [QudJP] NewProbe/v1:",
        "forbidden dev marker: [QudJP] FutureProbe/v1:",
        "forbidden dev marker: [QudJP] SinkObserve/v1:",
        "forbidden dev marker: [QudJP] Translator: missing key",
        "forbidden dev marker: no pattern for",
    ]


def test_verify_release_dll_reports_verbose_probe_fragments(tmp_path: Path) -> None:
    """Reject release DLLs that retain split verbose probe marker fragments."""
    dll = tmp_path / "QudJP.dll"
    dll.write_bytes(
        _REQUIRED_MARKER_PAYLOAD
        + b"\0"
        + b"\0".join(
            [
                b"DynamicTextProbe/v1",
                b"FinalOutputProbe/v1",
                b"SinkObserve/v1",
            ],
        ),
    )

    assert verify_release_dll(dll) == [
        "forbidden dev marker: DynamicTextProbe/",
        "forbidden dev marker: FinalOutputProbe/",
        "forbidden dev marker: SinkObserve/",
    ]


def test_verify_release_dll_reports_verbose_probe_log_markers_in_zip(
    tmp_path: Path,
) -> None:
    """Reject forbidden verbose probe markers inside release ZIP DLLs."""
    release_zip = tmp_path / "QudJP-v0.0.0.zip"
    with zipfile.ZipFile(release_zip, "w") as archive:
        archive.writestr(
            "QudJP/Assemblies/QudJP.dll",
            _REQUIRED_MARKER_PAYLOAD
            + b"\0"
            + b"[QudJP] SinkObserve/v1:",
        )

    assert verify_release_dll(release_zip) == [
        "forbidden dev marker: [QudJP] SinkObserve/v1:",
    ]


def test_verify_release_dll_reports_utf16_verbose_probe_log_markers(
    tmp_path: Path,
) -> None:
    """Reject .NET metadata strings encoded as UTF-16LE."""
    dll = tmp_path / "QudJP.dll"
    dll.write_bytes(
        _REQUIRED_MARKER_PAYLOAD
        + b"\0"
        + "[QudJP] NewProbe/v1:".encode("utf-16le"),
    )

    assert verify_release_dll(dll) == [
        "forbidden dev marker: [QudJP] NewProbe/v1:",
    ]


def test_verify_release_dll_reads_release_zip(tmp_path: Path) -> None:
    """Read QudJP.dll from a release ZIP before checking markers."""
    release_zip = tmp_path / "QudJP-v0.0.0.zip"
    with zipfile.ZipFile(release_zip, "w") as archive:
        archive.writestr(
            "QudJP/Assemblies/QudJP.dll",
            _REQUIRED_MARKER_PAYLOAD,
        )

    assert verify_release_dll(release_zip) == []


def test_verify_release_dll_reads_zip_content_without_zip_suffix(tmp_path: Path) -> None:
    """Detect ZIP archives by content so callers are not tied to file suffixes."""
    release_zip = tmp_path / "QudJP-v0.0.0"
    with zipfile.ZipFile(release_zip, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr(
            "QudJP/Assemblies/QudJP.dll",
            _REQUIRED_MARKER_PAYLOAD,
        )

    assert verify_release_dll(release_zip) == []
