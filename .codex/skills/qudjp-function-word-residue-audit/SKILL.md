---
name: qudjp-function-word-residue-audit
description: Use in QudJP when auditing or fixing English function-word residue in localized runtime text, including articles, possessive pronouns, prepositions, direction phrases, bracketed display states, and stale tests that incorrectly preserve those tokens.
---

# QudJP Function-Word Residue Audit

Use this skill before broad fixes for English residue such as `a`, `an`, `the`,
`your`, `its`, `their`, `his`, `her`, `to the east`, `from`, `with`, `of`, or
display-name states such as `[swimming]`.

The goal is to avoid local fallback patches and stale tests that preserve the
wrong behavior.

## Workflow

1. Start with fresh runtime evidence when available.
   - Run `scripts/triage_untranslated.py` on the latest `Player.log`.
   - Treat log evidence as route proof, not static completeness.

2. Generate a static shelf.
   - Use:

```bash
just function-word-residue-audit
```

   - For a custom decompiled root or output:

```bash
just function-word-residue-audit \
  /Users/toarupen/dev/coq-decompiled_stable \
  Mods/QudJP/Assemblies/QudJP.Tests \
  /tmp/qudjp-function-word-residue-audit.json \
  150
```

3. Classify each hit by route family.
   - Use the audit JSON fields first:
     - `classification` separates owner-route candidates, stale tests,
       fixtures, and intentional English.
     - `risk` is a prioritization hint, not proof of a live regression.
     - `owner_route_hint` names the likely layer to inspect next.
   - `Does(...)` / `MessageFrame`: prefer `DoesVerbRouteTranslator`,
     `MessageFrameTranslator`, or frame placeholder fixes.
   - `XDidY` / `DidX*`: fix entity slots, `Extra`, `Preposition`, owner labels,
     and direction suffixes at the owner route.
   - `Grammar.MakePossessive` / `poss(...)`: strip or translate the owner phrase
     before appending Japanese particles.
   - `GetDisplayName` / `DisplayName` / `SetText`: fix display-name composition
     and state suffix handling, not broad UI sink fallback.
   - `Popup` / `AddPlayerMessage` / `EmitMessage`: prefer producer or owner
     route translation before adding dictionary leaves.

4. Audit tests before implementation.
   - Search scanner output for `domain=test`.
   - Do not preserve expected strings such as `youを`, `the ...に`,
     `to the east`, or `[swimming]` unless a route-specific allow rule exists.
   - Keep intentional English such as hotkeys, stat abbreviations, markup tags,
     proper nouns, and quoted authored English out of the fix queue.

5. Fix by ownership layer.
   - Prefer `{tN}` placeholders or route-specific normalizers when a capture is
     a generated noun phrase.
   - Do not add mixed-output sink patterns such as `^Your (.+?)に...$` unless
     runtime proof shows no owner route can handle it.
   - Keep fallback behavior explicit and tested.

6. Verify.
   - Run focused L1/L2 tests for changed route families.
   - Run `uv run python -m pytest scripts/tests/test_audit_function_word_residue.py`
     when changing the audit script.
   - Run `just function-word-residue-audit` again and compare high-confidence
     hits before claiming closure.

## Output Expectations

When reporting results, include:

- confirmed runtime regressions,
- static high-risk route families,
- classified fix candidates vs intentional/fixture hits,
- stale or weak tests that need expectation updates,
- intentional English allow cases,
- the next owner-layer fix target.
