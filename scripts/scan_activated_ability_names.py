from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Final

SCHEMA_VERSION: Final = "1.0"
TARGET_METHODS: Final = frozenset(
    {
        "AddActivatedAbility",
        "AddDynamicCommand",
        "AddAbility",
        "AddMyActivatedAbility",
        "SetActivatedAbilityDisplayName",
        "SetMyActivatedAbilityDisplayName",
    }
)
DISPLAY_NAME_SOURCE_START_RE: Final = re.compile(
    r"(?:[A-Za-z_][A-Za-z0-9_]*\s*(?:\.|\?\.)\s*)*"
    r"Get[A-Za-z0-9_]*DisplayName[A-Za-z0-9_]*\s*\(",
    re.DOTALL,
)


@dataclass(frozen=True, slots=True)
class ActivatedAbilityNameItem:
    """Inventory row for one activated ability display-name callsite."""

    file: str
    line: int
    method: str
    classification: str
    name: str | None = None
    expression: str | None = None

    def to_dict(self) -> dict[str, object]:
        """Serialize optional fields only when the classification uses them."""
        item: dict[str, object] = {
            "file": self.file,
            "line": self.line,
            "method": self.method,
            "classification": self.classification,
        }
        if self.name is not None:
            item["name"] = self.name
        if self.expression is not None:
            item["expression"] = self.expression
        return item


@dataclass(frozen=True, slots=True)
class _Callsite:
    method: str
    line: int
    display_name_argument: str


@dataclass(frozen=True, slots=True)
class _Argument:
    name: str | None
    expression: str


def scan_source_root(source_root: Path) -> list[ActivatedAbilityNameItem]:
    """Scan `.cs` files below source_root for activated ability display-name callsites."""
    items: list[ActivatedAbilityNameItem] = []
    for source_path in _iter_cs_files(source_root):
        relative_path = source_path.relative_to(source_root).as_posix()
        text = source_path.read_text(encoding="utf-8")
        items.extend(_classify_callsite(relative_path, callsite) for callsite in _find_callsites(text))
    return sorted(items, key=lambda item: (item.file, item.line, item.method))


def write_inventory(source_root: Path, output_path: Path) -> None:
    """Write the activated ability display-name inventory JSON."""
    payload = {
        "schema_version": SCHEMA_VERSION,
        "source_root": str(source_root.resolve()),
        "items": [item.to_dict() for item in scan_source_root(source_root)],
    }
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _iter_cs_files(source_root: Path) -> list[Path]:
    return sorted(path for path in source_root.rglob("*.cs") if path.is_file())


def _find_callsites(text: str) -> list[_Callsite]:
    callsites: list[_Callsite] = []
    index = 0
    while index < len(text):
        skipped = _skip_csharp_trivia_or_literal(text, index)
        if skipped != index:
            index = skipped
            continue

        if not _is_identifier_start(text[index]):
            index += 1
            continue

        identifier_start = index
        index += 1
        while index < len(text) and _is_identifier_part(text[index]):
            index += 1

        parsed_callsite = _extract_callsite_at_identifier(text, text[identifier_start:index], identifier_start, index)
        if parsed_callsite is not None:
            callsite, next_index = parsed_callsite
            callsites.append(callsite)
            index = next_index

    return callsites


def _extract_callsite_at_identifier(
    text: str,
    identifier: str,
    identifier_start: int,
    identifier_end: int,
) -> tuple[_Callsite, int] | None:
    display_name_assignment = _extract_display_name_assignment(text, identifier, identifier_start, identifier_end)
    if display_name_assignment is not None:
        expression, assignment_end = display_name_assignment
        return (
            _Callsite(
                method="DisplayNameAssignment",
                line=text.count("\n", 0, identifier_start) + 1,
                display_name_argument=expression,
            ),
            assignment_end,
        )

    if identifier not in TARGET_METHODS:
        return None
    if identifier == "AddAbility" and not _has_activated_ability_receiver(text, identifier_start):
        return None

    parsed_arguments = _extract_call_arguments_at_identifier(text, identifier_start, identifier_end)
    if parsed_arguments is None:
        return None
    arguments, close_paren = parsed_arguments

    display_name_argument = _select_display_name_argument(identifier, arguments)
    if display_name_argument is None:
        return None

    return (
        _Callsite(
            method=identifier,
            line=text.count("\n", 0, identifier_start) + 1,
            display_name_argument=display_name_argument,
        ),
        close_paren + 1,
    )


