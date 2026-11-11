using System.Globalization;
using api.Models;
using MySqlConnector;

namespace api.Services;

/// <summary>
/// Picks one official UA resource from the DB (no AI). Fallback chain + soonest open slot.
/// </summary>
public class RecommendationService
{
    private static readonly TimeZoneInfo CentralTz = ResolveCentralTimeZone();

    private readonly string _connectionString;

    public RecommendationService()
    {
        _connectionString = DatabaseConfig.GetConnectionString();
    }

    public async Task<RecommendResponse> RecommendAsync(
        RecommendRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = (request.RequestCategory ?? "academic").Trim().ToLowerInvariant();
        var askedForAnswers = request.AskedForAnswers;

        // Step 1: distress → Counseling Center handoff only (no resource).
        if (category == "distress")
        {
            return HandoffResponse(
                type: "distress",
                // TODO (Mary to verify): replace this Counseling Center placeholder with the verified contact.
                message:
                    "If you're having a hard time, please reach out for support. " +
                    "TODO (Mary to verify): UA Counseling Center — contact info pending verification. " +
                    "You can also call or text 988 for the Suicide & Crisis Lifeline (24/7).",
                askedForAnswers);
        }

        // Step 2: non_academic → Capstone Center handoff only (no resource).
        if (category == "non_academic")
        {
            return HandoffResponse(
                type: "non_academic",
                message:
                    "This doesn't look like an academic assignment help request. " +
                    "Reach out to the Capstone Center for Student Success — Russell Hall, " +
                    "(205) 348-7011, ccssinfo@ua.edu.",
                askedForAnswers);
        }

        // Step 3: academic path needs a real course_id.
        if (request.CourseId is null or <= 0)
        {
            throw new InvalidOperationException("course_id is required for an academic recommendation.");
        }

        var assignmentType = NormalizeAssignmentType(request.AssignmentType);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Step 4: load course code + subject for reason text and subject fallback.
        var course = await LoadCourseAsync(connection, request.CourseId.Value, cancellationToken);
        if (course is null)
        {
            throw new InvalidOperationException($"No course found for course_id {request.CourseId.Value}.");
        }

        // Step 5: walk the fallback chain; first non-empty priority wins.
        // Skip any resource with a non-null eligibility (open-to-all only).
        var (candidates, matchKind) = await FindCandidatesByPriorityAsync(
            connection,
            course.CourseId,
            course.SubjectId,
            assignmentType,
            cancellationToken);

        if (candidates.Count == 0)
        {
            // Extreme empty-DB fallback: Capstone contact, still a recommendation-shaped handoff.
            return HandoffResponse(
                type: "non_academic",
                message:
                    "We couldn't find a tutoring resource right now. " +
                    "Reach out to the Capstone Center for Student Success — Russell Hall, " +
                    "(205) 348-7011, ccssinfo@ua.edu.",
                askedForAnswers);
        }

        // Step 6: among matches, pick the resource whose next open slot is soonest (Central time).
        var nowCentral = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, CentralTz);
        ResourcePick? best = null;

        foreach (var resource in candidates)
        {
            var schedules = await LoadSchedulesAsync(connection, resource.ResourceId, cancellationToken);
            var next = FindSoonestSlot(schedules, nowCentral);

            if (best is null)
            {
                best = new ResourcePick(resource, schedules, next);
                continue;
            }

            // Prefer a concrete upcoming time over "no times listed".
            if (next.HasValue && (!best.NextStart.HasValue || next.Value.Start < best.NextStart.Value))
            {
                best = new ResourcePick(resource, schedules, next);
            }
        }

        best ??= new ResourcePick(candidates[0], await LoadSchedulesAsync(connection, candidates[0].ResourceId, cancellationToken), null);

