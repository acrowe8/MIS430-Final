using api.Models;
using MySqlConnector;

namespace api.Services;

public class CourseService
{
    private readonly string _connectionString;

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

    private static string EscapeLike(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }
}
