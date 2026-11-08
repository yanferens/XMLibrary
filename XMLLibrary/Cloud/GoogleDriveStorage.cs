using System.Net;
using System.Text.Json;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using XMLLibrary.Core.Cloud;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace XMLLibrary.Cloud;

/// <summary>
/// Adapter: <see cref="ICloudStorage"/> on top of the Google Drive API. It signs the user in with OAuth 2.0
/// in the system browser on first use and keeps the tokens on disk, so the browser opens only once.
/// Every Google-specific exception is translated into <see cref="CloudStorageException"/> here and never
/// reaches the rest of the program.
/// </summary>
/// <remarks>
/// The OAuth client comes from <c>Resources/Raw/client_secret.json</c> (a "Desktop app" client from
/// Google Cloud Console). Do not commit this file to git.
/// </remarks>
public sealed class GoogleDriveStorage : ICloudStorage, IDisposable
{
    private const string ClientSecretAsset = "client_secret.json";
    private const string ApplicationName = "XMLLibrary";
    private const string UserId = "user";
    private const string GoogleDocsMimePrefix = "application/vnd.google-apps.";
    private const int PageSize = 1000;
    private const int MaxPages = 10;

    private static readonly TimeSpan SignInTimeout = TimeSpan.FromMinutes(3);

    private readonly SemaphoreSlim _serviceLock = new(1, 1);
    private readonly string _tokenDirectory = Path.Combine(FileSystem.AppDataDirectory, "GoogleAuth");

    private DriveService? _service;

    /// <inheritdoc />
    public bool IsSignedIn => _service is not null || HasSavedSignIn();

    /// <inheritdoc />
    public Task<IReadOnlyList<CloudFile>> ListAsync(IReadOnlyCollection<string> extensions, CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<CloudFile>>(async drive =>
        {
            var found = new List<CloudFile>();
            string? pageToken = null;

            // Drive returns at most 1000 files per request, so the pages are read one after another.
            for (int page = 0; page < MaxPages; page++)
            {
                FilesResource.ListRequest request = drive.Files.List();
                request.Q = "trashed = false";
                request.Fields = "nextPageToken, files(id, name, mimeType, modifiedTime, size)";
                request.OrderBy = "modifiedTime desc";
                request.PageSize = PageSize;
                request.PageToken = pageToken;

                Google.Apis.Drive.v3.Data.FileList result = await request.ExecuteAsync(cancellationToken);

                // Drive's query language cannot say "name ends with .xml", so the extension is checked here.
                // Google Docs files cannot be downloaded as they are, so they are skipped.
                found.AddRange((result.Files ?? new List<DriveFile>())
                    .Where(file => file.Name is not null
                                   && extensions.Any(extension => file.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                                   && !(file.MimeType ?? string.Empty).StartsWith(GoogleDocsMimePrefix, StringComparison.Ordinal))
                    .Select(ToCloudFile));

                pageToken = result.NextPageToken;
                if (string.IsNullOrEmpty(pageToken))
                {
                    break;
                }
            }

            return found;
        }, cancellationToken);

    /// <inheritdoc />
    public Task<byte[]> DownloadAsync(string fileId, CancellationToken cancellationToken = default)
        => RunAsync(async drive =>
        {
            ArgumentException.ThrowIfNullOrEmpty(fileId);

            using var buffer = new MemoryStream();
            IDownloadProgress progress = await drive.Files.Get(fileId).DownloadAsync(buffer, cancellationToken);
            if (progress.Status != DownloadStatus.Completed)
            {
                throw progress.Exception ?? new IOException("Завантаження з Google Drive не завершилося.");
            }

            return buffer.ToArray();
        }, cancellationToken);

    /// <inheritdoc />
    public Task<CloudFile> UploadAsync(string name, string mimeType, Stream content, CancellationToken cancellationToken = default)
        => RunAsync(async drive =>
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(content);

            FilesResource.CreateMediaUpload request = drive.Files.Create(
                new DriveFile { Name = name, MimeType = mimeType }, content, mimeType);
            request.Fields = "id, name, modifiedTime, size";

            IUploadProgress progress = await request.UploadAsync(cancellationToken);
            if (progress.Status != UploadStatus.Completed)
            {
                throw progress.Exception ?? new IOException("Збереження на Google Drive не завершилося.");
            }

            return request.ResponseBody is { } created
                ? ToCloudFile(created)
                : new CloudFile(string.Empty, name, DateTimeOffset.Now, content.CanSeek ? content.Length : null);
        }, cancellationToken);

    /// <inheritdoc />
    public async Task SignOutAsync()
    {
        await _serviceLock.WaitAsync();
        try
        {
            _service?.Dispose();
            _service = null;

            if (Directory.Exists(_tokenDirectory))
            {
                Directory.Delete(_tokenDirectory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CloudStorageException("Не вдалося видалити збережений вхід у Google. Закрийте програму й спробуйте ще раз.", ex);
        }
        finally
        {
            _serviceLock.Release();
        }
    }

    /// <summary>Releases the HTTP client of the Drive service.</summary>
    public void Dispose()
    {
        _service?.Dispose();
        _service = null;
        _serviceLock.Dispose();
    }

    // ───────────── Sign-in ─────────────

    /// <summary>Returns the Drive client, signing the user in on first use (a browser window opens).</summary>
    private async Task<DriveService> GetServiceAsync(CancellationToken cancellationToken)
    {
        await _serviceLock.WaitAsync(cancellationToken);
        try
        {
            if (_service is not null)
            {
                return _service;
            }

            ClientSecrets secrets = await ReadClientSecretsAsync();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(SignInTimeout);

            UserCredential credential;
            try
            {
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    secrets,
                    new[] { DriveService.Scope.Drive },
                    UserId,
                    timeout.Token,
                    new FileDataStore(_tokenDirectory, fullPath: true));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new CloudStorageException("Вхід у Google акаунт не завершено за 3 хвилини. Спробуйте ще раз.");
            }

            _service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });
            return _service;
        }
        finally
        {
            _serviceLock.Release();
        }
    }

