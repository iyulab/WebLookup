using System.Reflection;
using System.Text.RegularExpressions;
using Iyu.Conventions.Testing.Snippets;
using Microsoft.CodeAnalysis;

namespace WebLookup.Tests;

/// <summary>
/// Compiles every C# block in README.md against the current assembly. A compiler checks the receiver, the arguments, the
/// return types a block goes on to use, and the namespaces it needs.
/// </summary>
/// <remarks>
/// Each block is compiled as a top-level program by <see cref="SnippetCompiler"/>: its own usings, the implicit usings, the
/// README's own using, and the stand-ins below when the block uses the name without declaring it — values a reader already
/// has from the surrounding text (the service collection, a provider), not part of what the block shows.
/// A block that only declares types is the API Reference's listing of a library type. It is not compiled — compiled, it
/// would declare a second type of that name and check nothing — but compared with the real type
/// (<see cref="SnippetCompiler.CheckListings"/>): each member it shows must exist with the same signature, accessors and
/// parameter names, and the type must be one the library exports, of the same kind (<see cref="SnippetCompiler.StrictListings"/>:
/// every type-only block in this README is an API reference).
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
        CheckListings = true,
        StrictListings = true,
    };

    private static readonly IReadOnlyList<DocSnippet> Snippets = DocSnippets.Read(Path.Combine(RepoRoot(), "README.md"), "README.md");

    public static TheoryData<string> Blocks() => new(Snippets.Select(s => s.Key));

    [Theory]
    [MemberData(nameof(Blocks))]
    public void ReadmeBlock_Compiles(string key)
    {
        var snippet = Snippets.Single(s => s.Key == key);
        if (Fragments.IsFragment(snippet))
            return;

        var errors = Compiler.Compile(snippet.Code);

        Assert.True(errors.Count == 0,
            $"{key} does not match the current API:\n" + string.Join("\n", errors) + "\n--- compiled as ---\n" + Compiler.Program(snippet.Code));
    }

    [Fact]
    public void EveryReadmeBlock_IsFoundAndFragmentsNameRealHeadings() => Fragments.ShouldMatch([.. Snippets], atLeast: 16);

    /// <summary>
    /// The API Reference's listings are recognised as listings (and so compared with the real types), not compiled as the
    /// reader's own types — a floor, so a listing the checker stops recognising shows here.
    /// </summary>
    [Fact]
    public void ApiReferenceListings_AreCheckedAgainstTheRealTypes()
    {
        var document = Compiler.CompileDocument([.. Snippets]);

        Assert.True(document.ListedTypes >= 5, $"expected the API Reference's five listings to be checked, found {document.ListedTypes}");
    }

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
        var errors = Compiler.Compile("""
            var client = new WebSearchClient();
            await client.SearchAllTheThingsAsync("query");
            """);

        Assert.NotEmpty(errors);
    }

    /// <summary>Positive control: a listing that shows a member the real type does not have is rejected.</summary>
    [Fact]
    public void Listing_RejectsAMemberTheTypeDoesNotHave()
    {
        var errors = Compiler.Compile("""
            public record SearchResult
            {
                public required string Url { get; init; }
                public double Score { get; init; }
            }
            """);

        Assert.Contains(errors, e => e.Id == "LISTING001" && e.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("Score", StringComparison.Ordinal));
    }

    /// <summary>Positive control: a listing that shows a member with other accessors is rejected (the option is settable).</summary>
    [Fact]
    public void Listing_RejectsAMemberWithAnotherSignature()
    {
        var errors = Compiler.Compile("""
            public class WebSearchOptions
            {
                public int MaxResultsPerProvider { get; init; } = 10;
            }
            """);

        Assert.Contains(errors, e => e.Id == "LISTING002" && e.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("MaxResultsPerProvider", StringComparison.Ordinal));
    }

    /// <summary>Positive control: a listing of a type the library does not export is rejected.</summary>
    [Fact]
    public void Listing_RejectsATypeTheLibraryDoesNotExport()
    {
        var errors = Compiler.Compile("""
            public record SearchResponse
            {
                public string? Url { get; init; }
            }
            """);

        Assert.Contains(errors, e => e.Id == "LISTING004" && e.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("SearchResponse", StringComparison.Ordinal));
    }

    /// <summary>Positive control: a listing that calls a record a class is rejected.</summary>
    [Fact]
    public void Listing_RejectsATypeOfAnotherKind()
    {
        var errors = Compiler.Compile("""
            public class SearchResult
            {
                public required string Url { get; init; }
            }
            """);

        Assert.Contains(errors, e => e.Id == "LISTING004");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WebLookup.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("WebLookup.slnx not found above the test output directory");
    }
}
