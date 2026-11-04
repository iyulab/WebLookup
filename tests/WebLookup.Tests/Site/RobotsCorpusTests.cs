using WebLookup.Site;

namespace WebLookup.Tests.Site;

/// <summary>
/// Asks <see cref="RobotsInfo"/> every question in <c>robots-corpus.tsv</c>, a file kept byte for byte in another
/// library's tests that reads robots.txt too, so the two answer one set of questions. A row names what RFC 9309 says and
/// what this implementation answers; where they differ the row says whether that is a choice or a defect still to fix.
/// </summary>
public class RobotsCorpusTests
{
    private sealed record Row(string Id, string Robots, string Agent, string Path, string Expected, string WebFlux, string WebLookup, string Note);

    private static readonly string[] Answers = ["allow", "disallow"];
    private static readonly string[] Behaviours = ["=", "defect", "allow", "disallow"];

    private static readonly Lazy<List<Row>> Corpus = new(Load);

    public static TheoryData<string> Ids()
    {
        var ids = new TheoryData<string>();
        foreach (var row in Corpus.Value)
            ids.Add(row.Id);
        return ids;
    }

    private static List<Row> Load()
    {
        var rows = new List<Row>();
        foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Site", "robots-corpus.tsv")))
        {
            if (line.Length == 0 || line[0] == '#' || line.StartsWith("id\t", StringComparison.Ordinal))
                continue;

            var cells = line.Split('\t');
            Assert.True(cells.Length == 8, $"row '{cells[0]}' has the columns id, robots, agent, path, expected, webflux, weblookup, note");
            rows.Add(new Row(cells[0], cells[1].Replace("\\r", "\r").Replace("\\n", "\n"), cells[2], cells[3], cells[4], cells[5], cells[6], cells[7]));
        }

        return rows;
    }

    private static string Opposite(string answer) => answer == "allow" ? "disallow" : "allow";

    private static string Answer(Row row, string behaviour) => behaviour switch
    {
        "=" => row.Expected,
        "defect" => Opposite(row.Expected),
        _ => behaviour,
    };

    [Theory]
    [MemberData(nameof(Ids))]
    public void Answers_what_the_corpus_records(string id)
    {
        var row = Corpus.Value.Single(r => r.Id == id);

        var allowed = RobotsParser.Parse(row.Robots).IsAllowed(row.Path, row.Agent);

        Assert.True(
            (allowed ? "allow" : "disallow") == Answer(row, row.WebLookup),
            $"row '{id}' (weblookup column '{row.WebLookup}', RFC 9309 answers '{row.Expected}') got '{(allowed ? "allow" : "disallow")}'");
    }

    [Fact]
    public void The_corpus_is_not_empty()
    {
        // A positive control: a rename or a missing copy would otherwise leave the theory with no rows and a green run.
        Assert.True(Corpus.Value.Count > 40);
    }

    [Fact]
    public void A_row_that_differs_from_the_RFC_says_why()
    {
        foreach (var row in Corpus.Value)
        {
            Assert.Contains(row.Expected, Answers);

            foreach (var behaviour in new[] { row.WebFlux, row.WebLookup })
            {
                Assert.Contains(behaviour, Behaviours);
                Assert.False(behaviour == row.Expected, $"Row '{row.Id}' writes the RFC answer '{behaviour}' instead of '='.");
            }

            var differs = row.WebFlux != "=" || row.WebLookup != "=";
            if (!differs)
                continue;

            var defect = row.WebFlux == "defect" || row.WebLookup == "defect";
            Assert.StartsWith(defect ? "defect:" : "choice:", row.Note, StringComparison.Ordinal);
        }
    }
}
