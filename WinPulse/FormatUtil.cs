using System.Globalization;

namespace WinPulse;

public static class FormatUtil
{
    public static string WorkingSetMb(long bytes)
    {
        double mb = bytes / (1024.0 * 1024.0);
        if (mb < 0) mb = 0;
        return mb.ToString("0.0", CultureInfo.InvariantCulture);
    }

    public static string CpuPercent(double pct)
    {
        if (double.IsNaN(pct) || double.IsInfinity(pct) || pct < 0)
            return "—";
        if (pct > 100.0)
            pct = 100.0;
        return pct.ToString("0.0", CultureInfo.InvariantCulture);
    }

    public static string RamGb(ulong usedBytes, ulong totalBytes)
    {
        return $"{ToGb(usedBytes):0.0} / {ToGb(totalBytes):0.0} GB";
    }

    public static double ToGb(ulong bytes) => bytes / (1024.0 * 1024.0 * 1024.0);

    public static bool NameContains(string name, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;
        return name.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static string ProcessCount(int n)
    {
        return n == 1 ? "1 process" : $"{n} processes";
    }

    public static string RefreshAge(TimeSpan age)
    {
        int s = (int)Math.Max(0, age.TotalSeconds);
        return s == 0 ? "refreshed 0s ago" : $"refreshed {s}s ago";
    }
}