def _extract_call_arguments_at_identifier(
    text: str,
    identifier_start: int,
    identifier_end: int,
) -> tuple[list[_Argument], int] | None:
    open_paren = _skip_whitespace(text, identifier_end)
    if open_paren >= len(text) or text[open_paren] != "(":
        return None

    parsed_arguments = _extract_arguments(text, open_paren + 1)
    if parsed_arguments is None:
        return None
    arguments, close_paren = parsed_arguments
    if _looks_like_method_declaration(text, identifier_start, close_paren):
        return None
    return arguments, close_paren


def _classify_callsite(file_path: str, callsite: _Callsite) -> ActivatedAbilityNameItem:
    expression = callsite.display_name_argument.strip()
    literal = _parse_csharp_string_literal(expression)
    if literal is not None:
        return ActivatedAbilityNameItem(
            file=file_path,
            line=callsite.line,
            method=callsite.method,
            classification="static_leaf",
            name=literal,
        )

    if _is_display_name_source(expression):
        return ActivatedAbilityNameItem(
            file=file_path,
            line=callsite.line,
            method=callsite.method,
            classification="display_name_source",
            expression=expression,
        )

    return ActivatedAbilityNameItem(
        file=file_path,
        line=callsite.line,
        method=callsite.method,
        classification="dynamic_composition",
        expression=expression,
    )


def _select_display_name_argument(method: str, arguments: list[_Argument]) -> str | None:
    display_name = next((argument for argument in arguments if argument.name == "DisplayName"), None)
    if display_name is not None:
        return display_name.expression

    if method not in {"SetActivatedAbilityDisplayName", "SetMyActivatedAbilityDisplayName"}:
        name = next((argument for argument in arguments if argument.name == "Name"), None)
        if name is not None:
            return name.expression

    display_name_index = _positional_display_name_index(method)
    return _argument_at_formal_index(method, arguments, display_name_index)


def _argument_at_formal_index(method: str, arguments: list[_Argument], display_name_index: int) -> str | None:
    used_formal_indexes: set[int] = set()
    next_positional_index = 0
    for argument in arguments:
        if argument.name is None:
            while next_positional_index in used_formal_indexes:
                next_positional_index += 1
            formal_index = next_positional_index
            next_positional_index += 1
        else:
            formal_index = _named_argument_formal_index(method, argument.name)
            if formal_index is None:
                continue

        if formal_index == display_name_index:
            return argument.expression
        used_formal_indexes.add(formal_index)
    return None


def _positional_display_name_index(method: str) -> int:
    if method == "AddDynamicCommand":
        return 2
    if method in {"SetActivatedAbilityDisplayName", "SetMyActivatedAbilityDisplayName"}:
        return 1
    return 0


def _named_argument_formal_index(method: str, name: str) -> int | None:
    if method == "AddDynamicCommand":
        return {
            "CommandForDescription": 1,
            "DisplayName": 2,
            "Name": 2,
            "Class": 3,
        }.get(name)
    if method in {"SetActivatedAbilityDisplayName", "SetMyActivatedAbilityDisplayName"}:
        return {
            "DisplayName": 1,
        }.get(name)
    return {
        "DisplayName": 0,
        "Name": 0,
        "Command": 1,
        "Class": 2,
    }.get(name)


def _has_activated_ability_receiver(text: str, method_start: int) -> bool:
    receiver = _member_receiver_expression(text, method_start)
    if receiver is None:
        return False

    normalized = re.sub(r"\s+", "", receiver).rstrip("?")
    if normalized in {"activatedAbilities", "Abilities", "ActivatedAbilities"}:
        return True
    if normalized.endswith(".ActivatedAbilities"):
        return True
    return "RequirePart<ActivatedAbilities>()" in normalized


