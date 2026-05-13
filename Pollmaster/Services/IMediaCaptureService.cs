namespace Pollmaster.Services;

/// <summary>
/// Persists screenshots and screen recordings produced by the WebView capture interop and
/// hands them to the platform share sheet so the user can pick a final destination
/// (gallery, chat, e-mail) without granting the app any extra storage permissions.
/// </summary>
public interface IMediaCaptureService
{
    /// <summary>Save a data URI payload produced by JavaScript and open the share sheet.</summary>
    /// <param name="dataUri">Data URI of the form <c>data:&lt;mime&gt;;base64,&lt;payload&gt;</c>.</param>
    /// <param name="fileExtension">File extension including the leading dot (e.g. ".png", ".webm").</param>
    /// <param name="shareTitle">Title shown on the platform share dialog.</param>
    /// <returns>Absolute path of the saved file.</returns>
    Task<string> SaveAndShareAsync(string dataUri, string fileExtension, string shareTitle);
}
