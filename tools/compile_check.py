"""
Compile-check MaliGo's C# without opening Unity.

Unity generates Assembly-CSharp.csproj and Assembly-CSharp-Editor.csproj in the project
root (they are gitignored). This script copies their references, points relative
Library\\ paths at the project that owns that Library folder, compiles every runtime
script under <root>/Assets (and, with --editor, every Editor script) with `dotnet build`,
and reports the errors. Nothing is written inside <root>.

Usage:
    python tools/compile_check.py                     # check this checkout
    python tools/compile_check.py --root <path>       # check another checkout (e.g. a git worktree)
    python tools/compile_check.py --editor            # also check Editor scripts
    python tools/compile_check.py --template <path>   # project that has the generated .csproj files and Library/
Exit code: 0 = no errors, 1 = compile errors, 2 = setup problem.
"""
import argparse
import os
import re
import shutil
import subprocess
import sys
import tempfile

DEFAULT_TEMPLATE = r"C:\Temp\MaliGoGameDev"


def runtime_and_editor_sources(root):
    runtime, editor = [], []
    assets = os.path.join(root, "Assets")
    for dirpath, dirnames, filenames in os.walk(assets):
        # Skip folders Unity never compiles into these assemblies.
        dirnames[:] = [d for d in dirnames if not d.startswith(".") and not d.endswith("~")]
        for name in filenames:
            if not name.endswith(".cs"):
                continue
            path = os.path.join(dirpath, name)
            parts = os.path.relpath(path, assets).replace("\\", "/").split("/")
            (editor if "Editor" in parts[:-1] else runtime).append(path)
    return sorted(runtime), sorted(editor)


def make_project(template_csproj, template_root, sources, assembly_name, extra_reference=None):
    text = open(template_csproj, encoding="utf-8-sig").read()

    # Make relative HintPaths (Library\ScriptAssemblies, Library\PackageCache) absolute,
    # so the project can live outside the Unity project folder.
    def absolutise(match):
        path = match.group(1)
        if not re.match(r"^[A-Za-z]:", path):
            path = os.path.join(template_root, path)
        return "<HintPath>%s</HintPath>" % path

    text = re.sub(r"<HintPath>([^<]*)</HintPath>", absolutise, text)
    text = re.sub(r"\s*<Compile Include=\"[^\"]*\" />", "", text)
    text = re.sub(r"\s*<Analyzer Include=\"[^\"]*\" />", "", text)
    # Editor project: never compile against Unity's stale copy of the runtime assembly.
    text = re.sub(r"\s*<Reference Include=\"Assembly-CSharp\">\s*<HintPath>[^<]*</HintPath>\s*</Reference>", "", text)
    text = re.sub(r"\s*<ProjectReference Include=\"[^\"]*\"[^>]*/>", "", text)
    text = re.sub(r"\s*<ProjectReference Include=\"[^\"]*\"[^>]*>.*?</ProjectReference>", "", text, flags=re.S)
    text = re.sub(r"<AssemblyName>[^<]*</AssemblyName>", "<AssemblyName>%s</AssemblyName>" % assembly_name, text)
    text = re.sub(r"<BaseIntermediateOutputPath>[^<]*</BaseIntermediateOutputPath>", "<BaseIntermediateOutputPath>obj\\</BaseIntermediateOutputPath>", text)
    text = re.sub(r"<OutputPath>[^<]*</OutputPath>", "<OutputPath>bin\\</OutputPath>", text)

    items = "\n".join('    <Compile Include="%s" />' % s for s in sources)
    if extra_reference:
        items += '\n    <Reference Include="Assembly-CSharp"><HintPath>%s</HintPath></Reference>' % extra_reference
    return text.replace("<ItemGroup>", "<ItemGroup>\n" + items, 1)


def build(project_dir, project_name, content):
    path = os.path.join(project_dir, project_name)
    with open(path, "w", encoding="utf-8") as handle:
        handle.write(content)
    result = subprocess.run(
        ["dotnet", "build", path, "-nologo", "-v:q", "-clp:NoSummary"],
        capture_output=True, text=True, encoding="utf-8", errors="replace")
    output = result.stdout + result.stderr
    errors = sorted({line.strip() for line in output.splitlines() if ": error " in line})
    warnings = sorted({line.strip() for line in output.splitlines() if ": warning CS" in line})
    return result.returncode, errors, warnings


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    parser.add_argument("--template", default=None)
    parser.add_argument("--editor", action="store_true")
    parser.add_argument("--show-warnings", action="store_true")
    args = parser.parse_args()

    root = os.path.abspath(args.root)
    template = os.path.abspath(args.template) if args.template else (
        root if os.path.exists(os.path.join(root, "Assembly-CSharp.csproj")) else DEFAULT_TEMPLATE)
    runtime_csproj = os.path.join(template, "Assembly-CSharp.csproj")
    editor_csproj = os.path.join(template, "Assembly-CSharp-Editor.csproj")
    if not os.path.exists(runtime_csproj):
        print("SETUP: %s not found. Open the project in Unity once so it generates the .csproj files." % runtime_csproj)
        return 2

    runtime_sources, editor_sources = runtime_and_editor_sources(root)
    work = tempfile.mkdtemp(prefix="maligo_compile_")
    failed = False
    try:
        code, errors, warnings = build(work, "Assembly-CSharp.csproj",
                                       make_project(runtime_csproj, template, runtime_sources, "Assembly-CSharp"))
        print("Runtime scripts: %d files, %d errors, %d warnings" % (len(runtime_sources), len(errors), len(warnings)))
        for line in errors:
            print("  " + line)
        if args.show_warnings:
            for line in warnings:
                print("  " + line)
        failed = failed or code != 0 or bool(errors)

        if args.editor and not failed:
            if not os.path.exists(editor_csproj):
                print("SETUP: %s not found." % editor_csproj)
                return 2
            runtime_dll = os.path.join(work, "bin", "Assembly-CSharp.dll")
            editor_dir = os.path.join(work, "editor")
            os.makedirs(editor_dir)
            code, errors, warnings = build(editor_dir, "Assembly-CSharp-Editor.csproj",
                                           make_project(editor_csproj, template, editor_sources,
                                                        "Assembly-CSharp-Editor", extra_reference=runtime_dll))
            print("Editor scripts: %d files, %d errors, %d warnings" % (len(editor_sources), len(errors), len(warnings)))
            for line in errors:
                print("  " + line)
            failed = failed or code != 0 or bool(errors)
    finally:
        shutil.rmtree(work, ignore_errors=True)

    print("RESULT: %s" % ("FAILED" if failed else "OK"))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
