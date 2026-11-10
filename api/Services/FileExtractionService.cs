using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace api.Services;

/// <summary>
/// In-memory file checks + text extraction for PDF / .docx.
/// Never writes uploads to disk, DB, or logs.
/// </summary>
public class FileExtractionService
{
    public const long MaxFileBytes = 10 * 1024 * 1024; // 10 MB
    public const int MaxTextChars = 3000;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".png", ".jpg", ".jpeg", ".gif", ".webp"
    };

    public sealed class UploadedAssignment
    {
        public required string FileName { get; init; }
        public required string ContentType { get; init; }
        public required byte[] Bytes { get; init; }
        public bool IsImage { get; init; }
        /// <summary>Extracted text for PDF/docx (trimmed). Empty for images.</summary>
        public string ExtractedText { get; init; } = string.Empty;
    }

    /// <summary>
    /// Validate type/size, read into memory, extract text when possible.
    /// Throws InvalidOperationException with a student-facing message on bad input.
    /// </summary>
    public async Task<UploadedAssignment> ReadAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            throw new InvalidOperationException("Please upload a PDF, Word (.docx), or image file.");
        }

        if (file.Length > MaxFileBytes)
        {
            throw new InvalidOperationException("That file is too large. Please upload a file under 10 MB.");
        }

        var extension = Path.GetExtension(file.FileName ?? string.Empty);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(
                "That file type isn't supported. Please upload a PDF, Word (.docx), or image (PNG, JPG, GIF, WEBP).");
        }

        // Read the whole upload into memory only — never save to disk.
        await using var input = file.OpenReadStream();
        using var memory = new MemoryStream();
        await input.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();

        var isImage = extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp";
        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? GuessContentType(extension)
            : file.ContentType;

        string extractedText = string.Empty;
        if (!isImage)
        {
            // PDF / .docx: extract text and keep the first ~3,000 characters (course info is usually at the top).
            extractedText = extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                ? ExtractPdfText(bytes)
                : ExtractDocxText(bytes);

            if (extractedText.Length > MaxTextChars)
            {
                extractedText = extractedText[..MaxTextChars];
            }
        }

        return new UploadedAssignment
        {
            FileName = file.FileName ?? $"upload{extension}",
            ContentType = contentType,
            Bytes = bytes,
            IsImage = isImage,
            ExtractedText = extractedText
        };
    }

    private static string ExtractPdfText(byte[] bytes)
    {
        using var document = PdfDocument.Open(bytes);
        var builder = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
            if (builder.Length >= MaxTextChars)
            {
                break;
            }
        }

        return NormalizeWhitespace(builder.ToString());
    }

    private static string ExtractDocxText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body == null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var paragraph in body.Elements<Paragraph>())
        {
            builder.AppendLine(paragraph.InnerText);
            if (builder.Length >= MaxTextChars)
            {
                break;
            }
        }

        return NormalizeWhitespace(builder.ToString());
    }

    private static string NormalizeWhitespace(string text)
    {
        return RegexReplace(text).Trim();
    }

    private static string RegexReplace(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, @"[ \t]+", " ")
            .Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string GuessContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
}
