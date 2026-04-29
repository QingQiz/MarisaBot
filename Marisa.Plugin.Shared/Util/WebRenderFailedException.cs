namespace Marisa.Plugin.Shared.Util;

public sealed class WebRenderFailedException(string privateUrl, string publicUrl, Exception innerException)
    : Exception($"Web render failed for {privateUrl}", innerException)
{
    public string PrivateUrl { get; } = privateUrl;

    public string PublicUrl { get; } = publicUrl;
}