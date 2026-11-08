using System.Text;
using XMLLibrary.Core.Model;
using XMLLibrary.ViewModels;

namespace XMLLibrary.Services;

/// <summary><see cref="IFileService"/> with the MAUI file picker and the default browser of Windows.</summary>
public sealed class MauiFileService : IFileService
{
    /// <inheritdoc />
    public async Task<PickedFile?> PickAsync(string title, IReadOnlyList<string> extensions)
    {
        var options = new PickOptions
        {
            PickerTitle = title,
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.WinUI] = extensions,
            }),
        };

        FileResult? result = await FilePicker.Default.PickAsync(options);
        if (result is null)
        {
            return null;
        }

        await using Stream stream = await result.OpenReadAsync();
        byte[] content = await ReadLimitedAsync(stream, result.FileName);
        return new PickedFile(result.FileName, content);
    }

    /// <inheritdoc />
    public async Task OpenHtmlInBrowserAsync(string html, string fileName)
    {
        string folder = Path.Combine(FileSystem.CacheDirectory, "html-preview");
        Directory.CreateDirectory(folder);

        // The name is cleaned again here: it may come from Google Drive, where any character is allowed.
        string path = Path.Combine(folder, FileNames.Build(Path.GetFileNameWithoutExtension(fileName), ".html"));

        // UTF-8 with a byte order mark, so every browser shows the Ukrainian letters correctly.
        await File.WriteAllTextAsync(path, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        bool opened = await Launcher.Default.OpenAsync(new OpenFileRequest(Path.GetFileName(path), new ReadOnlyFile(path)));
        if (!opened)
        {
            throw new IOException("Windows не знайшов програму для відкриття HTML-файлу.");
        }
    }

    /// <summary>Reads the whole stream, but refuses a file larger than <see cref="FileKinds.MaxFileBytes"/>.</summary>
    private static async Task<byte[]> ReadLimitedAsync(Stream stream, string fileName)
    {
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + read > FileKinds.MaxFileBytes)
            {
                throw new InvalidDataException(
                    $"Файл «{fileName}» завеликий: можна відкрити файл до {FileKinds.MaxFileBytes / 1024 / 1024} МБ.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
