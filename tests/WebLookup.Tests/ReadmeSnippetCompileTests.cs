using System.Reflection;
using System.Text.RegularExpressions;
using Iyu.Conventions.Testing.Snippets;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace WebLookup.Tests;

/// <summary>
/// Compiles every C# block in README.md against the current assembly. A compiler checks the receiver, the arguments, the
/// return types a block goes on to use, and the namespaces it needs.
/// </summary>
/// <remarks>
/// Each block is compiled as a top-level program by <see cref="SnippetCompiler"/>: its own usings, the implicit usings, the
/// README's own using, and the stand-ins below when the block uses the name without declaring it — values a reader already
/// has from the surrounding text (the service collection, a provider), not part of what the block shows.
/// A block that only declares types is the API Reference's sketch of a library type: it compiles as a library, and each
/// member it shows must exist on the real type with the same signature — a sketch compiles on its own whatever it says,
/// so the compiler alone would not notice it drifting from the library. That comparison is this repository's own check.
/// </remarks>
public class ReadmeSnippetCompileTests
{
    // A block that is deliberately not a program (pseudocode), by the heading it sits under, with the reason. Shrink this,
    // never grow it silently.
    private static readonly SnippetRoster Fragments = new(new Dictionary<string, string>(StringComparer.Ordinal));

    // The implicit usings of an SDK console project (ImplicitUsings=enable).
    private static readonly string[] CommonUsings =
        ["System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Net.Http", "System.Threading", "System.Threading.Tasks"];

    // The README reads as one guide: its Quick Start states this using once, and every later block assumes it.
    // ReadmeUsings_AreTheOnesTheQuickStartStates keeps this list and the README's statement the same.
    private static readonly string[] ReadmeUsings = ["WebLookup"];

    private static readonly Dictionary<string, string> StandIns = new(StringComparer.Ordinal)
    {
        ["services"] = "Microsoft.Extensions.DependencyInjection.IServiceCollection services = null!;",
        // The "Filter URLs" block continues the "Explore a site" block.
        ["explorer"] = "WebLookup.SiteExplorer explorer = new();",
        // The WebSearchOptions block configures a client over a provider built earlier.
        ["provider"] = "WebLookup.ISearchProvider provider = null!;",
    };

    private static readonly IReadOnlyList<MetadataReference> References = SnippetCompiler.ReferencesFor(
        typeof(WebSearchClient).Assembly,
        Assembly.Load("Microsoft.Extensions.DependencyInjection"),
        Assembly.Load("Microsoft.Extensions.DependencyInjection.Abstractions"));

    private static readonly SnippetCompiler Compiler = new(References)
    {
        Usings = [.. CommonUsings, .. ReadmeUsings],
        StandIns = StandIns,
    };

    private static readonly IReadOnlyList<DocSnippet> Snippets = DocSnippets.Read(Path.Combine(RepoRoot(), "README.md"), "README.md");

    private static readonly SymbolDisplayFormat SignatureFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static TheoryData<string> Blocks() => new(Snippets.Select(s => s.Key));

    [Theory]
    [MemberData(nameof(Blocks))]
    public void ReadmeBlock_Compiles(string key)
    {
        var snippet = Snippets.Single(s => s.Key == key);
        if (Fragments.IsFragment(snippet))
            return;

        var errors = Check(snippet.Code);

        Assert.True(errors.Count == 0,
            $"{key} does not match the current API:\n" + string.Join("\n", errors) + "\n--- compiled as ---\n" + Compiler.Program(snippet.Code));
    }

    [Fact]
    public void EveryReadmeBlock_IsFoundAndFragmentsNameRealHeadings() => Fragments.ShouldMatch([.. Snippets], atLeast: 16);

    [Fact]
    public void ReadmeUsings_AreTheOnesTheQuickStartStates()
    {
        var stated = Snippets[0].Code.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
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

    // The kit compiles; a block that compiled and only declares types is then compared with the library's types.
    private static List<string> Check(string code)
    {
        var errors = Compiler.Compile(code).Select(d => d.ToString()).ToList();
        if (errors.Count > 0)
            return errors;

        var tree = CSharpSyntaxTree.ParseText(Compiler.Program(code), new CSharpParseOptions(LanguageVersion.Latest));
        if (tree.GetRoot().DescendantNodes().OfType<GlobalStatementSyntax>().Any())
            return errors;

        var compilation = CSharpCompilation.Create(
            "ReadmeSketch", [tree], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        return [.. SketchMismatches(compilation, tree)];
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

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WebLookup.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("WebLookup.slnx not found above the test output directory");
    }
}
