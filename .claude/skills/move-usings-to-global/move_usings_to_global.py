#!/usr/bin/env python3
"""
move_usings_to_global.py

Consolidates per-file "using" statements into a single GlobalUsings.cs per
project, prepending "global " to each moved using and removing the original
line. Python port of Move-UsingsToGlobal.ps1 — same behavior, cross-platform.

Usage:
    python move_usings_to_global.py --root-path C:\\path\\to\\solution --whatif
    python move_usings_to_global.py --root-path /path/to/solution

Behavior:
    For every .csproj found under --root-path:
      1. Creates <ProjectDir>/GlobalUsings.cs if it doesn't already exist.
      2. Scans every .cs file in that project (excluding bin/obj/.git/.vs,
         any Migrations folder at any depth — in every project of the
         solution, e.g. Clean Architecture / CQRS layouts — and
         GlobalUsings.cs itself).
      3. Only considers the file's top "header block" — the contiguous run
         of blank lines, comments, #directives, and using statements at the
         very start of the file. Scanning stops at the first non-matching
         line (namespace, class, etc.), so usings deliberately scoped inside
         a namespace block are left untouched.
      4. Never moves:
           - Alias usings, e.g. `using Entity = My.Entities.Product;`
           - `using static X;` directives
           - Lines already written as `global using ...;`
      5. Everything else is removed from the file and appended (deduplicated,
         sorted) to that project's GlobalUsings.cs as `global using X;`.
      6. Spacing before the first post-header line (e.g. "namespace Foo;")
         is normalised: dangling blank lines are removed, then exactly one
         blank line is inserted if any header content (alias/static usings,
         comments, directives) remains above it.

    Re-running is safe: existing entries in GlobalUsings.cs are read first
    and never duplicated.
"""

import argparse
import os
import re
import sys

USING_RE = re.compile(r'^using\s+(?P<body>.+?)\s*;\s*$')
GLOBAL_USING_RE = re.compile(r'^global\s+using\s+(?P<body>.+?)\s*;\s*$')
STATIC_BODY_RE = re.compile(r'^static\s')

BOM = b'\xef\xbb\xbf'


def parse_using_line(trimmed):
    """
    Returns a dict describing the line, or None if it's not a using directive.
    Keys: is_global (bool), is_alias (bool), is_static (bool), body (str)
    """
    m = GLOBAL_USING_RE.match(trimmed)
    if m:
        body = m.group('body')
        return {
            'is_global': True,
            'is_alias': '=' in trimmed,
            'is_static': bool(STATIC_BODY_RE.match(body)),
            'body': body,
        }

    m = USING_RE.match(trimmed)
    if m:
        body = m.group('body')
        return {
            'is_global': False,
            'is_alias': '=' in trimmed,
            'is_static': bool(STATIC_BODY_RE.match(body)),
            'body': body,
        }

    return None


def is_header_passthrough(trimmed):
    """Blank lines, // comments, single-line /* */ comments, and # directives."""
    if trimmed == '':
        return True
    if trimmed.startswith('//'):
        return True
    if trimmed.startswith('/*') and trimmed.endswith('*/'):
        return True
    if trimmed.startswith('#'):
        return True
    return False


def is_excluded(path, exclude_dirs_lower):
    parts = os.path.normpath(path).split(os.sep)
    return any(part.lower() in exclude_dirs_lower for part in parts)


def find_files(root, extension, exclude_dirs_lower):
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d.lower() not in exclude_dirs_lower]
        for name in filenames:
            if name.lower().endswith(extension):
                yield os.path.join(dirpath, name)


def read_lines_preserving_bom(path):
    """Returns (lines: list[str], had_bom: bool). Lines have no trailing newline."""
    with open(path, 'rb') as f:
        raw = f.read()
    had_bom = raw.startswith(BOM)
    text = raw[len(BOM):] if had_bom else raw
    text = text.decode('utf-8')
    # Normalize line endings, then split, dropping a single trailing empty
    # element caused by a final newline.
    lines = text.replace('\r\n', '\n').replace('\r', '\n').split('\n')
    if lines and lines[-1] == '':
        lines.pop()
    return lines, had_bom


def write_lines_preserving_bom(path, lines, had_bom):
    content = '\n'.join(lines) + '\n'
    data = content.encode('utf-8')
    if had_bom:
        data = BOM + data
    with open(path, 'wb') as f:
        f.write(data)


