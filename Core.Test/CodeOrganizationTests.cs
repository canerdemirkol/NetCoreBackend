using System.Text.RegularExpressions;

namespace Core.Test;

/// <summary>
/// One top-level type per source file, named after it, in every Core package and in these tests. Splitting a file keeps its folder, so
/// namespaces (and every consumer's usings) stay the same. Nested types are the file's own business (a private JSON shape) and are not
/// counted.
/// </summary>
public partial class CodeOrganizationTests
{
    private static readonly string[] SkippedFolders = ["Migrations", "bin", "obj"];

    // A declaration at the start of a line: with file-scoped namespaces only top-level types start there.
    [GeneratedRegex(@"^(?:(?:public|internal|file|sealed|static|abstract|partial|readonly)\s+)*(?:record\s+(?:struct|class)\s+|class\s+|record\s+|interface\s+|enum\s+|struct\s+)(\w+)", RegexOptions.Multiline)]
    private static partial Regex TopLevelType();

    private static string RepositoryRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "NetCoreBackend.sln")))
                return dir.FullName;
        throw new InvalidOperationException("The repository root (NetCoreBackend.sln) was not found above the test output.");
    }

    private static IEnumerable<string> SourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(root, f).Split(Path.DirectorySeparatorChar).Any(SkippedFolders.Contains));

    [Fact]
    public void EverySourceFile_HoldsOneTopLevelType_NamedAfterIt()
    {
        string root = RepositoryRoot();
        List<string> problems = [];
        foreach (string file in SourceFiles(root))
        {
            // A generic family sharing one name (IRequest and IRequest<TResponse>) counts as one type: .NET keeps it in one file.
            string[] types = [.. TopLevelType().Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value).Distinct()];
            string relative = Path.GetRelativePath(root, file);
            if (types.Length > 1)
                problems.Add($"{relative}: {types.Length} types ({string.Join(", ", types)})");
            else if (types.Length == 1 && types[0] != Path.GetFileNameWithoutExtension(file))
                problems.Add($"{relative}: holds {types[0]}");
        }

        Assert.True(problems.Count == 0, $"{problems.Count} file(s) break one type per file:\n" + string.Join("\n", problems.Order()));
    }
}
