using System.Reflection;
using LgymApi.IntegrationTests.Authorization;

namespace LgymApi.IntegrationTests;

public sealed partial class AuthQ01FixtureTests
{
    [Test]
    public void AuthQ01CoverageMap_IsClosedAndExecutable()
    {
        var root = FindRepositoryRoot();
        var coverage = File.ReadAllLines(Path.Combine(root, "docs", "testing", "auth-q01-api-coverage.md"));
        var matrix = File.ReadAllText(Path.Combine(root, "docs", "validation", "issue-395-validation-matrix.md"));
        var futureTasks = new Dictionary<string, int>
        {
            ["AUTH-A01"] = 213, ["AUTH-A02"] = 214, ["AUTH-A03"] = 215,
            ["AUTH-A04"] = 216, ["AUTH-A05"] = 217, ["AUTH-A06"] = 218,
            ["AUTH-A07"] = 219, ["AUTH-A08"] = 220, ["AUTH-A09"] = 221,
            ["AUTH-A11"] = 223, ["AUTH-W05"] = 228, ["AUTH-Q03"] = 238,
            ["AUTH-Q04"] = 239
        };
        var rows = coverage
            .Where(line => line.StartsWith("| AC", StringComparison.Ordinal) && line.Length > 4 && char.IsDigit(line[4]))
            .Select(line => line.Split('|', StringSplitOptions.TrimEntries))
            .ToArray();
        rows.Should().HaveCount(20);
        rows.Select(row => row[1]).Should().Equal(Enumerable.Range(1, 20).Select(number => $"AC{number:00}"));

        foreach (var row in rows)
        {
            row.Should().HaveCount(8);
            var surface = row[3].Trim('`');
            var status = row[4];
            var locator = row[5].Trim('`');
            row[6].Should().NotBeNullOrWhiteSpace("every row needs a repository owner and scope");
            if (surface.StartsWith("future: ", StringComparison.Ordinal))
            {
                status.Should().Be("FUTURE", "an unshipped surface cannot be current evidence");
            }
            else
            {
                matrix.Should().Contain($"| {surface} |", "live surfaces must come from K01");
            }

            switch (status)
            {
                case "CURRENT":
                    var separator = locator.LastIndexOf('.');
                    separator.Should().BeGreaterThan(0);
                    var method = typeof(AuthQ01FixtureTests).Assembly.GetTypes()
                        .Where(type => type.Name == locator[..separator])
                        .SelectMany(type => type.GetMethods())
                        .Single(methodInfo => methodInfo.Name == locator[(separator + 1)..]);
                    method.GetCustomAttributes().Any(attribute =>
                        attribute is TestAttribute or TestCaseAttribute or TestCaseSourceAttribute).Should().BeTrue();
                    method.GetCustomAttributes<AuthorizationEvidenceAttribute>()
                        .Should().Contain(attribute => attribute.Route == surface,
                            "the executable case must be tied to this route, not just exist");
                    break;
                case "FUTURE":
                    var key = locator.Split(" / ")[0];
                    futureTasks.Should().ContainKey(key);
                    var issue = $"LGYM-{futureTasks[key]}";
                    locator.Should().Be($"{key} / [{issue}](https://linear.app/lgym/issue/{issue})");
                    break;
                default:
                    Assert.Fail($"Unknown coverage disposition '{status}'.");
                    break;
            }
        }
    }
}
