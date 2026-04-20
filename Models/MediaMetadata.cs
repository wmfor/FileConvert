namespace FileConvert.Models;

public class MediaMetadata
{
    public string? Duration   { get; set; }
    public string? Bitrate    { get; set; }
    public string? FPS        { get; set; }
    public string? SampleRate { get; set; }
    public string? Channels   { get; set; }
    public string? VideoCodec { get; set; }
    public string? AudioCodec { get; set; }
    public string? ColorSpace { get; set; }
    public string? BitDepth   { get; set; }
    public int?    Width      { get; set; }
    public int?    Height     { get; set; }
}
