using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Reads the app version from the VERSION.txt file at the repo root (Semantic Versioning 2.0.0, see README
/// "Versioning") and derives the Android versionCode from it, so every tester build has a distinct name and
/// a code that only ever goes up. Bump the file with tools/bump_version.py.
/// </summary>
public static class MaliGoVersion
{
    static readonly Regex Pattern = new Regex(@"^(\d+)\.(\d+)\.(\d+)(?:-beta\.(\d+))?$");

    /// <summary>The VERSION.txt file's text, e.g. "0.3.0-beta.1".</summary>
    public static string Name => Read().name;

    /// <summary>major*1,000,000 + minor*10,000 + patch*100 + beta number; a release (no -beta) uses 99, so it
    /// sorts after all of its betas, exactly like SemVer precedence. 0.3.0-beta.1 -> 30001.</summary>
    public static int Code => Read().code;

    static (string name, int code) Read()
    {
        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "VERSION.txt");
        string name = File.Exists(path) ? File.ReadAllText(path).Trim() : "";
        Match m = Pattern.Match(name);
        if (!m.Success)
        {
            throw new InvalidOperationException($"VERSION must look like 1.2.3 or 1.2.3-beta.4, found '{name}' in {path}.");
        }

        int major = int.Parse(m.Groups[1].Value);
        int minor = int.Parse(m.Groups[2].Value);
        int patch = int.Parse(m.Groups[3].Value);
        int beta = m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 99;
        if (minor > 99 || patch > 99 || beta > 99 || (m.Groups[4].Success && beta < 1))
        {
            throw new InvalidOperationException($"VERSION '{name}': minor, patch and beta must be 0-99 (beta starts at 1).");
        }

        return (name, major * 1000000 + minor * 10000 + patch * 100 + beta);
    }
}
