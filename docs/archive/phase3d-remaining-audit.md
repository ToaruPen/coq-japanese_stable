# Phase 3d Remaining Audit: 96 Unresolved Sites

**Date**: 2026-03-25
**Scope**: Full source-trace audit of all 96 remaining `unresolved` sites in `docs/candidate-inventory.json`
**Method**: Decompiled source tracing + existing QudJP patch verification

## Executive Summary

Of 96 remaining unresolved sites, **only 16 are genuinely runtime-dependent**. The rest are already covered by existing patches, handled by the UITextSkinTranslationPatch sink, or should be excluded.

| Classification | Count | Action |
|---------------|-------|--------|
| EXCLUDED | 22 | Reclassify — no translation needed |
| COVERED_BY_EXISTING | 16 | Reclassify — upstream Harmony patch handles |
| COVERED_BY_SINK | 27 | UITextSkinTranslationPatch handles (sink-dependent) |
| NEEDS_DICTIONARY | 7 | Add ~30 dictionary entries |
| NEEDS_PRODUCER_PATCH | 4 | 1 active (zone name), 3 unreachable in v2.0.4 |
| TRULY_RUNTIME | 16 | Dynamic composition, sink-dependent best-effort |
| Dead code (registered) | 4 | No action needed in current game version |
| **Total** | **96** | |

## Sink Cutover Impact (#103)

**43 sites depend on sinks** (27 COVERED_BY_SINK + 16 TRULY_RUNTIME). Before removing UITextSkinTranslationPatch / MessageLogPatch:

### Required pre-cutover producer patches

1. CharGen framework producer — translate FrameworkDataElement.Description/Title (10 sites)
2. Ability/Effect DisplayName producer (2 sites)
3. TinkerData producer — translate DisplayName/Description (3 sites)
4. Zone name translation — PlayerStatusBar.cs L505 (1 site)
5. LeftSideCategory dictionary pre-population (4 sites)

### Key Findings

1. **Pettable TranslatablePartFields entry was dead code** — PetResponse is a tag, not part field. Covered by XML merge tags.
2. **3 NEEDS_PRODUCER_PATCH sites are unreachable** — Reconstitution.DropMessage, Spawner.SpawnMessage, SwapOnUse.Message have no XML overrides in v2.0.4.
3. **7 sites resolved with ~30 dictionary entries** — ending causes, loading status, Sheva launch text.
4. **3 missing verbs added to verbs.ja.json** — "slip", "collect", "piece" (rifle was already present).
