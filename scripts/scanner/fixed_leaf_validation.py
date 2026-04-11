"""Validation helpers for proposed fixed-leaf scanner additions."""

from __future__ import annotations

from dataclasses import dataclass
from enum import StrEnum

from scripts.scanner.inventory import (
    DestinationDictionary,
    FixedLeafRejectionReason,
    InventoryDraft,
    InventorySite,
    default_destination_dictionary_for_route,
)

_MIN_DUPLICATE_SITE_COUNT = 2
_DUPLICATE_RULE = "Use one exact fixed-leaf key per addition set; merge or reject duplicates before import."
_BROAD_RULE = "Reject non-proven fixed-leaf routes and keep this route owner-routed."
_SCOPED_RULE = (
    "Popup and message-log leaves should use a scoped dictionary when the route already defines a narrower owner."
)
_GLOBAL_RULE = "Shared exact leaves may use the global_flat dictionary when no narrower route-specific home applies."


class FixedLeafFailureClass(StrEnum):
    """Failure classes surfaced by fixed-leaf validation."""

    DUPLICATE_KEY = "duplicate_key"
    BROAD_ENTRY = "broad_entry"
    WRONG_DESTINATION = "wrong_destination"


@dataclass(frozen=True, slots=True)
class FixedLeafValidationIssue:
    """One deterministic validation issue."""

    failure_class: FixedLeafFailureClass
    candidate_ids: tuple[str, ...]
    key: str | None
    source_route: str | None
    expected_rule: str
    actual_destination: DestinationDictionary | None = None
    expected_destination: DestinationDictionary | None = None
    rejection_reason: FixedLeafRejectionReason | None = None
    failure_factors: tuple[str, ...] = ()


@dataclass(frozen=True, slots=True)
class FixedLeafValidationReport:
    """Validation summary for fixed-leaf candidate additions."""

    candidate_count: int
    issues: tuple[FixedLeafValidationIssue, ...]

    @property
    def is_valid(self) -> bool:
        """Return whether the candidate set passed validation."""
        return not self.issues


def validate_fixed_leaf_inventory(draft: InventoryDraft) -> FixedLeafValidationReport:
    """Validate proposed fixed-leaf additions in a candidate inventory."""
    candidate_sites = tuple(
        sorted(
            (site for site in draft.sites if site.destination_dictionary is not None),
            key=lambda site: (site.id, site.key or "", site.source_route or ""),
        )
    )
    issues = [*(_site_issue(site) for site in candidate_sites), *_duplicate_issues(candidate_sites)]
    filtered_issues = tuple(issue for issue in issues if issue is not None)
    return FixedLeafValidationReport(
        candidate_count=len(candidate_sites),
        issues=tuple(sorted(filtered_issues, key=_issue_sort_key)),
    )


def render_fixed_leaf_validation_report(report: FixedLeafValidationReport) -> str:
    """Render a deterministic human-readable validation report."""
    if report.is_valid:
        return f"Fixed-leaf validation passed: {report.candidate_count} candidate(s) checked, 0 issue(s).\n"

    lines = [
        f"Fixed-leaf validation failed: {len(report.issues)} issue(s) across {report.candidate_count} candidate(s)."
    ]
    for issue in report.issues:
        parts = [
            f"ERROR [{issue.failure_class.value}]",
            f"candidates={', '.join(issue.candidate_ids)}",
        ]
        if issue.key is not None:
            parts.append(f"key={issue.key!r}")
        if issue.source_route is not None:
            parts.append(f"route={issue.source_route}")
        if issue.rejection_reason is not None:
            parts.append(f"rejection={issue.rejection_reason.value}")
        if issue.failure_factors:
            parts.append(f"factors={', '.join(issue.failure_factors)}")
        if issue.actual_destination is not None:
            parts.append(f"actual={issue.actual_destination.value}")
        if issue.expected_destination is not None:
            parts.append(f"expected={issue.expected_destination.value}")
        parts.append(f"rule={issue.expected_rule}")
        lines.append("  " + "; ".join(parts))
    return "\n".join(lines) + "\n"


def _site_issue(site: InventorySite) -> FixedLeafValidationIssue | None:
    """Validate one candidate site in isolation."""
    if not site.is_proven_fixed_leaf:
        return FixedLeafValidationIssue(
            failure_class=FixedLeafFailureClass.BROAD_ENTRY,
            candidate_ids=(site.id,),
            key=site.key,
            source_route=site.source_route,
            actual_destination=site.destination_dictionary,
            rejection_reason=site.rejection_reason,
            failure_factors=_failure_factors(site),
            expected_rule=_BROAD_RULE,
        )

    expected_destination, expected_rule = _expected_destination(site)
    if site.destination_dictionary is expected_destination:
        return None
    return FixedLeafValidationIssue(
        failure_class=FixedLeafFailureClass.WRONG_DESTINATION,
        candidate_ids=(site.id,),
        key=site.key,
        source_route=site.source_route,
        actual_destination=site.destination_dictionary,
        expected_destination=expected_destination,
        expected_rule=expected_rule,
    )


def _duplicate_issues(candidate_sites: tuple[InventorySite, ...]) -> list[FixedLeafValidationIssue]:
    """Return duplicate-key failures for proposed additions."""
    keyed_sites: dict[str, list[InventorySite]] = {}
    for site in candidate_sites:
        if site.key is None:
            continue
        keyed_sites.setdefault(site.key, []).append(site)

    duplicates: list[FixedLeafValidationIssue] = []
    for key, sites in sorted(keyed_sites.items()):
        if len(sites) < _MIN_DUPLICATE_SITE_COUNT:
            continue
        duplicates.append(
            FixedLeafValidationIssue(
                failure_class=FixedLeafFailureClass.DUPLICATE_KEY,
                candidate_ids=tuple(sorted(site.id for site in sites)),
                key=key,
                source_route=None,
                expected_rule=_DUPLICATE_RULE,
            )
        )
    return duplicates


def _expected_destination(site: InventorySite) -> tuple[DestinationDictionary, str]:
    """Return the expected dictionary tier and the rule that chose it."""
    if (
        default_destination_dictionary_for_route(source_route=site.source_route, sink=site.sink)
        is DestinationDictionary.SCOPED
    ):
        return DestinationDictionary.SCOPED, _SCOPED_RULE
    return DestinationDictionary.GLOBAL_FLAT, _GLOBAL_RULE


def _failure_factors(site: InventorySite) -> tuple[str, ...]:
    """Return concrete failure factors that explain why a route is too broad."""
    factors: list[str] = []
    if site.ownership_class is not None:
        factors.append(f"ownership={site.ownership_class.value}")
    if site.needs_review:
        factors.append("needs_review")
    if site.needs_runtime:
        factors.append("needs_runtime")
    return tuple(factors)


def _issue_sort_key(issue: FixedLeafValidationIssue) -> tuple[str, tuple[str, ...], str]:
    """Return a stable sort key for rendered issues."""
    return issue.failure_class.value, issue.candidate_ids, issue.key or ""
