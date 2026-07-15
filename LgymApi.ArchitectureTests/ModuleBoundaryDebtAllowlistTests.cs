namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class ModuleBoundaryDebtAllowlistTests
{
    private const string GuardId = "ModuleDependencyGuardTests";

    [Test]
    public void Allowlist_Registry_Remains_Centralized_And_Populated_With_Explicit_Current_Debt_Entries()
    {
        var entries = ModuleBoundaryDebtAllowlistRegistry.AllEntries;

        Assert.Multiple(() =>
        {
            Assert.That(entries, Is.Not.Empty, "The shrink-only allowlist should contain the currently approved explicit debt entries.");
            Assert.That(entries.Select(entry => entry.Key.GuardId).Distinct(StringComparer.Ordinal).Count(), Is.GreaterThanOrEqualTo(1));
            Assert.That(entries.All(entry => !string.IsNullOrWhiteSpace(entry.Key.Rationale)), Is.True, "Every centralized debt entry must stay reviewable with a rationale.");
            Assert.That(entries.Select(entry => entry.IdentityKey).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(entries.Count), "Centralized debt entries must stay exact and non-duplicated.");
        });
    }

    [Test]
    public void Allowlist_Evaluation_Allows_Exact_Normalized_Current_Match()
    {
        var entry = new ModuleBoundaryDebtEntry(
            ModuleBoundaryDebtKey.Create(
                GuardId,
                " Nutrition ",
                " User ",
                " LgymApi.Application\\Nutrition\\Plans\\PlanService.cs ",
                " LgymApi.Application\\User\\IUserService.cs ",
                "issue-379 debt"));

        var observedViolation = new ModuleBoundaryObservedViolation(
            GuardId,
            "Nutrition",
            "User",
            "LgymApi.Application/Nutrition/Plans/PlanService.cs",
            "LgymApi.Application/User/IUserService.cs");

        var evaluation = ModuleBoundaryDebtAllowlistEvaluator.Evaluate([entry], [observedViolation]);

        Assert.That(evaluation.IsSuccess, Is.True, evaluation.BuildFailureMessage());
    }

    [Test]
    public void Allowlist_Evaluation_Fails_When_A_Live_Violation_Is_Not_Exactly_Allowlisted()
    {
        var observedViolation = new ModuleBoundaryObservedViolation(
            GuardId,
            "Nutrition",
            "User",
            "LgymApi.Application/Nutrition/Plans/PlanService.cs",
            "LgymApi.Application/User/IUserService.cs");

        var evaluation = ModuleBoundaryDebtAllowlistEvaluator.Evaluate([], [observedViolation], GuardId);

        Assert.Multiple(() =>
        {
            Assert.That(evaluation.IsSuccess, Is.False);
            Assert.That(evaluation.UnexpectedViolations, Has.Count.EqualTo(1));
            Assert.That(evaluation.BuildFailureMessage(), Does.Contain("New module-boundary violations"));
            Assert.That(evaluation.BuildFailureMessage(), Does.Contain("Rule: ModuleDependencyGuardTests"));
            Assert.That(evaluation.BuildFailureMessage(), Does.Contain("Source module: Nutrition"));
            Assert.That(evaluation.BuildFailureMessage(), Does.Contain("Target module: User"));
            Assert.That(evaluation.BuildFailureMessage(), Does.Contain("Source symbol/file: LgymApi.Application/Nutrition/Plans/PlanService.cs"));
        });
    }

    [Test]
    public void Allowlist_Evaluation_Fails_When_An_Allowlist_Entry_Becomes_Stale()
    {
        var entry = new ModuleBoundaryDebtEntry(
            ModuleBoundaryDebtKey.Create(
                GuardId,
                "Nutrition",
                "User",
                "LgymApi.Application/Nutrition/Plans/PlanService.cs",
                "LgymApi.Application/User/IUserService.cs",
                "issue-379 debt"));

        var evaluation = ModuleBoundaryDebtAllowlistEvaluator.Evaluate([entry], [], GuardId);

        Assert.Multiple(() =>
        {
            Assert.That(evaluation.IsSuccess, Is.False);
            Assert.That(evaluation.StaleEntries, Has.Count.EqualTo(1));
            Assert.That(evaluation.BuildFailureMessage(), Does.Contain("Stale module-boundary allowlist entries"));
            Assert.That(evaluation.BuildFailureMessage(), Does.Contain("Rule: ModuleDependencyGuardTests"));
            Assert.That(evaluation.BuildFailureMessage(), Does.Contain("Source module: Nutrition"));
            Assert.That(evaluation.BuildFailureMessage(), Does.Contain("Rationale: issue-379 debt"));
        });
    }

    [Test]
    public void Allowlist_Evaluation_Fails_For_Speculative_Or_Duplicate_Identity_Entries()
    {
        var firstEntry = new ModuleBoundaryDebtEntry(
            ModuleBoundaryDebtKey.Create(
                GuardId,
                "Nutrition",
                "User",
                "LgymApi.Application/Nutrition/Plans/PlanService.cs",
                "LgymApi.Application/User/IUserService.cs",
                "issue-379 debt"));

        var speculativeDuplicate = new ModuleBoundaryDebtEntry(
            ModuleBoundaryDebtKey.Create(
                GuardId,
                "Nutrition",
                "User",
                "LgymApi.Application/Nutrition/Plans/PlanService.cs",
                "LgymApi.Application/User/IUserService.cs",
                "future debt"));

        var act = () => ModuleBoundaryDebtAllowlistEvaluator.Evaluate([firstEntry, speculativeDuplicate], [], GuardId);

        Assert.That(
            act,
            Throws.TypeOf<AssertionException>()
                .With.Message.Contains("duplicate identity matches"));
    }

    [Test]
    public void Allowlist_Registry_Assertion_Uses_The_Centralized_Exact_Match_Path()
    {
        var act = () => ModuleBoundaryDebtAllowlistRegistry.AssertNoUnexpectedViolations(
            GuardId,
            [
                new ModuleBoundaryObservedViolation(
                    GuardId,
                    "Nutrition",
                    "User",
                    "LgymApi.Application/Nutrition/Plans/PlanService.cs",
                    "LgymApi.Application/User/IUserService.cs")
            ]);

        Assert.That(
            act,
            Throws.TypeOf<AssertionException>()
                .With.Message.Contains("Module-boundary shrink-only debt allowlist failed"));
    }
}
