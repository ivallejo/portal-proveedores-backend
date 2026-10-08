using System.Text.RegularExpressions;

namespace WebProveedores.Tests.Architecture;

/// <summary>Un tipo por archivo, con el nombre del archivo, y sin tipos anidados (docs/PLAN_HEXAGONAL.md, regla 6).</summary>
public sealed partial class SourceLayoutTests
{
    public static TheoryData<string> Projects => new() { "WebProveedores.Domain", "WebProveedores.Application" };

    [Theory]
    [MemberData(nameof(Projects))]
    public void Each_file_declares_one_type_named_like_the_file(string project)
    {
        var offenders = SourceFiles(project)
            .Select(path => (Path: path, Types: TopLevelType().Matches(File.ReadAllText(path)).Select(match => match.Groups["name"].Value).ToList()))
            .Where(file => file.Types.Count != 1 || file.Types[0] != Path.GetFileNameWithoutExtension(file.Path))
            .Select(file => $"{Relative(file.Path)} [{string.Join(", ", file.Types)}]")
            .ToList();

        Assert.True(offenders.Count == 0, "Archivos que no tienen exactamente un tipo con su nombre:\n" + string.Join("\n", offenders));
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void Types_are_not_nested(string project)
    {
        var offenders = SourceFiles(project)
            .SelectMany(path => NestedType().Matches(File.ReadAllText(path)).Select(match => $"{Relative(path)}: {match.Groups["name"].Value}"))
            .ToList();

        Assert.True(offenders.Count == 0, "Tipos anidados:\n" + string.Join("\n", offenders));
    }

    private static readonly string Root = FindRoot();

    private static IEnumerable<string> SourceFiles(string project) =>
        Directory.EnumerateFiles(Path.Combine(Root, project), "*.cs", SearchOption.AllDirectories)
            .Where(path => !Segments(path).Any(segment => segment is "obj" or "bin" or "Migrations"));

    private static string[] Segments(string path) => Path.GetRelativePath(Root, path).Split(Path.DirectorySeparatorChar);

    private static string Relative(string path) => Path.GetRelativePath(Root, path);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WebProveedores.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("No se encontró WebProveedores.slnx.");
    }

    private const string Declaration = @"(?:(?:public|internal|private|protected|file)\s+)*(?:(?:sealed|static|abstract|partial|readonly)\s+)*(?:record\s+struct|record\s+class|class|record|enum|interface|struct)\s+(?<name>\w+)";

    [GeneratedRegex(@"^" + Declaration, RegexOptions.Multiline)]
    private static partial Regex TopLevelType();

    [GeneratedRegex(@"^[ \t]+" + Declaration, RegexOptions.Multiline)]
    private static partial Regex NestedType();
}
