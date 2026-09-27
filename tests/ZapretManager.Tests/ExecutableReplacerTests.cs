using ZapretManager.App.Services;

namespace ZapretManager.Tests;

public sealed class ExecutableReplacerTests
{
    [Fact]
    public void Replace_PutsNewFileInPlaceAndKeepsPreviousForRollback()
    {
        var directory = Directory.CreateTempSubdirectory("zapret-replace-").FullName;
        var target = Path.Combine(directory, "Zapret Manager.exe");
        var update = Path.Combine(directory, "update.exe");
        File.WriteAllText(target, "old");
        File.WriteAllText(update, "new");

        var previous = ExecutableReplacer.Replace(update, target);

        Assert.Equal("new", File.ReadAllText(target));
        Assert.NotNull(previous);
        Assert.Equal("old", File.ReadAllText(previous));

        ExecutableReplacer.RollBack(target, previous);

        Assert.Equal("old", File.ReadAllText(target));
        Assert.False(File.Exists(previous));
    }

    [Fact]
    public void Replace_WhenPreviousLeftoverIsLocked_UsesAnotherName()
    {
        var directory = Directory.CreateTempSubdirectory("zapret-replace-").FullName;
        var target = Path.Combine(directory, "Zapret Manager.exe");
        var update = Path.Combine(directory, "update.exe");
        File.WriteAllText(target, "current");
        File.WriteAllText(update, "new");
        File.WriteAllText(target + ".old", "still running");
        using var lockedLeftover = File.Open(target + ".old", FileMode.Open, FileAccess.Read, FileShare.None);

        var previous = ExecutableReplacer.Replace(update, target);

        Assert.Equal("new", File.ReadAllText(target));
        Assert.NotEqual(target + ".old", previous);
        Assert.Equal("current", File.ReadAllText(previous!));
    }

    [Fact]
    public void Replace_WhenTargetIsMissing_JustCopies()
    {
        var directory = Directory.CreateTempSubdirectory("zapret-replace-").FullName;
        var target = Path.Combine(directory, "Zapret Manager.exe");
        var update = Path.Combine(directory, "update.exe");
        File.WriteAllText(update, "new");

        var previous = ExecutableReplacer.Replace(update, target);

        Assert.Null(previous);
        Assert.Equal("new", File.ReadAllText(target));
    }

    [Fact]
    public void DeleteLeftover_RemovesEveryPreviousCopyButNotTheExecutable()
    {
        var directory = Directory.CreateTempSubdirectory("zapret-replace-").FullName;
        var target = Path.Combine(directory, "Zapret Manager.exe");
        File.WriteAllText(target, "current");
        File.WriteAllText(target + ".old", "a");
        File.WriteAllText(target + ".old-1234", "b");

        ExecutableReplacer.DeleteLeftover(target);

        Assert.Equal([target], Directory.GetFiles(directory));
    }
}