def _extract_display_name_assignment(
    text: str,
    identifier: str,
    identifier_start: int,
    identifier_end: int,
) -> tuple[str, int] | None:
    if identifier != "DisplayName" or not _has_activated_ability_entry_receiver(text, identifier_start):
        return None

    equals_index = _skip_whitespace(text, identifier_end)
    if equals_index >= len(text) or text[equals_index] != "=":
        return None
    if equals_index + 1 < len(text) and text[equals_index + 1] in "=>":
        return None

    parsed_expression = _extract_assignment_expression(text, equals_index + 1)
    if parsed_expression is None:
        return None
    return parsed_expression


def _has_activated_ability_entry_receiver(text: str, member_start: int) -> bool:
    receiver = _member_receiver_expression(text, member_start)
    if receiver is None:
        return False

    receiver_name = _last_identifier(receiver)
    if receiver_name is None:
        return False

    normalized_receiver_name = receiver_name.lower()
    return normalized_receiver_name == "entry" or normalized_receiver_name.endswith("activatedabilityentry")


def _member_receiver_expression(text: str, member_start: int) -> str | None:
    dot_index = _previous_non_whitespace_index(text, member_start)
    if dot_index is None or text[dot_index] != ".":
        return None

    receiver_start = _receiver_expression_start(text, dot_index)
    receiver = text[receiver_start:dot_index].strip()
    return receiver or None


def _receiver_expression_start(text: str, receiver_end: int) -> int:
    index = receiver_end - 1
    depth = 0
    while index >= 0:
        char = text[index]
        if char in ")]}":
            depth += 1
        elif char in "([{":
            depth -= 1
            if depth < 0:
                return index + 1
        elif depth == 0 and (char.isspace() or char in ";{}=,+-*/"):
            return index + 1
        index -= 1
    return 0


def _last_identifier(expression: str) -> str | None:
    match = re.search(r"([A-Za-z_][A-Za-z0-9_]*)\??\s*$", expression)
    return None if match is None else match.group(1)


def _extract_assignment_expression(text: str, start: int) -> tuple[str, int] | None:
    index = start
    expression_start = _skip_whitespace(text, start)
    depth = 0
    while index < len(text):
        skipped = _skip_csharp_trivia_or_literal(text, index)
        if skipped != index:
            index = skipped
            continue

        char = text[index]
        if char in "([{":
            depth += 1
        elif char in ")]}":
            depth = max(0, depth - 1)
        elif char == ";" and depth == 0:
            return text[expression_start:index].strip(), index + 1
        index += 1
    return None


def _extract_arguments(text: str, start: int) -> tuple[list[_Argument], int] | None:
    arguments: list[_Argument] = []
    index = start
    argument_start = start
    depth = 0
    while index < len(text):
        skipped = _skip_csharp_trivia_or_literal(text, index)
        if skipped != index:
            index = skipped
            continue

        char = text[index]
        if char in "([{":
            depth += 1
        elif char in ")]}":
            if char == ")" and depth == 0:
                _append_argument(arguments, text[argument_start:index])
                return arguments, index
            depth = max(0, depth - 1)
        elif char == "," and depth == 0:
            _append_argument(arguments, text[argument_start:index])
            argument_start = index + 1
        index += 1
    return None


def _append_argument(arguments: list[_Argument], raw_argument: str) -> None:
    expression = raw_argument.strip()
    if not expression:
        return

    name, named_expression = _split_named_argument(expression)
    arguments.append(_Argument(name=name, expression=named_expression.strip() if name is not None else expression))


def _split_named_argument(expression: str) -> tuple[str | None, str]:
    depth = 0
    index = 0
    while index < len(expression):
        skipped = _skip_csharp_trivia_or_literal(expression, index)
        if skipped != index:
            index = skipped
            continue

        char = expression[index]
        if char in "([{":
            depth += 1
        elif char in ")]}":
            depth = max(0, depth - 1)
        elif char == "?" and depth == 0:
            return None, expression
        elif char == ":" and depth == 0:
            name = expression[:index].strip()
            if re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", name):
                return name, expression[index + 1 :]
            return None, expression
        index += 1
    return None, expression