        // Step 7: build the response from the chosen resource + winning schedule row.
        return BuildRecommendation(best, course.Code, assignmentType, matchKind, askedForAnswers, nowCentral);
    }

    private static RecommendResponse HandoffResponse(string type, string message, bool askedForAnswers)
    {
        return new RecommendResponse
        {
            Type = type,
            ResourceName = null,
            Building = null,
            Room = null,
            IsOnline = null,
            OnlineLink = null,
            NextOpen = null,
            SessionType = null,
            ContactEmail = null,
            ContactPhone = null,
            BookingUrl = null,
            Reason = null,
            WhatToExpect = null,
            LastVerified = null,
            IsStale = false,
            ShowSyllabusTip = false,
            DeclinedAssignmentHelp = askedForAnswers,
            Message = message
        };
    }

    private static RecommendResponse BuildRecommendation(
        ResourcePick pick,
        string courseCode,
        string assignmentType,
        MatchKind matchKind,
        bool askedForAnswers,
        DateTimeOffset nowCentral)
    {
        var resource = pick.Resource;
        ScheduleRow? slot = pick.NextSlot;

        // If no timed slot, still pull session_type / location from the first schedule row when present.
        if (slot is null && pick.Schedules.Count > 0)
        {
            slot = pick.Schedules[0];
        }

        string? nextOpen = null;
        if (pick.NextStart.HasValue && pick.NextSlot is not null
            && pick.NextSlot.StartTime.HasValue && pick.NextSlot.EndTime.HasValue)
        {
            nextOpen = FormatNextOpen(pick.NextStart.Value, pick.NextSlot.StartTime.Value, pick.NextSlot.EndTime.Value);
        }

        string? lastVerified = resource.LastVerified?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var isStale = resource.LastVerified.HasValue
            && resource.LastVerified.Value < DateOnly.FromDateTime(nowCentral.DateTime).AddDays(-30);

        // Template reason — no AI call. Only say "covers [course]" when resource_course linked it.
        var typeLabel = AssignmentTypeLabel(assignmentType);
        var reason = matchKind switch
        {
            MatchKind.CourseLinked =>
                $"This looks like a {courseCode} {typeLabel}, and {resource.Name} covers {courseCode}.",
            MatchKind.SubjectLinked =>
                $"This looks like a {courseCode} {typeLabel}, and {resource.Name} can help with this subject.",
            _ =>
                $"We couldn't find help specific to {courseCode}, but {resource.Name} can help with {typeLabel} for any class."
        };

        return new RecommendResponse
        {
            Type = "recommendation",
            ResourceName = resource.Name,
            Building = slot?.Building,
            Room = slot?.Room,
            IsOnline = slot?.IsOnline,
            OnlineLink = null,
            NextOpen = nextOpen,
            SessionType = slot?.SessionType,
            ContactEmail = resource.ContactEmail,
            ContactPhone = resource.ContactPhone,
            BookingUrl = resource.BookingUrl,
            Reason = reason,
            WhatToExpect = resource.WhatToExpect,
            LastVerified = lastVerified,
            IsStale = isStale,
            ShowSyllabusTip = matchKind == MatchKind.General,
            DeclinedAssignmentHelp = askedForAnswers,
            Message = null
        };
    }

    /// <summary>
    /// Fallback order (eligibility IS NULL on every step):
    /// a) course + assignment type → b) course → c) subject → d) general + type → e) general.
    /// </summary>
    private static async Task<(IReadOnlyList<ResourceRow> Candidates, MatchKind Kind)> FindCandidatesByPriorityAsync(
        MySqlConnection connection,
        int courseId,
        int subjectId,
        string assignmentType,
        CancellationToken cancellationToken)
    {
        // a. linked to the course AND to the assignment type
        var a = await QueryResourcesAsync(connection, """
            SELECT DISTINCT r.resource_id, r.name, r.what_to_expect, r.contact_email, r.contact_phone,
                   r.booking_url, r.last_verified, r.is_general
            FROM resource r
            INNER JOIN resource_course rc ON rc.resource_id = r.resource_id
            INNER JOIN resource_assignment_type rat ON rat.resource_id = r.resource_id
            INNER JOIN assignment_type at ON at.type_id = rat.type_id
            WHERE r.eligibility IS NULL
              AND rc.course_id = @courseId
              AND at.name = @assignmentType
            """, courseId, subjectId, assignmentType, cancellationToken);
        if (a.Count > 0)
        {
            return (a, MatchKind.CourseLinked);
        }

        // b. linked to the course
        var b = await QueryResourcesAsync(connection, """
            SELECT DISTINCT r.resource_id, r.name, r.what_to_expect, r.contact_email, r.contact_phone,
                   r.booking_url, r.last_verified, r.is_general
            FROM resource r
            INNER JOIN resource_course rc ON rc.resource_id = r.resource_id
            WHERE r.eligibility IS NULL
              AND rc.course_id = @courseId
            """, courseId, subjectId, assignmentType, cancellationToken);
        if (b.Count > 0)
        {
            return (b, MatchKind.CourseLinked);
        }

        // c. linked to the course’s subject
        var c = await QueryResourcesAsync(connection, """
            SELECT DISTINCT r.resource_id, r.name, r.what_to_expect, r.contact_email, r.contact_phone,
                   r.booking_url, r.last_verified, r.is_general
            FROM resource r
            INNER JOIN resource_subject rs ON rs.resource_id = r.resource_id
            WHERE r.eligibility IS NULL
              AND rs.subject_id = @subjectId
            """, courseId, subjectId, assignmentType, cancellationToken);
        if (c.Count > 0)
        {
            return (c, MatchKind.SubjectLinked);
        }

        // d. is_general = true AND linked to the assignment type → syllabus tip
        var d = await QueryResourcesAsync(connection, """
            SELECT DISTINCT r.resource_id, r.name, r.what_to_expect, r.contact_email, r.contact_phone,
                   r.booking_url, r.last_verified, r.is_general
            FROM resource r
            INNER JOIN resource_assignment_type rat ON rat.resource_id = r.resource_id
            INNER JOIN assignment_type at ON at.type_id = rat.type_id
            WHERE r.eligibility IS NULL
              AND r.is_general = 1
              AND at.name = @assignmentType
            """, courseId, subjectId, assignmentType, cancellationToken);
        if (d.Count > 0)
        {
            return (d, MatchKind.General);
        }

        // e. is_general = true → syllabus tip
        var e = await QueryResourcesAsync(connection, """
            SELECT DISTINCT r.resource_id, r.name, r.what_to_expect, r.contact_email, r.contact_phone,
                   r.booking_url, r.last_verified, r.is_general
            FROM resource r
            WHERE r.eligibility IS NULL
              AND r.is_general = 1
            """, courseId, subjectId, assignmentType, cancellationToken);
        return (e, MatchKind.General);
    }

    private static async Task<IReadOnlyList<ResourceRow>> QueryResourcesAsync(
        MySqlConnection connection,
        string sql,
        int courseId,
        int subjectId,
        string assignmentType,
        CancellationToken cancellationToken)
    {
        var results = new List<ResourceRow>();

        await using var command = new MySqlCommand(sql, connection);
        // Bind all three so every priority query can reuse the same helper safely.
        command.Parameters.AddWithValue("@courseId", courseId);
        command.Parameters.AddWithValue("@subjectId", subjectId);
        command.Parameters.AddWithValue("@assignmentType", assignmentType);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ResourceRow(
                ResourceId: reader.GetInt32("resource_id"),
                Name: reader.GetString("name"),
                WhatToExpect: reader.IsDBNull(reader.GetOrdinal("what_to_expect"))
                    ? null
                    : reader.GetString("what_to_expect"),
                ContactEmail: reader.IsDBNull(reader.GetOrdinal("contact_email"))
                    ? null
                    : reader.GetString("contact_email"),
                ContactPhone: reader.IsDBNull(reader.GetOrdinal("contact_phone"))
                    ? null
                    : reader.GetString("contact_phone"),
                BookingUrl: reader.IsDBNull(reader.GetOrdinal("booking_url"))
                    ? null
                    : reader.GetString("booking_url"),
                LastVerified: reader.IsDBNull(reader.GetOrdinal("last_verified"))
                    ? null
                    : DateOnly.FromDateTime(reader.GetDateTime("last_verified")),
                IsGeneral: reader.GetBoolean("is_general")));
        }

        return results;
    }

    private static async Task<CourseInfo?> LoadCourseAsync(
        MySqlConnection connection,
        int courseId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT c.course_id, c.subject_id, CONCAT(s.code, ' ', c.number) AS code
            FROM course c
            INNER JOIN subject s ON s.subject_id = c.subject_id
            WHERE c.course_id = @courseId
            LIMIT 1
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@courseId", courseId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new CourseInfo(
            CourseId: reader.GetInt32("course_id"),
            SubjectId: reader.GetInt32("subject_id"),
            Code: reader.GetString("code"));
    }

    private static async Task<IReadOnlyList<ScheduleRow>> LoadSchedulesAsync(
        MySqlConnection connection,
        int resourceId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT s.schedule_id, s.day_of_week, s.start_time, s.end_time, s.session_type,
                   s.start_date, s.end_date,
                   l.building, l.room, l.is_online
            FROM schedule s
            LEFT JOIN location l ON l.location_id = s.location_id
            WHERE s.resource_id = @resourceId
            """;

        var rows = new List<ScheduleRow>();

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@resourceId", resourceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            TimeOnly? startTime = reader.IsDBNull(reader.GetOrdinal("start_time"))
                ? null
                : TimeOnly.FromTimeSpan(reader.GetTimeSpan("start_time"));
            TimeOnly? endTime = reader.IsDBNull(reader.GetOrdinal("end_time"))
                ? null
                : TimeOnly.FromTimeSpan(reader.GetTimeSpan("end_time"));
            DateOnly? startDate = reader.IsDBNull(reader.GetOrdinal("start_date"))
                ? null
                : DateOnly.FromDateTime(reader.GetDateTime("start_date"));
            DateOnly? endDate = reader.IsDBNull(reader.GetOrdinal("end_date"))
                ? null
                : DateOnly.FromDateTime(reader.GetDateTime("end_date"));

            rows.Add(new ScheduleRow(
                DayOfWeekName: reader.IsDBNull(reader.GetOrdinal("day_of_week"))
                    ? null
                    : reader.GetString("day_of_week"),
                StartTime: startTime,
                EndTime: endTime,
                SessionType: reader.GetString("session_type"),
                StartDate: startDate,
                EndDate: endDate,
                Building: reader.IsDBNull(reader.GetOrdinal("building"))
                    ? null
                    : reader.GetString("building"),
                Room: reader.IsDBNull(reader.GetOrdinal("room"))
                    ? null
                    : reader.GetString("room"),
                IsOnline: reader.IsDBNull(reader.GetOrdinal("is_online"))
                    ? null
                    : reader.GetBoolean("is_online")));
        }

        return rows;
    }

    /// <summary>
    /// Find the soonest upcoming open window in Central time.
    /// Rows without start/end times do not count (next_open stays null).
    /// </summary>
    private static (DateTimeOffset Start, ScheduleRow Slot)? FindSoonestSlot(
        IReadOnlyList<ScheduleRow> schedules,
        DateTimeOffset nowCentral)
    {
        (DateTimeOffset Start, ScheduleRow Slot)? best = null;
        var today = DateOnly.FromDateTime(nowCentral.DateTime);

        // Look ahead up to 14 days so weekly + dated ranges both resolve.
        for (var offset = 0; offset < 14; offset++)
        {
            var date = today.AddDays(offset);
            var dayName = date.DayOfWeek.ToString(); // "Monday", matches seed data

            foreach (var row in schedules)
            {
                // Must have a day and concrete start/end times to format next_open.
                if (string.IsNullOrWhiteSpace(row.DayOfWeekName)
                    || !row.StartTime.HasValue
                    || !row.EndTime.HasValue)
                {
                    continue;
                }

                if (!row.DayOfWeekName.Equals(dayName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Honor optional date ranges on the schedule row.
                if (row.StartDate.HasValue && date < row.StartDate.Value)
                {
                    continue;
                }

                if (row.EndDate.HasValue && date > row.EndDate.Value)
                {
                    continue;
                }

                var startLocal = new DateTime(
                    date.Year, date.Month, date.Day,
                    row.StartTime.Value.Hour, row.StartTime.Value.Minute, row.StartTime.Value.Second,
                    DateTimeKind.Unspecified);
                var endLocal = new DateTime(
                    date.Year, date.Month, date.Day,
                    row.EndTime.Value.Hour, row.EndTime.Value.Minute, row.EndTime.Value.Second,
                    DateTimeKind.Unspecified);

                var start = new DateTimeOffset(startLocal, CentralTz.GetUtcOffset(startLocal));
                var end = new DateTimeOffset(endLocal, CentralTz.GetUtcOffset(endLocal));

                // Skip slots that already ended; a slot still in progress counts as next open.
                if (end <= nowCentral)
                {
                    continue;
                }

                if (best is null || start < best.Value.Start)
                {
                    best = (start, row);
                }
            }
        }

        return best;
    }

    private static string FormatNextOpen(DateTimeOffset start, TimeOnly startTime, TimeOnly endTime)
    {
        var day = start.ToString("ddd", CultureInfo.InvariantCulture); // Tue
        var startPart = FormatTime(startTime);
        var endPart = FormatTime(endTime);

        // "Tue 5:00–8:00 PM" when both sides share AM/PM; otherwise keep both markers.
        var startPeriod = startTime.Hour >= 12 ? "PM" : "AM";
        var endPeriod = endTime.Hour >= 12 ? "PM" : "AM";

        if (startPeriod == endPeriod)
        {
            return $"{day} {StripPeriod(startPart)}–{endPart}";
        }

        return $"{day} {startPart}–{endPart}";
    }

    private static string FormatTime(TimeOnly time)
    {
        var hour = time.Hour % 12;
        if (hour == 0)
        {
            hour = 12;
        }

        var period = time.Hour >= 12 ? "PM" : "AM";
        return $"{hour}:{time.Minute:D2} {period}";
    }

    private static string StripPeriod(string formatted) =>
        formatted.Replace(" AM", "", StringComparison.Ordinal).Replace(" PM", "", StringComparison.Ordinal);

    private static string NormalizeAssignmentType(string? raw)
    {
        var value = (raw ?? "other").Trim().ToLowerInvariant().Replace(' ', '_');
        return value switch
        {
            "essay" or "lab_report" or "problem_set" or "coding_project"
                or "presentation" or "exam_prep" or "other" => value,
            _ => "other"
        };
    }

    private static string AssignmentTypeLabel(string assignmentType) => assignmentType switch
    {
        "essay" => "essay",
        "lab_report" => "lab report",
        "problem_set" => "problem set",
        "coding_project" => "coding project",
        "presentation" => "presentation",
        "exam_prep" => "exam prep",
        _ => "assignment"
    };

    private static TimeZoneInfo ResolveCentralTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
        }
        catch (TimeZoneNotFoundException)
        {
            // Windows often uses the display id instead of the IANA name.
            return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        }
    }

    /// <summary>Which fallback step produced the candidates (drives reason text + syllabus tip).</summary>
    private enum MatchKind
    {
        CourseLinked,   // a or b — resource_course link
        SubjectLinked,  // c — resource_subject link
        General         // d or e — is_general
    }

    private sealed record CourseInfo(int CourseId, int SubjectId, string Code);

    private sealed record ResourceRow(
        int ResourceId,
        string Name,
        string? WhatToExpect,
        string? ContactEmail,
        string? ContactPhone,
        string? BookingUrl,
        DateOnly? LastVerified,
        bool IsGeneral);

    private sealed record ScheduleRow(
        string? DayOfWeekName,
        TimeOnly? StartTime,
        TimeOnly? EndTime,
        string SessionType,
        DateOnly? StartDate,
        DateOnly? EndDate,
        string? Building,
        string? Room,
        bool? IsOnline);

    private sealed class ResourcePick
    {
        public ResourceRow Resource { get; }
        public IReadOnlyList<ScheduleRow> Schedules { get; }
        public DateTimeOffset? NextStart { get; }
        public ScheduleRow? NextSlot { get; }

        public ResourcePick(
            ResourceRow resource,
            IReadOnlyList<ScheduleRow> schedules,
            (DateTimeOffset Start, ScheduleRow Slot)? next)
        {
            Resource = resource;
            Schedules = schedules;
            NextStart = next?.Start;
            NextSlot = next?.Slot;
        }
    }
}
