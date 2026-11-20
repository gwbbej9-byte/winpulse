using WinPulse;
using Xunit;

namespace WinPulse.Tests;

public class ProcessSamplerTests
{
    [Fact]
    public void Tick_DoesNotThrow()
    {
        var sampler = new ProcessSampler();
        sampler.Tick();
        sampler.Tick();
    }

    [Fact]
    public void Tick_SetsMachineName()
    {
        var sampler = new ProcessSampler();
        sampler.Tick();
        Assert.Equal(Environment.MachineName, sampler.Host.MachineName);
        Assert.False(string.IsNullOrWhiteSpace(sampler.Host.MachineName));
    }

    [Fact]
    public void Tick_FirstHostCpuNaN_SecondIsNumber()
    {
        var sampler = new ProcessSampler();
        sampler.Tick();
        Assert.True(double.IsNaN(sampler.Host.CpuPercent));

        Thread.Sleep(50);
        sampler.Tick();
        Assert.False(double.IsNaN(sampler.Host.CpuPercent));
        Assert.InRange(sampler.Host.CpuPercent, 0.0, 100.0);
    }

    [Fact]
    public void Tick_RamFromGlobalMemoryStatusEx()
    {
        var sampler = new ProcessSampler();
        sampler.Tick();
        Assert.True(sampler.Host.RamTotalBytes > 0);
        Assert.True(sampler.Host.RamUsedBytes <= sampler.Host.RamTotalBytes);
    }

    [Fact]
    public void Tick_ListsProcessesIncludingPid()
    {
        var sampler = new ProcessSampler();
        sampler.Tick();
        Assert.NotEmpty(sampler.Processes);
        Assert.All(sampler.Processes, p =>
        {
            Assert.False(string.IsNullOrEmpty(p.Name));
            Assert.True(p.Pid >= 0);
            Assert.True(p.WorkingSetBytes >= 0);
            Assert.True(p.PeakWorkingSetBytes >= 0);
        });
        Assert.Contains(sampler.Processes, p => p.PeakWorkingSetBytes > 0);
    }
}
