# NextStep UA – Project Status

Keep this file current. Update it in the same commit when you finish work.

## Done

| What’s working | Files |
|---|---|
| DB schema + seed (subjects, courses, resources, schedules, etc.) | `/database/schema.sql`, `/database/seed.sql`, `/database/data/` |
| `.env` loading in Development; Heroku config vars in prod | `/api/Program.cs`, repo-root `.env` (local only, gitignored) |
| MySQL connection from `JAWSDB_URL` | `/api/Services/DatabaseConfig.cs` |
| `GET /api/courses?search=` | `/api/Controllers/CoursesController.cs`, `/api/Services/CourseService.cs`, `/api/Models/Course.cs` |
| `POST /api/analyze` (upload → classify; file in memory only) | `/api/Controllers/AnalyzeController.cs`, `/api/Services/AnalyzeService.cs`, `/api/Services/FileExtractionService.cs`, `/api/Models/AnalyzeResponse.cs` |
| Course candidate narrowing (codes, full subject names, section numbers, header-first, file-name clue, FULLTEXT fallback) | `/api/Services/CourseService.cs`, `/api/Models/CandidateCourse.cs` |
| OpenAI classifier (one call; `<file_name>` clue; gate "high" unless course named in text/file name; low-confidence fallback on failure) | `/api/Services/AiService.cs`, `/api/Services/AnalyzeService.cs`, `/api/Services/CourseService.cs` |
| Classifier system prompt | `/api/Prompts/classifier-system-prompt.md` |

## Owners

| Owner | Owns |
|---|---|
| Cole | `AiService`, `CourseService`, `DatabaseConfig`, `/api/Prompts/`, `/database/`, `AnalyzeController`, `CoursesController` (and related analyze/course models + services he introduced) |
| Carter | Frontend in `/api/wwwroot/`, upcoming `POST /api/recommend` |

Do not edit another teammate’s files. Ask the owner first.

## Open

- Carter main screen implemented locally on `carter/main-screen`: upload button, optional message, client file checks, loading/error/success states, and relative `POST /api/analyze` request (`/api/wwwroot/index.html`, `index.js`, `styles.css`). Pending review; not yet a complete Story 1 flow.
- Successful uploads currently show an acknowledgment only. Wire the classification to course confirmation/search, assignment-type selection, and non-academic/distress handoffs next.
- Main-screen checks: `dotnet build NextStep.sln` passed (0 warnings/errors), JavaScript syntax passed, and mocked upload checks passed for invalid/empty/oversized files, multipart submission, loading, success, and server-error recovery. No OpenAI calls made. Desktop Chrome visual verification remains pending because browser preview timed out.
- `POST /api/recommend` (not built)
- Course confirmation, course search, assignment-type question, and recommendation frontend screens (not built)
- Sprint 1 stretch stories 8–10 (Talk to a person, why + last updated, what to expect) — wait until core recommend + UI exist
