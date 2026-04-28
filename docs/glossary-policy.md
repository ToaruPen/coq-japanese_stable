# Glossary Policy

`docs/glossary.csv` is the canonical source for stable player-facing proper nouns and fixed terminology. Before adding or changing a translation, check the glossary first; if a row is `confirmed` or `approved`, use that Japanese form unless fresh tests or runtime evidence prove the row is wrong.

Status meanings:

| Status | Meaning |
| --- | --- |
| `draft` | Candidate term. Useful for review, but not authoritative. |
| `confirmed` | Reviewed term with credible source evidence. New work should follow it unless conflicting shipped assets or runtime evidence are found. |
| `approved` | Release-authoritative term. The row matches shipped localization assets, has enough source evidence in `Notes`, and may be consumed by deterministic checks or scripts. |

Promote a row to `approved` only when the Japanese form is already implemented in the relevant shipped XML/JSON assets or the same PR implements it, `Notes` records the source/evidence, and known conflicting visible forms have either been fixed or explicitly deferred in the issue. Broad cross-file consistency enforcement belongs to the #409 CI gate; until then, `scripts/tokenize_corpus.py` is the live consumer for `confirmed` and `approved` Japanese terms.
