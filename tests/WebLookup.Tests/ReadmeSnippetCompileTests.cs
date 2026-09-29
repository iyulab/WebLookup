using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace WebLookup.Tests;

/// <summary>
/// Compiles every <c>```csharp</c> block in README.md against the current assembly. A compiler checks the receiver, the
/// arguments, the return types a block goes on to use, and the namespaces it needs.
/// </summary>
/// <remarks>
/// A block is compiled as a top-level program: its <c>using</c> lines are hoisted, the common usings below are added, and
/// the stand-ins below are declared when the block uses the name without declaring it — values a reader already has
/// from the surrounding text (the service collection, a provider), not part of what the block shows.
/// A block that only declares types is the API Reference's sketch of a library type: it compiles as a library, and each
/// member it shows must exist on the real type with the same signature — a sketch compiles on its own whatever it says,
/// so the compiler alone would not notice it drifting from the library.
/// </remarks>
public class ReadmeSnippetCompileTests
{
    // A block that is deliberately not a program (a signature sketch, pseudocode) is listed here by the heading it sits
    // under, with the reason. Shrink this, never grow it silently.
    private static readonly Dictionary<string, string> Fragments = new(StringComparer.Ordinal);

    // The implicit usings of an SDK console project (ImplicitUsings=enable).
    private const string CommonUsings = """
        using System;
        using System.Collections.Generic;
        using System.IO;
        using System.Linq;
        using System.Net.Http;
        using System.Threading;
        using System.Threading.Tasks;
        """;

    // The README reads as one guide: its Quick Start states this using once, and every later block assumes it.
    // ReadmeUsings_AreTheOnesTheQuickStartStates keeps this list and the README's statement the same.
    private static readonly string[] ReadmeUsings = ["WebLookup"];

    private static readonly (string Name, string Declaration)[] StandIns =
    [
        ("services", "Microsoft.Extensions.DependencyInjection.IServiceCollection services = null!;"),
        // The "Filter URLs" block continues the "Explore a site" block.
        ("explorer", "WebLookup.SiteExplorer explorer = new();"),
        // The WebSearchOptions block configures a client over a provider built earlier.
        ("provider", "WebLookup.ISearchProvider provider = null!;"),
    ];

    private static readonly string[] AssembliesToLoad =
    [
        "WebLookup",
        "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.DependencyInjection.Abstractions",
    ];

    private static readonly SymbolDisplayFormat SignatureFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static TheoryData<string> Blocks()
    {
        var data = new TheoryData<string>();
        foreach (var block in ReadBlocks())
            data.Add(block.Key);
        return data;
    }

    [Theory]
    [MemberData(nameof(Blocks))]
    public void ReadmeBlock_Compiles(string key)
    {
        var block = ReadBlocks().Single(b => b.Key == key);
        if (Fragments.ContainsKey(block.Heading))
            return;

        var errors = Check(block.Code);

        Assert.True(errors.Count == 0,
            $"README block {key} does not match the current API:\n" +
            string.Join("\n", errors) + "\n--- source ---\n" + Program(block.Code));
    }

    [Fact]
    public void EveryReadmeBlock_IsFoundAndFragmentsNameRealHeadings()
    {
        var blocks = ReadBlocks();
        Assert.True(blocks.Count >= 16, $"expected the README's C# blocks, found {blocks.Count}");
        Assert.All(Fragments.Keys, heading => Assert.Contains(blocks, b => b.Heading == heading));
    }

    [Fact]
    public void ReadmeUsings_AreTheOnesTheQuickStartStates()
    {
        var quickStart = ReadBlocks()[0];
        var stated = quickStart.Code.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Select(l => Regex.Match(l, @"^using ([\w.]+);"))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value);

