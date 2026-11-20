
namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class PersistedCommandAuthorizationCatalogTests
{
    [Test]
    public void Catalog_IsClosedAndClassifiesEveryPersistedCommandAsACommittedSystemIntent()
    {
        var entries = PersistedCommandAuthorizationCatalog.Entries;

        Assert.That(entries, Has.Exactly(19).Items);
        Assert.That(entries.Select(entry => entry.CanonicalId), Is.Unique);
        Assert.That(entries.Select(entry => entry.RuntimeCommandType), Is.Unique);
        Assert.That(PersistedCommandAuthorizationCatalog.AuthorizationClass, Is.EqualTo(PersistedCommandAuthorizationClass.CommittedSystemIntent));
        Assert.That((entries).All(entry =>
            !string.IsNullOrWhiteSpace(entry.Owner)
            && entry.SubjectIds.Count != 0
            && entry.RecipientIds.Count != 0
            && entry.ScheduleSites.Count != 0), Is.True);
    }

    [Test]
    public void Catalog_CoversTheClosedRegistryAndEveryDeclaredScheduleSite()
    {
        var repositoryRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var defaultContractsSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "LgymApi.BackgroundWorker",
            "Runtime",
            "CommandContractRegistry.DefaultContracts.cs"));
        var registrySource = defaultContractsSource + File.ReadAllText(Path.Combine(
            repositoryRoot,
            "LgymApi.BackgroundWorker",
            "Runtime",
            "CommandContractRegistry.cs"));

        foreach (var entry in PersistedCommandAuthorizationCatalog.Entries)
        {
            Assert.That(registrySource, Does.Contain(entry.CanonicalId));
            Assert.That(defaultContractsSource, Does.Contain(entry.RuntimeCommandType));

            foreach (var scheduleSite in entry.ScheduleSites)
            {
                var sourcePath = Path.Combine(repositoryRoot, scheduleSite);
                Assert.That(File.Exists(sourcePath), Is.True, $"{entry.RuntimeCommandType} must have its declared schedule site");
                Assert.That(File.ReadAllText(sourcePath), Does.Contain(entry.RuntimeCommandType), $"{scheduleSite} must schedule {entry.RuntimeCommandType}");
            }
        }
    }
}
