"""Bump the VERSION file (Semantic Versioning 2.0.0, see README "Versioning").

    python tools/bump_version.py beta    # 0.3.0-beta.1 -> 0.3.0-beta.2   (every tester build)
    python tools/bump_version.py patch   # 0.3.0-beta.2 -> 0.3.1-beta.1   (bug-fix line)
    python tools/bump_version.py minor   # 0.3.1-beta.4 -> 0.4.0-beta.1   (new features)
    python tools/bump_version.py major   # 0.9.0-beta.3 -> 1.0.0-beta.1
    python tools/bump_version.py release # 1.0.0-beta.3 -> 1.0.0          (drop the pre-release tag)
    python tools/bump_version.py         # print the current version and Android versionCode
"""
import pathlib
import re
import sys

FILE = pathlib.Path(__file__).resolve().parent.parent / "VERSION"
PATTERN = re.compile(r"^(\d+)\.(\d+)\.(\d+)(?:-beta\.(\d+))?$")


def parse(text):
    m = PATTERN.match(text.strip())
    if not m:
        sys.exit(f"VERSION must look like 1.2.3 or 1.2.3-beta.4, found {text.strip()!r}")
    major, minor, patch = (int(m.group(i)) for i in (1, 2, 3))
    beta = int(m.group(4)) if m.group(4) else None
    return major, minor, patch, beta


def fmt(major, minor, patch, beta):
    return f"{major}.{minor}.{patch}" + (f"-beta.{beta}" if beta is not None else "")


def code(major, minor, patch, beta):
    """Must match Assets/Editor/MaliGoVersion.cs."""
    return major * 1_000_000 + minor * 10_000 + patch * 100 + (beta if beta is not None else 99)


def main():
    major, minor, patch, beta = parse(FILE.read_text(encoding="utf-8"))
    part = sys.argv[1] if len(sys.argv) > 1 else None
    if part == "beta":
        if beta is None:
            # A beta after a release starts the next patch line.
            major, minor, patch, beta = major, minor, patch + 1, 1
        else:
            beta += 1
    elif part == "patch":
        major, minor, patch, beta = major, minor, patch + 1, 1
    elif part == "minor":
        major, minor, patch, beta = major, minor + 1, 0, 1
    elif part == "major":
        major, minor, patch, beta = major + 1, 0, 0, 1
    elif part == "release":
        beta = None
    elif part is not None:
        sys.exit(__doc__)

    if minor > 99 or patch > 99 or (beta or 0) > 99:
        sys.exit("minor, patch and beta must stay 0-99 (versionCode encoding)")

    version = fmt(major, minor, patch, beta)
    if part is not None:
        FILE.write_text(version + "\n", encoding="utf-8", newline="\n")
    print(f"{version} (Android versionCode {code(major, minor, patch, beta)})")


if __name__ == "__main__":
    main()
