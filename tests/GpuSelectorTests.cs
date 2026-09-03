namespace SimpleNvidiaUndervolt.Tests;

/// <summary>Tests for the <c>--gpu</c> selector: how it parses, resolves against the enumeration,
/// and re-emits into a saved shortcut.</summary>
public class GpuSelectorTests
{
    private static GpuSelector Parse(params string[] flags)
        => GpuSelector.Parse(Args.GpuBound.Parse(new[] { "status" }.Concat(flags).ToArray()));

    [Fact]
    public void Absent_IsIndexZero_AndNotGiven()
    {
        GpuSelector selector = Parse();

        Assert.False(selector.Given);
        Assert.False(selector.IsAll);
        Assert.Equal(new[] { 0 }, selector.IndexesOn(gpuCount: 2));
        Assert.Empty(selector.ToArgs());
    }

    [Fact]
    public void Index_SelectsThatGpu_AndRoundTripsIntoAShortcut()
    {
        GpuSelector selector = Parse("--gpu", "1");

        Assert.True(selector.Given);
        Assert.Equal(new[] { 1 }, selector.IndexesOn(gpuCount: 2));
        Assert.Equal(new[] { "--gpu", "1" }, selector.ToArgs());
        Assert.Equal("GPU 1", selector.Describe());
    }

    [Fact]
    public void All_ResolvesToEveryEnumeratedGpu()
    {
        GpuSelector selector = Parse("--gpu", "ALL");

        Assert.True(selector.IsAll);
        Assert.Equal(new[] { 0, 1, 2 }, selector.IndexesOn(gpuCount: 3));
        Assert.Equal(new[] { "--gpu", "all" }, selector.ToArgs()); // normalized for the link
        Assert.Equal("all GPUs", selector.Describe());
    }

    [Theory]
    [InlineData("x")]
    [InlineData("-1")]
    [InlineData("1.5")]
    public void RejectsAnythingButAnIndexOrAll(string value)
    {
        Assert.Throws<CliError>(() => Parse("--gpu", value));
    }

    [Fact]
    public void MissingGpu_HintsAtClear_OnlyForTheStoredSet()
    {
        // A stored index with no card is a persisted tuning that outlived its GPU.
        string stored = GpuSelector.Stored(new[] { 0, 1 }).MissingGpu(1, gpuCount: 1).Message;
        string requested = Parse("--gpu", "1").MissingGpu(1, gpuCount: 1).Message;

        Assert.Contains("GPU 1 doesn't exist", stored);
        Assert.Contains("clear --gpu 1", stored);
        Assert.Contains("GPU 1 doesn't exist", requested);
        Assert.DoesNotContain("clear", requested);
    }

    [Fact]
    public void Stored_CarriesThePersistedSet_WithoutAShortcutFlag()
    {
        GpuSelector selector = GpuSelector.Stored(new[] { 0, 2 });

        Assert.False(selector.Given);
        Assert.Equal(new[] { 0, 2 }, selector.IndexesOn(gpuCount: 3));
        Assert.Empty(selector.ToArgs());
    }

    [Fact]
    public void RequireOneForExport_RejectsAllOnlyWithAFile()
    {
        Parse("--gpu", "all").RequireOneForExport("--out-tuning-file", null);
        Parse("--gpu", "1").RequireOneForExport("--out-tuning-file", "t.json");

        Assert.Throws<CliError>(
            () => Parse("--gpu", "all").RequireOneForExport("--out-tuning-file", "t.json"));
    }
}
