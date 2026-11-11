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
| `POST /api/recommend` (fallback chain + soonest Central open slot; distress / non-academic handoffs) | `/api/Controllers/RecommendController.cs`, `/api/Services/RecommendationService.cs`, `/api/Models/RecommendRequest.cs`, `/api/Models/RecommendResponse.cs` |

## Owners

| Owner | Owns |
|---|---|
| Cole | `AiService`, `CourseService`, `RecommendationService`, `DatabaseConfig`, `/api/Prompts/`, `/database/`, `AnalyzeController`, `CoursesController`, `RecommendController` (and related analyze/course/recommend models + services he introduced); Sprint 1 Story 2 |
| Carter | Frontend in `/api/wwwroot/` |

Do not edit another teammate’s files. Ask the owner first.

## Open

- All frontend screens in `/api/wwwroot/` (not built)
- Sprint 1 stretch stories 8–10 (Talk to a person, why + last updated, what to expect) — wait until core recommend + UI exist