        Assert.Equal(ReadmeUsings, stated);
    }

    /// <summary>Positive control: the compiler rejects a call the library does not have.</summary>
    [Fact]
    public void Compile_RejectsAMethodTheLibraryDoesNotHave()
    {
        var errors = Check("""
            var client = new WebSearchClient();
            await client.SearchAllTheThingsAsync("query");
            """);

        Assert.NotEmpty(errors);
    }

    /// <summary>Positive control: a type sketch that shows a member the real type does not have is rejected.</summary>
    [Fact]
    public void Sketch_RejectsAMemberTheTypeDoesNotHave()
    {
        var errors = Check("""
            public record SearchResult
            {
                public required string Url { get; init; }
                public double Score { get; init; }
            }
            """);

        Assert.Contains(errors, e => e.Contains("Score", StringComparison.Ordinal));
    }

    /// <summary>Positive control: a type sketch that shows a member with the wrong signature is rejected.</summary>
    [Fact]
    public void Sketch_RejectsAMemberWithAnotherSignature()
    {
        var errors = Check("""
            public class WebSearchOptions
            {
                public int MaxResultsPerProvider { get; init; } = 10;
            }
            """);

        Assert.Contains(errors, e => e.Contains("MaxResultsPerProvider", StringComparison.Ordinal));
    }

    /// <summary>Positive control: a type sketch of a type the library does not export is rejected.</summary>
    [Fact]
    public void Sketch_RejectsATypeTheLibraryDoesNotExport()
    {
        var errors = Check("""
            public record SearchResponse
            {
                public string? Url { get; init; }
            }
            """);

        Assert.Contains(errors, e => e.Contains("SearchResponse", StringComparison.Ordinal));
    }

    private sealed record Block(string Key, string Heading, string Code);

    private static List<Block> ReadBlocks()
    {
        var lines = File.ReadAllText(Path.Combine(RepoRoot(), "README.md"))
            .Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var blocks = new List<Block>();
        var heading = "(top)";
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith('#'))
                heading = lines[i].TrimStart('#').Trim();
            if (lines[i].Trim() != "```csharp")
                continue;

            var start = i + 1;
            var code = new StringBuilder();
            for (i++; i < lines.Length && lines[i].Trim() != "```"; i++)
                code.AppendLine(lines[i]);
            blocks.Add(new Block($"README.md line {start}: {heading}", heading, code.ToString()));
        }

        return blocks;
    }

    private static string Program(string code)
    {
        var lines = code.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        static string Code(string l) => l.Split("//", 2)[0].TrimEnd();
        static bool IsUsingDirective(string l) =>
            l.StartsWith("using ", StringComparison.Ordinal) && Code(l).EndsWith(';') && !l.StartsWith("using var ", StringComparison.Ordinal);

        var body = string.Join("\n", lines.Where(l => !IsUsingDirective(l)));
        var standIns = StandIns
            .Where(s => Regex.IsMatch(body, $@"\b{s.Name}\b")
                        && !Regex.IsMatch(body, $@"\b(var|[A-Z][\w<>?,\s]*)\s+{s.Name}\s*[=;]"))
            .Select(s => s.Declaration);

        // A block that states a using the README already assumes must not get it twice.
        var stated = lines.Where(IsUsingDirective).Select(Code).ToHashSet(StringComparer.Ordinal);
        var assumed = ReadmeUsings.Select(n => $"using {n};").Where(u => !stated.Contains(u));

        return string.Join("\n", stated) + "\n" + CommonUsings + "\n"
               + string.Join("\n", assumed) + "\n"
               + string.Join("\n", standIns) + "\n" + body;
    }

    private static List<string> Check(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(Program(code), new CSharpParseOptions(LanguageVersion.Latest));
        // A block that only declares types is a sketch of a library type, not a program.
        var isSketch = !tree.GetRoot().DescendantNodes().OfType<GlobalStatementSyntax>().Any();
        var compilation = CSharpCompilation.Create(
            "ReadmeSnippet", [tree], References(),
            new CSharpCompilationOptions(
                isSketch ? OutputKind.DynamicallyLinkedLibrary : OutputKind.ConsoleApplication,
                nullableContextOptions: NullableContextOptions.Enable));

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())
            .ToList();
        if (errors.Count == 0 && isSketch)
            errors.AddRange(SketchMismatches(compilation, tree));
        return errors;
    }

    // Each type the sketch declares must be a type the library exports, and each public member it shows must exist on
    // that type with the same signature. A sketch may leave members out; it may not show ones that are not there.
    private static IEnumerable<string> SketchMismatches(CSharpCompilation compilation, SyntaxTree tree)
    {
        var model = compilation.GetSemanticModel(tree);
        foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol sketch)
                continue;

            var real = compilation.GetTypeByMetadataName($"WebLookup.{sketch.MetadataName}");
            if (real is null || real.DeclaredAccessibility != Accessibility.Public)
            {
                yield return $"the sketch declares {sketch.Name}, which the library does not export";
                continue;
            }

            if (sketch.TypeKind != real.TypeKind || sketch.IsRecord != real.IsRecord)
                yield return $"{sketch.Name}: the sketch is a {Kind(sketch)}, the library's type is a {Kind(real)}";

            foreach (var member in sketch.GetMembers().Where(m => !m.IsImplicitlyDeclared && m.DeclaredAccessibility == Accessibility.Public))
            {
                var signature = Signature(member);
                if (signature is null)
                    continue;

                var actual = real.GetMembers(member.Name).Select(Signature).OfType<string>().ToList();
                if (!actual.Contains(signature, StringComparer.Ordinal))
                {
                    yield return actual.Count == 0
                        ? $"{sketch.Name}.{member.Name}: the library's type has no such member"
                        : $"{sketch.Name}.{member.Name}: the sketch shows `{signature}`, the library has `{string.Join("` / `", actual)}`";
                }
            }
        }
    }

    private static string Kind(INamedTypeSymbol type) => type.IsRecord ? "record" : type.TypeKind.ToString().ToLowerInvariant();

    private static string? Signature(ISymbol member) => member switch
    {
        IPropertySymbol p => $"{(p.IsRequired ? "required " : "")}{p.Type.ToDisplayString(SignatureFormat)} {p.Name} {{ "
                             + (p.GetMethod is null ? "" : "get; ")
                             + (p.SetMethod is null ? "" : p.SetMethod.IsInitOnly ? "init; " : "set; ") + "}",
        IMethodSymbol { MethodKind: MethodKind.Ordinary } m =>
            $"{m.ReturnType.ToDisplayString(SignatureFormat)} {m.Name}("
            + string.Join(", ", m.Parameters.Select(p => p.Type.ToDisplayString(SignatureFormat) + " " + p.Name
                                                         + (p.HasExplicitDefaultValue ? " = " + (p.ExplicitDefaultValue ?? "default") : "")))
            + ")",
        _ => null,
    };

    private static List<MetadataReference> References()
    {
        foreach (var name in AssembliesToLoad)
            Assembly.Load(name);

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted)
            paths.UnionWith(trusted.Split(Path.PathSeparator).Where(p => p.Length > 0));
        paths.UnionWith(AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.Location.Length > 0)
            .Select(a => a.Location));
        return paths.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WebLookup.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("WebLookup.slnx not found above the test output directory");
    }
}