def _looks_like_method_declaration(text: str, identifier_start: int, close_paren: int) -> bool:
    previous_index = _previous_non_whitespace_index(text, identifier_start)
    if previous_index is None or (not _is_identifier_part(text[previous_index]) and text[previous_index] not in ">]?"):
        return False
    if text[previous_index] == ">" and previous_index > 0 and text[previous_index - 1] == "=":
        return False
    if _previous_identifier(text, identifier_start) in {"await", "return", "throw"}:
        return False

    after_paren = _skip_whitespace(text, close_paren + 1)
    if after_paren >= len(text):
        return False
    return text.startswith("=>", after_paren) or text[after_paren] in "{;"


def _previous_non_whitespace_index(text: str, index: int) -> int | None:
    index -= 1
    while index >= 0 and text[index].isspace():
        index -= 1
    return index if index >= 0 else None


def _previous_identifier(text: str, index: int) -> str | None:
    end = _previous_non_whitespace_index(text, index)
    if end is None or not _is_identifier_part(text[end]):
        return None

    start = end
    while start >= 0 and _is_identifier_part(text[start]):
        start -= 1
    return text[start + 1 : end + 1]


def _is_display_name_source(expression: str) -> bool:
    expression = _strip_balanced_outer_parentheses(expression.strip())
    if expression.startswith("$"):
        return False
    match = DISPLAY_NAME_SOURCE_START_RE.match(expression)
    if match is None:
        return False

    close_paren = _find_matching_close_paren(expression, match.end() - 1)
    return close_paren == len(expression) - 1


def _find_matching_close_paren(expression: str, open_paren: int) -> int | None:
    index = open_paren + 1
    depth = 0
    while index < len(expression):
        skipped = _skip_csharp_trivia_or_literal(expression, index)
        if skipped != index:
            index = skipped
            continue

        char = expression[index]
        if char in "([{":
            depth += 1
        elif char in ")]}":
            if char == ")" and depth == 0:
                return index
            depth = max(0, depth - 1)
        index += 1
    return None


def _strip_balanced_outer_parentheses(expression: str) -> str:
    while expression.startswith("(") and expression.endswith(")"):
        inner = expression[1:-1].strip()
        if not _is_balanced_expression(inner):
            break
        expression = inner
    return expression


def _is_balanced_expression(expression: str) -> bool:
    depth = 0
    index = 0
    while index < len(expression):
        skipped = _skip_csharp_trivia_or_literal(expression, index)
        if skipped != index:
            index = skipped
            continue

        char = expression[index]
        if char in "([{":
            depth += 1
        elif char in ")]}":
            depth -= 1
            if depth < 0:
                return False
        index += 1
    return depth == 0


def _parse_csharp_string_literal(expression: str) -> str | None:
    if expression.startswith('"'):
        result = _consume_regular_string(expression, 0)
    elif expression.startswith('@"'):
        result = _consume_verbatim_string(expression, 0)
    else:
        return None

    if result is None:
        return None
    end_index, value = result
    if end_index != len(expression):
        return None
    return value


def _skip_csharp_trivia_or_literal(text: str, index: int) -> int:
    comment_end = _skip_csharp_comment(text, index)
    if comment_end is not None:
        return comment_end
    literal_end = _skip_csharp_literal(text, index)
    if literal_end is not None:
        return literal_end
    return index


def _skip_csharp_comment(text: str, index: int) -> int | None:
    if text.startswith("//", index):
        newline = text.find("\n", index + 2)
        return len(text) if newline == -1 else newline + 1
    if text.startswith("/*", index):
        end = text.find("*/", index + 2)
        return len(text) if end == -1 else end + 2
    return None


def _skip_csharp_literal(text: str, index: int) -> int | None:
    literal_end: int | None = None
    if text.startswith('@"', index):
        literal_end = _consume_verbatim_string_end(text, index)
    elif text.startswith('$@"', index):
        literal_end = _consume_verbatim_string_end(text, index + 1)
    elif text.startswith('@$"', index):
        literal_end = _consume_verbatim_string_body_end(text, index + 2)
    elif text.startswith('"""', index) or text.startswith('$"""', index):
        literal_end = _consume_raw_string_end(text, index)
    elif text.startswith('$"', index):
        literal_end = _consume_regular_string_end(text, index + 1)
    elif text[index] == '"':
        literal_end = _consume_regular_string_end(text, index)
    elif text[index] == "'":
        literal_end = _consume_char_literal_end(text, index)
    return literal_end


