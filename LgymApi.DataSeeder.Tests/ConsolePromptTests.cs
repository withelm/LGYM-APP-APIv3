namespace LgymApi.DataSeeder.Tests;

[TestFixture]
[NonParallelizable]
public sealed class ConsolePromptTests
{
    private TextReader? _originalIn;
    private TextWriter? _originalOut;

    [SetUp]
    public void SetUp()
    {
        _originalIn = Console.In;
        _originalOut = Console.Out;
    }

    [TearDown]
    public void TearDown()
    {
        if (_originalIn != null)
        {
            Console.SetIn(_originalIn);
        }

        if (_originalOut != null)
        {
            Console.SetOut(_originalOut);
        }
    }

    [Test]
    public void Confirm_Should_Return_Default_On_Empty_Input()
    {
        Console.SetIn(new StringReader("\n"));
        Console.SetOut(new StringWriter());

        var result = ConsolePrompt.Confirm("Continue?", defaultValue: true);

        result.Should().BeTrue();
    }

    [Test]
    public void Confirm_Should_Handle_Invalid_Then_Yes()
    {
        Console.SetIn(new StringReader("maybe\nY\n"));
        Console.SetOut(new StringWriter());

        var result = ConsolePrompt.Confirm("Continue?", defaultValue: false);

        result.Should().BeTrue();
    }

    [Test]
    public void Choose_Should_Return_Default_On_Empty_Input()
    {
        Console.SetIn(new StringReader("\n"));
        Console.SetOut(new StringWriter());

        var result = ConsolePrompt.Choose("Pick", new[] { "Migrate", "EnsureCreated" }, "Migrate");

        result.Should().Be("Migrate");
    }

    [Test]
    public void Choose_Should_Handle_Invalid_Then_Valid()
    {
        Console.SetIn(new StringReader("wrong\nEnsureCreated\n"));
        Console.SetOut(new StringWriter());

        var result = ConsolePrompt.Choose("Pick", new[] { "Migrate", "EnsureCreated" }, "Migrate");

        result.Should().Be("EnsureCreated");
    }
}
