namespace XMLLibrary.Core.Cloud;

/// <summary>
/// What the program needs from a cloud storage. Implemented for Google Drive in the app project
/// (<c>GoogleDriveStorage</c> — an adapter over the Google API); the tests use an in-memory fake.
/// </summary>
/// <remarks>Implementations report every provider-specific failure as <see cref="CloudStorageException"/>.</remarks>
public interface ICloudStorage
{
    /// <summary>True when the user is signed in (now or with a saved sign-in from the last run).</summary>
    bool IsSignedIn { get; }

    /// <summary>Lists the files whose names end with one of the extensions, the newest first.</summary>
    /// <param name="extensions">Extensions with the dot: ".xml", ".xsl".</param>
    /// <param name="cancellationToken">Stops the request.</param>
    Task<IReadOnlyList<CloudFile>> ListAsync(IReadOnlyCollection<string> extensions, CancellationToken cancellationToken = default);

    /// <summary>Downloads the whole content of a file.</summary>
    /// <param name="fileId">The id from <see cref="CloudFile.Id"/>.</param>
    /// <param name="cancellationToken">Stops the download.</param>
    Task<byte[]> DownloadAsync(string fileId, CancellationToken cancellationToken = default);

    /// <summary>Creates a new file and returns it.</summary>
    /// <param name="name">The file name with the extension.</param>
    /// <param name="mimeType">The content type, e.g. "application/xml".</param>
    /// <param name="content">The content, read from its current position to the end.</param>
    /// <param name="cancellationToken">Stops the upload.</param>
    Task<CloudFile> UploadAsync(string name, string mimeType, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Forgets the signed-in account, so the next operation asks to sign in again.</summary>
    Task SignOutAsync();
}

/// <summary>A file in the cloud.</summary>
/// <param name="Id">The provider's id of the file.</param>
/// <param name="Name">The file name.</param>
/// <param name="Modified">When the file was last changed, if the provider says.</param>
/// <param name="Size">The size in bytes, if the provider says.</param>
public sealed record CloudFile(string Id, string Name, DateTimeOffset? Modified, long? Size);

/// <summary>A cloud operation failed; the message is written for the user.</summary>
/// <param name="message">What went wrong, in Ukrainian.</param>
/// <param name="inner">The original exception of the provider, if any.</param>
public sealed class CloudStorageException(string message, Exception? inner = null) : Exception(message, inner);
