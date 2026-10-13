using System.Reflection;
using System.Text.RegularExpressions;
using DotNetBoostSample.Tests.Integration;
using Xunit;

namespace DotNetBoostSample.Tests;

/// <summary>
/// The numbers the documentation states are the numbers that are true.
///
/// <para><b>Why this exists.</b> Five of the last six commits on this repository were about counts,
/// and one of them is titled "Fix a test count the last commit left stale" — then left three more
/// behind. Every count in these documents is load-bearing in the same way the route inventory is:
/// the README's argument is that it is checked rather than claimed, and a number that has quietly
/// stopped being true is the cheapest possible way to lose that argument. Counting by hand after
/// each change is what has not worked, three times.</para>
///
/// <para><b>What it reads.</b> The documents themselves, by regular expression, and the test
/// assembly, by reflection — so a failure names the sentence and the two numbers rather than
/// leaving someone to work out which document went stale.</para>
///
/// <para>These four tests are part of every total below, which is the property worth having: the
/// count includes the thing doing the counting, so there is no version of this file that passes
/// while being wrong about itself.</para>
///
/// <para>No container runtime is needed. These are the documentation's own tests, and they must run
/// for someone who has only cloned the repository — that reader is exactly who a stale number
/// misleads.</para>
/// </summary>
public class DocumentedCountTests
{
    /// <summary>
    /// Every test this assembly publishes, as (the class that runs it, whether it needs Docker).
    ///
    /// <para>Discovered the way xUnit discovers them: public concrete classes, public instance
    /// methods carrying a <see cref="FactAttribute"/>, <b>including inherited ones</b>. That last
    /// part is the whole reason this cannot be a grep — the integration classes are abstract and
    /// bound to a fixture by two sealed subclasses each, so one method in the source is two tests
    /// in the run. 77 attributes, 121 tests.</para>
    /// </summary>
    private static readonly (Type Class, MethodInfo Method, bool NeedsDocker)[] Discovered =
    [
        .. from type in typeof(DocumentedCountTests).Assembly.GetTypes()
           where type is { IsClass: true, IsAbstract: false, IsPublic: true }
           from method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
           let fact = method.GetCustomAttribute<FactAttribute>()
           where fact is not null
           select (type, method, fact is RequiresDockerFactAttribute)
    ];

    /// <summary>
    /// <c>ARCHITECTURE-SLICES.md</c>'s status line: "all N tests pass".
    ///
    /// <para>That document states its other figures as snapshots — "1,788 lines of C# in 13 files
    /// when this was written" — but this one is written in the present tense and read as a status,
    /// so it is the one that has to keep up. It was 57 when written, which was correct, and 117 by
    /// the time anyone noticed.</para>
    /// </summary>
    [Fact]
    public void The_total_the_architecture_document_states_is_the_number_of_tests_there_are()
    {
        // A [Theory] is as many tests as it has data rows, and nothing below can see them. There
        // are none today; if one arrives, the arithmetic here has to be made deliberate rather
        // than silently undercounting.
        var theories = Discovered
            .Where(t => t.Method.GetCustomAttribute<TheoryAttribute>() is not null)
            .Select(t => $"{t.Class.Name}.{t.Method.Name}")
            .ToList();

        Assert.True(
            theories.Count == 0,
            $"{string.Join(", ", theories)} is a [Theory], so this count is no longer one test per "
            + "method. Count its data rows here on purpose, or keep the suite on [Fact].");

        // Stated first, discovered second, so a failure reads "the document says 117, there are
        // 121" rather than the other way round.
        Assert.Equal(
            Stated("ARCHITECTURE-SLICES.md", @"all (\d+) tests\s+pass"),
            Discovered.Length);
    }

    /// <summary>
    /// The README's promise to someone without Docker: which tests still run, and how many of them
    /// say why they did not.
    ///
    /// <para>Both numbers, because they are the two halves of one sentence and have drifted
    /// independently — it read "23 fast tests" and "the other 34 as skipped" when the truth was 22
    /// and 95, one of them off by one and the other by an era.</para>
    /// </summary>
    [Fact]
    public void The_split_the_README_promises_without_a_container_runtime_is_the_real_one()
    {
        var sentence = Match(
            "README.md",
            @"runs the (\d+) fast\s+tests and reports the other (\d+) as skipped");

        Assert.Equal(
            (int.Parse(sentence.Groups[1].Value), int.Parse(sentence.Groups[2].Value)),
            (Discovered.Count(t => !t.NeedsDocker), Discovered.Count(t => t.NeedsDocker)));
    }

