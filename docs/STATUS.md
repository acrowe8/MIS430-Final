# NextStep UA – Project Status

Keep this file current. Update it in the same commit when you finish work.

## Done

| What’s working | Files |
|---|---|
| DB schema + seed (subjects, courses, resources, schedules, etc.) | `/database/schema.sql`, `/database/seed.sql`, `/database/data/` |
| `.env` loading in Development; Heroku config vars in prod | `/api/Program.cs`, repo-root `.env` (local only, gitignored) |
| MySQL connection from `JAWSDB_URL` | `/api/Services/DatabaseConfig.cs` |
| `GET /api/courses?search=` | `/api/Controllers/CoursesController.cs`, `/api/Services/CourseService.cs`, `/api/Models/Course.cs` |
| `POST /api/analyze` (upload → classify; file in memory only; file-only uploads never return non_academic — forced to academic + low confidence; distress still wins; typed message keeps Capstone handoff) | `/api/Controllers/AnalyzeController.cs`, `/api/Services/AnalyzeService.cs`, `/api/Services/FileExtractionService.cs`, `/api/Models/AnalyzeResponse.cs` |
| Course candidate narrowing (codes, full subject names, section numbers, header-first, file-name clue, FULLTEXT fallback) | `/api/Services/CourseService.cs`, `/api/Models/CandidateCourse.cs` |
| OpenAI classifier (one call; `<file_name>` clue; gate "high" unless course named in text/file name; low-confidence fallback on failure) | `/api/Services/AiService.cs`, `/api/Services/AnalyzeService.cs`, `/api/Services/CourseService.cs` |
| Classifier system prompt | `/api/Prompts/classifier-system-prompt.md` |
| `POST /api/recommend` (Cole) — fallback chain + soonest Central open slot; distress / non-academic handoffs; reason text only says a resource “covers” a course when `resource_course` linked it; general-fallback reason uses plural type names (essays and papers, lab reports, problem sets, coding projects, presentations, exam prep) | `/api/Controllers/RecommendController.cs`, `/api/Services/RecommendationService.cs`, `/api/Models/RecommendRequest.cs`, `/api/Models/RecommendResponse.cs` |
| Course confirmation/search screen (Carter) — Yes/No on high/low-confidence guess, searchable `GET /api/courses?search=` picker on No/none, debounce + stale-response protection, change course / restart | `/api/wwwroot/index.html`, `/api/wwwroot/index.js`, `/api/wwwroot/styles.css` |
| Assignment-type selection screen (Carter) — seven options when type confidence is low or type invalid; skips for valid high-confidence types; answer survives course change, resets on new upload; `recommendation-ready` event for next step | `/api/wwwroot/index.html`, `/api/wwwroot/index.js`, `/api/wwwroot/styles.css` |
| Recommendation screen (Carter) — auto `POST /api/recommend` after confirm; resource, reason, map/online link, next open, session type, contact, booking, verified/stale, refusal notice, syllabus tip; handoffs for non-academic/distress; missing fields → "Contact the office to confirm." | `/api/wwwroot/index.html`, `/api/wwwroot/index.js`, `/api/wwwroot/styles.css` |

## Owners

| Owner | Owns |
|---|---|
| Cole | `AiService`, `CourseService`, `RecommendationService`, `DatabaseConfig`, `/api/Prompts/`, `/database/`, `AnalyzeController`, `CoursesController`, `RecommendController` (and related analyze/course/recommend models + services he introduced); Sprint 1 Story 2 |
| Carter | Frontend in `/api/wwwroot/` |

Do not edit another teammate’s files. Ask the owner first.

## Open

- Carter main screen implemented locally on `carter/main-screen`: upload button, optional message, client file checks, loading/error/success states, and relative `POST /api/analyze` request (`/api/wwwroot/index.html`, `index.js`, `styles.css`). Pending review; not yet a complete Story 1 flow.
- Academic uploads now open course confirmation for a valid high/low-confidence match, or searchable course selection when no match exists. Yes confirms the guessed course; No opens search. Search uses `GET /api/courses?search=`, with debouncing, stale-response protection, empty/error/retry states, and keyboard-accessible result buttons. Confirmed courses can be changed, or the upload flow restarted.
- Course confirmation/search: classification stays in memory and course selection makes no new AI calls. Desktop Chrome visual verification remains pending.
- Assignment-type selection: answers survive course changes and reset on a new upload/start over. The confirmed-details screen displays the type; a `recommendation-ready` event carries the course ID, selected type (including `other`), category, and refusal flag. No extra AI calls.
- Assignment-type checks: .NET build passed (0 warnings/errors), JavaScript syntax passed, and mocked interaction checks passed for all seven options, confidence branching, invalid-type fallback, answer retention/reset, Other payload, and non-academic/distress bypass. Five mocked low-confidence classifications triggered the question; this is not the sprint's real classifier evaluation. Desktop Chrome visual verification remains pending.
- Recommendation screen: confirmed details automatically POST to `/api/recommend` without another click or AI call. Loading, 10-second request timeout, retry, change-course, restart, and stale-response cancellation are implemented. Non-academic/distress classifications bypass course selection and request the backend handoff; no tutoring resource is shown for handoff responses. Internal TODO verification placeholders are replaced with the missing-info message, not exposed to students.
- Recommendation checks: .NET build (0 warnings/errors), JavaScript syntax, and mocked request/render checks passed for automatic submission, request fields, locations, missing fields, unsafe links, flags, errors/retry/timeout, invalid response, cancellation, handoffs, and Other selection. No OpenAI calls made. Desktop Chrome and live-database end-to-end verification remain pending.
- Backend/data dependencies before acceptance: Mary must verify and supply the Counseling Center contact currently marked TODO in Cole's handoff response. Story 5's Other-specific general support, human contact, and syllabus fallback must be verified with Cole; the frontend sends `other` unchanged and does not override backend resource selection.
- Course-flow checks: .NET build passed with 0 warnings/errors; JavaScript syntax and mocked interaction checks passed for confirmation, search selection, preserved classification context, empty/error/retry states, stale responses, reset, and distress bypass. No OpenAI calls made. Browser preview host was unavailable, so visual verification remains pending.
- Main-screen checks: `dotnet build NextStep.sln` passed (0 warnings/errors), JavaScript syntax passed, and mocked upload checks passed for invalid/empty/oversized files, multipart submission, loading, success, and server-error recovery. No OpenAI calls made. Desktop Chrome visual verification remains pending because browser preview timed out.
- Core recommendation and handoff frontend merged to main; desktop Chrome/end-to-end acceptance checks still pending.
- Sprint 1 stretch follow-ups: dedicated "Talk to a person" option and "What to expect" section. Reason and last-checked date are now displayed in the core recommendation screen.
