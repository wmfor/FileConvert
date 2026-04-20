using System;

namespace FileConvert.Models;

public class ConversionHistoryEntry
{
    public DateTime Timestamp   { get; init; } = DateTime.Now;
    public string   InputName   { get; init; } = "";
    public string   FromExt     { get; init; } = "";
    public string   ToExt       { get; init; } = "";
    public bool     Success     { get; init; }
    public long     InputBytes  { get; init; }
    public long     OutputBytes { get; init; }
    public int      ElapsedMs   { get; init; }
    public string?  OutputPath  { get; init; }

    public string DisplayLine =>
        $"{InputName}  →  {ToExt.ToUpper()}";

    public string SizeLine => Success
        ? $"{FmtBytes(InputBytes)} → {FmtBytes(OutputBytes)}"
        : "Failed";

    public string TimeLine =>
        Timestamp.ToString("HH:mm:ss");

    private static string FmtBytes(long b)
    {
        if (b <= 0) return "—";
        if (b < 1_048_576) return $"{b / 1024.0:F1} KB";
        return $"{b / 1_048_576.0:F2} MB";
    }
}