    /// <summary>
    /// The two counts the base-language section rests on: how many tests run against each mode, and
    /// how many only the second one can be asked.
    ///
    /// <para>Spelled out in the prose, so they are compared as words — <see cref="InWords"/>. A
    /// count this test cannot spell fails loudly rather than being written back into English
    /// wrongly: the sentence is someone's prose, and only they should rewrite it.</para>
    /// </summary>
    [Fact]
    public void The_per_mode_counts_in_the_README_are_the_ones_that_run()
    {
        var perMode = Discovered.Count(t => t.Class.Name.EndsWith("InDefaultMode", StringComparison.Ordinal));
        var baseOnly = Discovered.Count(t => t.Class.Name == nameof(BaseLanguageTests));

        // The claim is that the SAME tests run twice, so the two halves have to agree before the
        // number in the prose means anything.
        Assert.Equal(
            perMode,
            Discovered.Count(t => t.Class.Name.EndsWith("InBaseLanguageMode", StringComparison.Ordinal)));

        Assert.Equal(
            Match("README.md", @"in both modes\*\*: ([a-z-]+) tests against each").Groups[1].Value,
            InWords(perMode));

        var beside = Match(
            "README.md",
            @"([A-Za-z-]+) tests, beside the ([a-z-]+) the\s+suite runs in each mode");

        Assert.Equal(
            (beside.Groups[1].Value.ToLowerInvariant(), beside.Groups[2].Value),
            (InWords(baseOnly), InWords(perMode)));
    }

    /// <summary>
    /// The heading "Why four projects for N files", against the files those four projects hold.
    ///
    /// <para><c>.cs</c> under <c>src/</c>, which is the metric the heading was written with: it
    /// counted 42 at the commit that wrote it, and 43 after this branch added two files and removed
    /// one. <c>bin</c> and <c>obj</c> are excluded — generated code is not what "four projects for
    /// 43 files" is arguing about.</para>
    /// </summary>
    [Fact]
    public void The_file_count_in_the_README_heading_is_the_number_of_files()
    {
        var files = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Count();

        Assert.Equal(Stated("README.md", @"### Why four projects for (\d+) files"), files);
    }

    // ── Reading the documents ────────────────────────────────────────────────

    /// <summary>
    /// The repository root, found by walking up from the test binary until the solution file is
    /// beside us.
    ///
    /// <para>Rather than a relative path with a known number of <c>..</c> in it, which is a second
    /// thing to get wrong every time the target framework or the output layout moves.</para>
    /// </summary>
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DotNetBoostSample.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException(
            $"No DotNetBoostSample.slnx above {AppContext.BaseDirectory}, so the documents these "
            + "tests read cannot be found. They assume the test binary sits inside the repository.");
    }

    /// <summary>The one number <paramref name="pattern"/> captures, from <paramref name="file"/>.</summary>
    private static int Stated(string file, string pattern) =>
        int.Parse(Match(file, pattern).Groups[1].Value);

    /// <summary>
    /// <paramref name="pattern"/> against <paramref name="file"/>, failing with the pattern itself
    /// when the sentence is no longer there.
    ///
    /// <para>Which is a real outcome and not a test bug: prose gets rewritten. The failure says so,
    /// rather than throwing on an empty match group.</para>
    /// </summary>
    private static Match Match(string file, string pattern)
    {
        var match = Regex.Match(
            File.ReadAllText(Path.Combine(RepositoryRoot, file)), pattern, RegexOptions.None,
            TimeSpan.FromSeconds(5));

        Assert.True(
            match.Success,
            $"{file} no longer contains the sentence this test pins, /{pattern}/. If it was "
            + "rewritten, update the pattern; if the claim was dropped, drop the test with it.");

        return match;
    }

    /// <summary>
    /// A count as the prose spells it: <c>40</c> → <c>forty</c>, <c>15</c> → <c>fifteen</c>.
    ///
    /// <para>Up to ninety-nine, which is as far as these sentences go. Past that it refuses rather
    /// than guessing — a suite that outgrows this is a sentence someone has to rewrite anyway, and
    /// "one hundred and twenty" versus "a hundred and twenty" is not a choice a test should make on
    /// an author's behalf.</para>
    /// </summary>
    private static string InWords(int count)
    {
        string[] units =
        [
            "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
            "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen",
            "eighteen", "nineteen"
        ];

        string[] tens =
            ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

        if (count is < 0 or > 99)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count), count,
                "This spells numbers up to ninety-nine, which is as far as the sentences it checks "
                + "go. Write the new sentence by hand and widen the pattern to match it.");
        }

        if (count < 20) return units[count];

        return count % 10 == 0 ? tens[count / 10] : $"{tens[count / 10]}-{units[count % 10]}";
    }
}
