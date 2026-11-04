using System.Reflection;
using System.Xml.Linq;

namespace WebLookup.Tests;

/// <summary>
/// The package ships <c>WebLookup.xml</c> so an IDE can show what a type or member is for; it once held only the generated
/// regex members and no public-API documentation at all. The compiler now fails the build for a public member without
/// <c>///</c> (CS1591, warnings are errors); this reads the generated file the package carries and pins what that guarantee
/// means for a consumer: every public type is described, and the file is where the assembly is.
/// </summary>
public class PublicApiDocumentationTests
{
    private static readonly Assembly Library = typeof(SiteExplorer).Assembly;

    private static XDocument Docs()
    {
        var path = Path.ChangeExtension(Library.Location, ".xml");
        Assert.True(File.Exists(path), $"{path} is missing: the documentation file is not generated next to the assembly");
        return XDocument.Load(path);
    }

    [Fact]
    public void EveryPublicType_HasASummaryInTheDocumentationFile()
    {
        var members = Docs().Descendants("member").ToDictionary(m => (string)m.Attribute("name")!, m => m);
        var publicTypes = Library.GetExportedTypes();

        Assert.True(publicTypes.Length >= 24, $"expected at least the 24 public types of 0.3.0, found {publicTypes.Length}");

        var undocumented = publicTypes
            .Where(t => !members.TryGetValue("T:" + t.FullName, out var m) || string.IsNullOrWhiteSpace((string?)m.Element("summary")))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(undocumented);
    }

    [Fact]
    public void TheFileCarriesTheDocumentedEntryPoints()
    {
        var names = Docs().Descendants("member").Select(m => (string)m.Attribute("name")!).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("M:WebLookup.SiteExplorer.GetSitemapAsync(System.Uri,System.Threading.CancellationToken)", names);
        Assert.Contains("M:WebLookup.WebSearchClient.SearchAsync(System.String,System.Threading.CancellationToken)", names);
        Assert.Contains("M:WebLookup.ISearchProvider.SearchAsync(System.String,System.Int32,System.Threading.CancellationToken)", names);
        Assert.Contains("M:WebLookup.RobotsInfo.IsAllowed(System.String,System.String)", names);
    }
}
