using WinPulse;
using Xunit;

namespace WinPulse.Tests;

public class FormatUtilTests
{
    [Fact]
    public void WorkingSetMb_Zero()
    {
        Assert.Equal("0.0", FormatUtil.WorkingSetMb(0));
    }

    [Fact]
    public void WorkingSetMb_OneMegabyte()
    {
        Assert.Equal("1.0", FormatUtil.WorkingSetMb(1024 * 1024));
    }

    [Fact]
    public void WorkingSetMb_Fractional()
    {
        Assert.Equal("1.5", FormatUtil.WorkingSetMb(1024 * 1024 + 512 * 1024));
    }

    [Theory]
    [InlineData("chrome", "", true)]
    [InlineData("chrome", "   ", true)]
    [InlineData("chrome", "CHR", true)]
    [InlineData("explorer", "devenv", false)]
    [InlineData("WinPulse", "pulse", true)]
    public void NameContains_Filter(string name, string filter, bool expected)
    {
        Assert.Equal(expected, FormatUtil.NameContains(name, filter));
    }

    [Fact]
    public void CpuPercent_NaN_IsDash()
    {
        Assert.Equal("—", FormatUtil.CpuPercent(double.NaN));
    }

    [Fact]
    public void CpuPercent_Rounded()
    {
        Assert.Equal("12.3", FormatUtil.CpuPercent(12.34));
    }

    [Fact]
    public void ProcessCount_SingularPlural()
    {
        Assert.Equal("1 process", FormatUtil.ProcessCount(1));
        Assert.Equal("8 processes", FormatUtil.ProcessCount(8));
        Assert.Equal("0 processes", FormatUtil.ProcessCount(0));
    }

    [Fact]
    public void RefreshAge_ZeroSeconds()
    {
        Assert.Equal("refreshed 0s ago", FormatUtil.RefreshAge(TimeSpan.Zero));
        Assert.Equal("refreshed 3s ago", FormatUtil.RefreshAge(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void RamGb_Format()
    {
        ulong gb = 1024UL * 1024 * 1024;
        Assert.Equal("8.0 / 16.0 GB", FormatUtil.RamGb(8 * gb, 16 * gb));
    }
}
