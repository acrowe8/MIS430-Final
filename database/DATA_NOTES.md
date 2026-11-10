# NextStep UA – Database Data Notes

## How to load
1. Run `schema.sql` on the empty database (creates the 10 tables from the ERD).
2. Run `seed.sql` (loads all data in one transaction).
The CSVs in `data/` hold the same data, one file per table, for reference.

## What's in it
| Table | Rows | Source |
|---|---|---|
| subject | 132 | Official UA 2026–27 catalog, subject code table |
| course | 3,970 | Official UA 2026–27 Undergraduate Catalog (every course) |
| assignment_type | 7 | The 7 types the AI returns |
| resource | 43 | Team resource research (Mary) |
| location | 30 | Team resource research |
| schedule | 130 | Team resource research |
| resource_course | 63 | Team resource research + course lists on the cited UA pages |
| resource_assignment_type | 101 | Mapped from the research's assignment-type text |
| resource_subject, course_example | 0 | Nothing to add yet |

## Fixed (verified errors)
- **CHINESE 201** was linked to CHI 102 (Elementary Chinese II). Now linked to **CHI 201** (Intermediate Chinese I). The tutoring page lists "Chinese 101 & 201." "CHINESE" codes now use UA's official code, **CHI**.
- **PY 211** was labeled Physics. In the catalog it's **Elementary Statistical Methods**, a Psychology course. Its description now says "Statistics problem sets."
- **PHL** was named "Physics" in the earlier subjects file. It's **Philosophy**. PH is Physics. `ua_subjects_2026-27.csv` in this folder is the corrected replacement.
- **NSE** was "Nursing Science (verify code)." It's **National Student Exchange** (Nursing Science is NUS).
- Subject names use the official catalog wording.
- Course titles and descriptions come from the verified catalog extraction, which keeps hyphens like "C- or higher" that an earlier copy had dropped.
- Removed 2 exact duplicate CS 100 SI sessions (they were listed twice).

## Restructured (no facts changed)
- One resource per service + course (e.g. "Learning Commons Peer Tutoring – CH 101"), so each course gets its own rooms and times.
- Multi-day rows were split into one schedule row per day. Times were converted to 24-hour format.
- `session_type` is `appointment` where "Appointment Required" was Yes, otherwise `drop_in`. The original wording is kept in `session_detail`, and "Recommended" is kept in `notes`.
- Range rows were linked to the exact course lists on their cited pages:
  - Peer tutoring MATH 005–125 → MATH 005, 100, 110, 112, 113, 115, 121, 125
  - MTLC → its 12 listed courses
  - Online 1:1 → its 8 listed courses
  - ECE 225/320 → both courses
- The research's assignment-type text was mapped to the AI's 7 types. The original text is kept in `resource.description`.
- The ENGenuity lab's "open Aug 31–Dec 1, 2026" note was turned into a start date and end date.
- General resources (Writing Center, Online Writing Center, Speaking Studio, Academic Coaching) are marked `is_general`. TRIO is marked "Active TRIO SSS students only."

## Added
- Contact info for Learning Commons Peer Tutoring and Online 1:1: ccssinfo@ua.edu, (205) 348-7011. Both come from the official peer tutoring page.

## Still needed before testing (Mon 11/16)
- `what_to_expect` for every resource
- Contact info for SI, Academic Coaching, Writing Center, Speaking Studio, MTLC, ENGenuity, Engineering Math Coaches, and TRIO
- Capstone Center and UA Counseling Center contacts for the handoff screens (story 7)
- Re-check times against the official pages. The research notes say they weren't re-verified, and the Writing Center and Speaking Studio rows say "confirm current schedule."
- ENGenuity is marked "Appointment Required: Yes," but its notes say "appointment policy not specified." Confirm which is right.
- Engineering Math Coaches ("MATH 112–238") isn't linked to any courses yet. Confirm which courses it covers and who's eligible.
- Math test-week room changes (Nov 17–20) aren't in the data.
