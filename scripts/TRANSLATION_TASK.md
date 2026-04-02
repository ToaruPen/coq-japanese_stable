# Markov Corpus Translation Task

## What to do

1. Read `scripts/corpus_en_for_translation.json` — 9,371 English sentences as `[{id, en}]`
2. Read `scripts/translation_glossary.txt` — mandatory term mappings and the single source of truth
3. Translate every sentence to Japanese
4. Write output to `scripts/corpus_ja_translated.json` as `[{id, ja}]`

## Mandatory Glossary (from translation_glossary.txt)

Use `scripts/translation_glossary.txt` as the only canonical glossary.
Do not duplicate, rewrite, or partially restate glossary entries anywhere else in the workflow.

## Translation Rules

1. Match the original register — literary stays literary, scientific stays analytical, aphorisms stay pithy
2. Each English sentence = one Japanese sentence (do NOT merge or split)
3. End sentences with ASCII period `.` NOT `。`
4. Transliterate proper names to katakana (use ・ for multi-part names)
5. Keep Qud lore concepts faithful — do NOT domesticate to real-world equivalents
6. Output ONLY the JSON array `[{id, ja}]`, no commentary
7. Process ALL sentences — do not skip any
