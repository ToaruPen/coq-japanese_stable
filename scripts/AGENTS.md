# scripts/ — Python Tooling

Translation tooling, validation scripts, and their tests live here.

## Runtime Requirements

- Python 3.12 or newer
- Dependencies declared in `pyproject.toml` (project root)

## Linting

Ruff is the sole linter and formatter. Config lives in `pyproject.toml`:

```toml
[tool.ruff.lint]
select = ["ALL"]

[tool.ruff.lint.mccabe]
max-complexity = 10
```

Run before committing:

```bash
ruff check scripts/
ruff format scripts/
```

All rules are enabled. Disable individual rules only with an inline comment
and a justification:

```python
result = some_func()  # noqa: SIM117 -- nested with needed for clarity
```

## Type Hints

Required on every public function and method:

```python
def translate_text(source: str, locale: str = "ja") -> str:
    ...

def load_xml(path: Path) -> ET.ElementTree:
    ...
```

Private helpers (leading underscore) should also have hints where practical.

## Docstrings

Google style. Required on all public functions, classes, and modules:

```python
def sync_translations(source_dir: Path, output_dir: Path) -> int:
    """Sync XML translation files from source to output directory.

    Args:
        source_dir: Directory containing source XML files.
        output_dir: Destination directory for translated files.

    Returns:
        Number of files written.

    Raises:
        FileNotFoundError: If source_dir does not exist.
    """
```

## Script Naming

`verb_noun.py` pattern. The verb describes the action; the noun describes
the target:

```
sync_translations.py    # copies/merges XML files
check_encoding.py       # validates UTF-8 / BOM / mojibake
validate_ids.py         # checks blueprint/conversation IDs against game data
extract_strings.py      # pulls translatable strings from game XML
```

No generic names like `utils.py`, `helpers.py`, or `main.py`.

## Import Ordering

Enforced by Ruff (isort rules). Three groups, separated by blank lines:

```python
# 1. stdlib
import re
from pathlib import Path

# 2. third-party
import lxml.etree as ET

# 3. local
from scripts.common import load_config
```

## Tests

Location: `scripts/tests/`
Runner: pytest

```bash
pytest scripts/tests/
pytest scripts/tests/ -v          # verbose
pytest scripts/tests/ -k encoding # filter by name
```

Test file naming mirrors the script it covers:

```
scripts/check_encoding.py   ->  scripts/tests/test_check_encoding.py
scripts/sync_translations.py -> scripts/tests/test_sync_translations.py
```

Each test file starts with a module docstring explaining what it covers.

## Error Handling

Fail fast with actionable messages. Include: cause, affected file/line,
and a suggested fix where possible:

```python
msg = (
    f"BOM detected in {path}. "
    "Save as UTF-8 without BOM (VS Code: bottom-right encoding selector)."
)
raise ValueError(msg)
```

Never silently swallow exceptions. Never use bare `except:`.

## Common Patterns

### Reading XML safely

```python
from pathlib import Path
import lxml.etree as ET

def load_xml(path: Path) -> ET.ElementTree:
    """Load an XML file, raising on parse error."""
    with path.open("rb") as fh:
        return ET.parse(fh)
```

### Walking translation files

```python
def iter_jp_xml(root: Path):
    """Yield all *.jp.xml files under root."""
    yield from root.rglob("*.jp.xml")
```
