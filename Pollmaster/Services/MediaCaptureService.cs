using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Pollmaster.Services;

/// <summary>
/// Default <see cref="IMediaCaptureService"/>. Captures land in
/// <c>FileSystem.AppDataDirectory/captures/</c> so we never need WRITE_EXTERNAL_STORAGE on
/// Android; the share sheet then lets the user export to gallery / chats / e-mail.
/// </summary>
public sealed class MediaCaptureService : IMediaCaptureService
{
    private const string CaptureFolderName = "captures";

    private readonly ILogger<MediaCaptureService> _logger;

    /// <summary>Construct the capture service.</summary>
    /// <param name="logger">Logger.</param>
    public MediaCaptureService(ILogger<MediaCaptureService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string> SaveAndShareAsync(string dataUri, string fileExtension, string shareTitle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileExtension);

        var bytes = DecodeDataUri(dataUri);
        var path = await PersistAsync(bytes, fileExtension).ConfigureAwait(false);
        _logger.LogInformation("Saved capture to {Path} ({Size} bytes)", path, bytes.Length);

        try
        {
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = shareTitle,
                File = new ShareFile(path)
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The file is still on disk — share failures should not lose the capture.
            _logger.LogWarning(ex, "Share sheet failed for {Path}", path);
        }
        return path;
    }

    private static byte[] DecodeDataUri(string dataUri)
    {
        var commaIndex = dataUri.IndexOf(',');
        var base64 = commaIndex >= 0 ? dataUri[(commaIndex + 1)..] : dataUri;
        return Convert.FromBase64String(base64);
    }

    private static async Task<string> PersistAsync(byte[] bytes, string fileExtension)
    {
        var dir = Path.Combine(FileSystem.AppDataDirectory, CaptureFolderName);
        Directory.CreateDirectory(dir);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var fileName = $"pollmaster-{stamp}{fileExtension}";
        var fullPath = Path.Combine(dir, fileName);
        await File.WriteAllBytesAsync(fullPath, bytes).ConfigureAwait(false);
        return fullPath;
    }
}