def process_cs_file(path):
    """
    Returns (moved_any: bool, output_lines: list[str], moved_bodies: set[str], had_bom: bool)
    """
    lines, had_bom = read_lines_preserving_bom(path)
    if not lines:
        return False, lines, set(), had_bom

    output_lines = []
    moved_bodies = set()
    moved_any = False
    still_in_header = True
    header_end_index = -1

    for line in lines:
        if still_in_header:
            trimmed = line.strip()

            if is_header_passthrough(trimmed):
                output_lines.append(line)
                continue

            info = parse_using_line(trimmed)
            if info is not None:
                if info['is_alias'] or info['is_static'] or info['is_global']:
                    # Alias usings and 'using static' are exempt;
                    # already-global usings are left as-is.
                    output_lines.append(line)
                else:
                    moved_bodies.add(info['body'])
                    moved_any = True
                continue
            else:
                # First non-matching line -> header ends here.
                still_in_header = False
                header_end_index = len(output_lines)
                output_lines.append(line)
                continue
        else:
            output_lines.append(line)

    if moved_any and header_end_index > 0:
        # Collapse any blank line(s) left dangling before the first post-header line.
        while header_end_index > 0 and output_lines[header_end_index - 1].strip() == '':
            del output_lines[header_end_index - 1]
            header_end_index -= 1

        # If anything remains above it (alias/static usings, comments, directives),
        # insert exactly ONE blank line so it is separated from the namespace.
        if header_end_index > 0:
            output_lines.insert(header_end_index, '')

    return moved_any, output_lines, moved_bodies, had_bom


def main():
    parser = argparse.ArgumentParser(
        description='Consolidate per-file using statements into GlobalUsings.cs per project.'
    )
    parser.add_argument('--root-path', default='.', help='Root folder to search for .csproj files.')
    parser.add_argument(
        '--exclude-dirs',
        nargs='+',
        default=['bin', 'obj', '.git', '.vs', 'Migrations'],
        help='Folder names to skip anywhere in the path.',
    )
    parser.add_argument(
        '--whatif',
        action='store_true',
        help='Preview changes without writing any files.',
    )
    args = parser.parse_args()

    exclude_dirs_lower = {d.lower() for d in args.exclude_dirs}
    root_path = os.path.abspath(args.root_path)

    csproj_files = sorted(find_files(root_path, '.csproj', exclude_dirs_lower))

    if not csproj_files:
        print(f"No .csproj files found under '{root_path}'.", file=sys.stderr)
        return

    for csproj in csproj_files:
        project_dir = os.path.dirname(csproj)
        global_usings_file = os.path.join(project_dir, 'GlobalUsings.cs')

        print(f"\n=== Project: {os.path.basename(csproj)} ===")

        # --- Load existing global usings (so we don't duplicate on re-run) ---
        existing_global_usings = set()
        if os.path.exists(global_usings_file):
            existing_lines, _ = read_lines_preserving_bom(global_usings_file)
            for line in existing_lines:
                info = parse_using_line(line.strip())
                if info is not None and info['is_global']:
                    existing_global_usings.add(info['body'])
        else:
            if args.whatif:
                print(f"  Would create {global_usings_file}")
            else:
                open(global_usings_file, 'a', encoding='utf-8').close()
                print(f"  Created {global_usings_file}")

        new_usings_this_project = set()

        # --- Scan every .cs file in the project (except GlobalUsings.cs itself) ---
        cs_files = sorted(find_files(project_dir, '.cs', exclude_dirs_lower))
        global_usings_norm = os.path.normcase(os.path.abspath(global_usings_file))

        for cs_file in cs_files:
            if os.path.normcase(os.path.abspath(cs_file)) == global_usings_norm:
                continue

            moved_any, output_lines, moved_bodies, had_bom = process_cs_file(cs_file)

            if moved_any:
                new_usings_this_project.update(moved_bodies)
                if args.whatif:
                    print(f"  Would update: {cs_file}")
                else:
                    write_lines_preserving_bom(cs_file, output_lines, had_bom)
                    print(f"  Updated: {cs_file}")

        # --- Write the consolidated GlobalUsings.cs ---
        all_usings = existing_global_usings | new_usings_this_project

        if all_usings:
            sorted_usings = sorted(all_usings)
            content_lines = [f'global using {body};' for body in sorted_usings]

            if args.whatif:
                print(f"  Would write {len(sorted_usings)} global using(s) to {global_usings_file}")
            else:
                write_lines_preserving_bom(global_usings_file, content_lines, had_bom=False)
                print(f"  Wrote {len(sorted_usings)} global using(s) to {global_usings_file}")
        else:
            print("  No usings to move for this project.")

    print("\nDone.")


if __name__ == '__main__':
    main()
