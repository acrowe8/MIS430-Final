using System.Text.RegularExpressions;
using api.Models;
using MySqlConnector;

namespace api.Services;

public class CourseService
{
    private readonly string _connectionString;

    // UA-style codes: "CH 101", "CH101", "MATH 125", "BSC 108L", optional section "CH 101-002"
    private static readonly Regex CourseCodeRegex = new(
        @"\b([A-Za-z]{2,5})\s*(\d{3}[A-Za-z]?)(?:-\d{2,3})?\b",
        RegexOptions.Compiled);

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "about", "after", "again", "also", "and", "assignment", "because", "been", "before",
        "being", "both", "but", "can", "could", "does", "each", "for", "from", "have", "here",
        "into", "just", "like", "more", "most", "must", "only", "other", "over", "please",
        "should", "some", "such", "than", "that", "the", "their", "them", "then", "there",
        "these", "they", "this", "those", "through", "under", "very", "what", "when", "where",
        "which", "while", "will", "with", "would", "your", "homework", "page", "pages"
    };

    public CourseService()
    {
        _connectionString = DatabaseConfig.GetConnectionString();
    }

    /// <summary>
    /// Search courses by subject code, course number, full code (e.g. CH 101 / CH101), or title.
    /// Returns up to 20 matches. Empty search returns an empty list.
    /// </summary>
    public async Task<IReadOnlyList<Course>> SearchAsync(string? search, CancellationToken cancellationToken = default)
    {
        var term = (search ?? string.Empty).Trim();
        if (term.Length == 0)
        {
            return Array.Empty<Course>();
        }

        // Escape LIKE wildcards so user input can't broaden the match unintentionally.
        var pattern = "%" + EscapeLike(term) + "%";
        var compact = EscapeLike(term.Replace(" ", string.Empty));

        const string sql = """
            SELECT c.course_id,
                   CONCAT(s.code, ' ', c.number) AS code,
                   c.title
            FROM course c
            INNER JOIN subject s ON s.subject_id = c.subject_id
            WHERE s.code LIKE @pattern ESCAPE '\\'
               OR c.number LIKE @pattern ESCAPE '\\'
               OR c.title LIKE @pattern ESCAPE '\\'
               OR s.name LIKE @pattern ESCAPE '\\'
               OR CONCAT(s.code, ' ', c.number) LIKE @pattern ESCAPE '\\'
               OR CONCAT(s.code, c.number) LIKE @compactPattern ESCAPE '\\'
            ORDER BY s.code, c.number
            LIMIT 20
            """;

        var results = new List<Course>();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@pattern", pattern);
        command.Parameters.AddWithValue("@compactPattern", "%" + compact + "%");

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new Course
            {
                CourseId = reader.GetInt32("course_id"),
                Code = reader.GetString("code"),
                Title = reader.GetString("title")
            });
        }

        return results;
    }

    /// <summary>
    /// Narrow candidate courses for one OpenAI call (max 15).
    /// 1) Course codes or subject names found in the text that exist in the DB
    ///    (matches in the first ~10 lines are listed first).
    /// 2) Else FULLTEXT on title/description using keywords from the text.
    /// </summary>
    public async Task<IReadOnlyList<CandidateCourse>> FindCandidatesAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var combined = text ?? string.Empty;

        // Part 1: real course refs (code or full subject name) from the text.
        var codeMatches = await FindCoursesByCodesInTextAsync(combined, cancellationToken);
        if (codeMatches.Count > 0)
        {
            return codeMatches.Take(15).ToList();
        }

        // Part 2: no code found — keyword FULLTEXT search, top 15 only.
        return await FindCoursesByFullTextAsync(combined, cancellationToken);
    }

    /// <summary>Resolve a course_guess code (e.g. "CH 101") to a DB course, or null.</summary>
    public async Task<Course?> FindByCodeAsync(string? code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalized = Regex.Replace(code.Trim(), @"\s+", " ");
        var compact = normalized.Replace(" ", string.Empty);

        const string sql = """
            SELECT c.course_id,
                   CONCAT(s.code, ' ', c.number) AS code,
                   c.title
            FROM course c
            INNER JOIN subject s ON s.subject_id = c.subject_id
            WHERE CONCAT(s.code, ' ', c.number) = @code
               OR CONCAT(s.code, c.number) = @compact
            LIMIT 1
            """;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@code", normalized);
        command.Parameters.AddWithValue("@compact", compact);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new Course
        {
            CourseId = reader.GetInt32("course_id"),
            Code = reader.GetString("code"),
            Title = reader.GetString("title")
        };
    }

    private async Task<IReadOnlyList<CandidateCourse>> FindCoursesByCodesInTextAsync(
        string text,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var subjects = await LoadSubjectsAsync(connection, cancellationToken);

        // Prefer the header — syllabi / assignments usually put the class in the first lines.
        var header = FirstLines(text, 10);
        var headerRefs = ExtractCourseRefs(header, subjects);
        var allRefs = ExtractCourseRefs(text, subjects);

        var found = new List<(string Subject, string Number)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in headerRefs.Concat(allRefs))
        {
            var key = pair.Subject + pair.Number;
            if (seen.Add(key))
            {
                found.Add(pair);
            }
        }

        if (found.Count == 0)
        {
            return Array.Empty<CandidateCourse>();
        }

        // Parameterized lookup: one (subject, number) pair at a time.
        var results = new List<CandidateCourse>();

        foreach (var (subject, number) in found)
        {
            if (results.Count >= 15)
            {
                break;
            }

            const string sql = """
                SELECT c.course_id,
                       CONCAT(s.code, ' ', c.number) AS code,
                       c.title,
                       c.description
                FROM course c
                INNER JOIN subject s ON s.subject_id = c.subject_id
                WHERE s.code = @subject AND c.number = @number
                LIMIT 1
                """;

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@subject", subject);
            command.Parameters.AddWithValue("@number", number);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                results.Add(ReadCandidate(reader));
            }
        }

        return results;
    }

    /// <summary>
    /// Pull (subject code, course number) pairs from text via subject codes
    /// (e.g. CH 101-002) or subject names (e.g. English 102-075).
    /// </summary>
    private static List<(string Subject, string Number)> ExtractCourseRefs(
        string text,
        IReadOnlyList<(string Code, string Name)> subjects)
    {
        var found = new List<(string Subject, string Number)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(text))
        {
            return found;
        }

        // Longest subject names first so "Art History 101" wins over a shorter overlap.
        foreach (var (code, name) in subjects.OrderByDescending(s => s.Name.Length))
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length < 3)
            {
                continue;
            }

            var pattern = $@"\b{Regex.Escape(name)}\s+(\d{{3}}[A-Za-z]?)(?:-\d{{2,3}})?\b";
            foreach (Match match in Regex.Matches(text, pattern, RegexOptions.IgnoreCase))
            {
                var number = match.Groups[1].Value.ToUpperInvariant();
                var subject = code.ToUpperInvariant();
                var key = subject + number;
                if (seen.Add(key))
                {
                    found.Add((subject, number));
                }
            }
        }

        foreach (Match match in CourseCodeRegex.Matches(text))
        {
            var subject = match.Groups[1].Value.ToUpperInvariant();
            var number = match.Groups[2].Value.ToUpperInvariant();
            var key = subject + number;
            if (seen.Add(key))
            {
                found.Add((subject, number));
            }
        }

        return found;
    }

    private static async Task<IReadOnlyList<(string Code, string Name)>> LoadSubjectsAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT code, name FROM subject";
        var subjects = new List<(string Code, string Name)>();

        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            subjects.Add((reader.GetString("code"), reader.GetString("name")));
        }

        return subjects;
    }

    private static string FirstLines(string text, int count)
    {
        if (string.IsNullOrEmpty(text) || count <= 0)
        {
            return string.Empty;
        }

        var lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        return string.Join("\n", lines.Take(count));
    }

    private async Task<IReadOnlyList<CandidateCourse>> FindCoursesByFullTextAsync(
        string text,
        CancellationToken cancellationToken)
    {
        var keywords = BuildKeywordQuery(text);
        if (string.IsNullOrWhiteSpace(keywords))
        {
            return Array.Empty<CandidateCourse>();
        }

        // FULLTEXT index ft_course_text (title, description) — never more than 15.
        const string sql = """
            SELECT c.course_id,
                   CONCAT(s.code, ' ', c.number) AS code,
                   c.title,
                   c.description,
                   MATCH(c.title, c.description) AGAINST (@keywords IN NATURAL LANGUAGE MODE) AS score
            FROM course c
            INNER JOIN subject s ON s.subject_id = c.subject_id
            WHERE MATCH(c.title, c.description) AGAINST (@keywords IN NATURAL LANGUAGE MODE)
            ORDER BY score DESC
            LIMIT 15
            """;

        var results = new List<CandidateCourse>();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@keywords", keywords);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadCandidate(reader));
        }

        return results;
    }

    private static CandidateCourse ReadCandidate(MySqlDataReader reader)
    {
        return new CandidateCourse
        {
            CourseId = reader.GetInt32("course_id"),
            Code = reader.GetString("code"),
            Title = reader.GetString("title"),
            Description = reader.GetString("description")
        };
    }

    /// <summary>Turn assignment text into a short keyword string for FULLTEXT.</summary>
    private static string BuildKeywordQuery(string text)
    {
        var words = Regex.Matches(text, @"[A-Za-z]{4,}")
            .Select(m => m.Value)
            .Where(w => !StopWords.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(40);

        return string.Join(' ', words);
    }

    private static string EscapeLike(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }
}