    /// <summary>Reads the OAuth client id and secret from the file packed with the app.</summary>
    private static async Task<ClientSecrets> ReadClientSecretsAsync()
    {
        try
        {
            await using Stream stream = await FileSystem.OpenAppPackageFileAsync(ClientSecretAsset);
            using JsonDocument json = await JsonDocument.ParseAsync(stream);

            // Google names the root "installed" for desktop clients and "web" for web clients.
            JsonElement root = json.RootElement;
            JsonElement client = root.TryGetProperty("installed", out JsonElement installed) ? installed : root.GetProperty("web");

            string? clientId = client.GetProperty("client_id").GetString();
            string? clientSecret = client.GetProperty("client_secret").GetString();
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new KeyNotFoundException("client_id or client_secret is empty.");
            }

            return new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret };
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new CloudStorageException(
                "Не знайдено файл client_secret.json. Покладіть його в папку Resources/Raw проєкту XMLLibrary і зберіть програму знову.", ex);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new CloudStorageException(
                "Файл client_secret.json має неправильний формат: потрібен OAuth-клієнт типу «Desktop app».", ex);
        }
    }

    /// <summary>True when the token folder of a previous sign-in exists and is not empty.</summary>
    private bool HasSavedSignIn()
    {
        try
        {
            return Directory.Exists(_tokenDirectory) && Directory.EnumerateFiles(_tokenDirectory).Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // ───────────── Error translation ─────────────

    /// <summary>
    /// Runs one Drive operation and translates every failure into a <see cref="CloudStorageException"/>
    /// with a message for the user.
    /// </summary>
    private async Task<T> RunAsync<T>(Func<DriveService, Task<T>> operation, CancellationToken cancellationToken)
    {
        try
        {
            DriveService drive = await GetServiceAsync(cancellationToken);
            return await operation(drive);
        }
        catch (CloudStorageException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (GoogleApiException ex)
        {
            throw new CloudStorageException(ex.HttpStatusCode switch
            {
                HttpStatusCode.NotFound => "Файл не знайдено на Google Drive.",
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    "Google Drive відмовив у доступі. Перевірте, що ваш акаунт додано до тестових користувачів у Google Cloud Console.",
                _ => $"Помилка Google Drive: {ex.Message}",
            }, ex);
        }
        catch (TokenResponseException ex) when (ex.Error?.Error == "access_denied")
        {
            throw new CloudStorageException("Вхід у Google скасовано. Дозвольте доступ у вікні браузера.", ex);
        }
        catch (TokenResponseException ex)
        {
            await ForgetSessionAsync();
            throw new CloudStorageException("Сеанс входу в Google завершився. Повторіть дію, щоб увійти знову.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new CloudStorageException("Немає зв'язку з Google Drive. Перевірте підключення до інтернету.", ex);
        }
        catch (TaskCanceledException ex)
        {
            // A timeout of the HTTP client, not a cancellation by the user.
            throw new CloudStorageException("Google Drive не відповідає. Спробуйте трохи пізніше.", ex);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new CloudStorageException($"Помилка Google Drive: {ex.Message}", ex);
        }
    }

    /// <summary>Drops the client and the saved tokens after the tokens stopped working, so the next operation signs in again.</summary>
    private async Task ForgetSessionAsync()
    {
        try
        {
            await SignOutAsync();
        }
        catch (CloudStorageException)
        {
            // The old tokens stay on disk; Google will reject them again and the user can sign out by hand.
        }
    }

    /// <summary>Converts the Google model of a file into the program's own model.</summary>
    private static CloudFile ToCloudFile(DriveFile file)
        => new(file.Id ?? string.Empty, file.Name ?? string.Empty, file.ModifiedTimeDateTimeOffset, file.Size);
}