def _consume_regular_string(text: str, start: int) -> tuple[int, str] | None:
    if start >= len(text) or text[start] != '"':
        return None

    chars: list[str] = []
    index = start + 1
    while index < len(text):
        char = text[index]
        if char == '"':
            return index + 1, "".join(chars)
        if char == "\\":
            escaped = _consume_regular_string_escape(text, index)
            if escaped is None:
                return None
            index, value = escaped
            chars.append(value)
            continue
        chars.append(char)
        index += 1
    return None


def _consume_regular_string_escape(text: str, start: int) -> tuple[int, str] | None:
    if start + 1 >= len(text):
        return None
    escape = text[start + 1]
    escape_map = {
        '"': '"',
        "'": "'",
        "\\": "\\",
        "0": "\0",
        "a": "\a",
        "b": "\b",
        "f": "\f",
        "n": "\n",
        "r": "\r",
        "t": "\t",
        "v": "\v",
    }
    if escape in escape_map:
        return start + 2, escape_map[escape]
    if escape == "u" and start + 6 <= len(text):
        hex_digits = text[start + 2 : start + 6]
        if re.fullmatch(r"[0-9A-Fa-f]{4}", hex_digits):
            return start + 6, chr(int(hex_digits, 16))
    return start + 2, escape


def _consume_verbatim_string(text: str, start: int) -> tuple[int, str] | None:
    if not text.startswith('@"', start):
        return None

    return _consume_verbatim_string_body(text, start + 2)


def _consume_verbatim_string_body(text: str, start: int) -> tuple[int, str] | None:
    chars: list[str] = []
    index = start
    while index < len(text):
        char = text[index]
        if char == '"':
            if index + 1 < len(text) and text[index + 1] == '"':
                chars.append('"')
                index += 2
                continue
            return index + 1, "".join(chars)
        chars.append(char)
        index += 1
    return None


def _consume_regular_string_end(text: str, start: int) -> int:
    result = _consume_regular_string(text, start)
    return len(text) if result is None else result[0]


def _consume_verbatim_string_end(text: str, start: int) -> int:
    result = _consume_verbatim_string(text, start)
    return len(text) if result is None else result[0]


def _consume_verbatim_string_body_end(text: str, start: int) -> int:
    result = _consume_verbatim_string_body(text, start)
    return len(text) if result is None else result[0]


def _consume_raw_string_end(text: str, start: int) -> int:
    quote_start = text.find('"""', start)
    if quote_start == -1:
        return len(text)
    quote_count = 0
    while quote_start + quote_count < len(text) and text[quote_start + quote_count] == '"':
        quote_count += 1
    delimiter = '"' * quote_count
    end = text.find(delimiter, quote_start + quote_count)
    return len(text) if end == -1 else end + quote_count


def _consume_char_literal_end(text: str, start: int) -> int:
    index = start + 1
    while index < len(text):
        if text[index] == "\\":
            index += 2
            continue
        if text[index] == "'":
            return index + 1
        index += 1
    return len(text)


def _skip_whitespace(text: str, index: int) -> int:
    while index < len(text) and text[index].isspace():
        index += 1
    return index


def _is_identifier_start(char: str) -> bool:
    return char == "_" or char.isalpha()


def _is_identifier_part(char: str) -> bool:
    return char == "_" or char.isalnum()


def _parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Scan C# sources for activated ability display-name callsites.",
    )
    parser.add_argument("--source-root", required=True, type=Path, help="Directory containing C# source files.")
    parser.add_argument("--output", required=True, type=Path, help="Path to write inventory JSON.")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    """Run the activated ability display-name scanner CLI."""
    args = _parse_args(argv)
    source_root = args.source_root
    if not source_root.is_dir():
        sys.stderr.write(f"source root does not exist or is not a directory: {source_root}\n")
        return 1

    write_inventory(source_root, args.output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
