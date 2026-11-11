# Sprint 1 Plan

**Dates:** Mon 11/9 – Wed 11/18
**Team:** Cole Hoffman (PM/Developer), Carter White (Developer), Mary Slawson (Research/Data), Allie Crowe (Notes/Testing)

## Sprint Goal
A working prototype where a student can upload an assignment, confirm the course, and get a real UA resource recommendation, with all guardrails working, ready for user testing on Wed 11/18.

## Sprint Backlog
| ID | Story | Priority | Points | Owner |
|---|---|---|---|---|
| 1 | Upload assignment; AI identifies the course with a Yes/No check | Must | 5 | Carter |
| 2 | AI recommends the best-fit UA resource (`POST /api/recommend`) | Must | 8 | Cole |
| 3 | Real, accurate UA resource data | Must | 5 | Mary |
| 4 | Show building, room, hours, and contact info | Must | 3 | Carter |
| 5 | Follow-up question when the assignment type is unclear | Must | 5 | Cole |
| 6 | Refuse to help with the assignment itself | Must | 3 | Cole (build), Allie (testing) |
| 7 | Hand off to a real person for non-academic requests | Must | 3 | Cole (build), Allie (testing) |
| 8 | Human contact option on every recommendation | Stretch | 2 | Carter |
| 9 | Why it was recommended + last updated date | Stretch | 2 | Carter |
| 10 | What to expect before you go | Stretch | 2 | Carter, Mary (text) |

## Acceptance Criteria

### Story 1: Upload and course identification
- The home screen shows one upload button, and nothing else is needed to start.
- PDF, Word, and image files up to 10 MB are accepted. Any other file type shows a clear error message.
- After upload, the tool shows its best guess ("Is this for CH 101 – General Chemistry?") with Yes and No buttons.
- Yes goes straight to the recommendation. No opens a searchable course list.
- If the AI can't make any guess, it skips to the course list.
- When the guess is right, a student gets from the home screen to a recommendation in 3 clicks or fewer.
- The uploaded file is not saved anywhere after the recommendation is shown.

### Story 2: AI recommendation
- After the student confirms the course, the tool shows one recommended UA resource with its building, room, hours, and contact info.
- The recommendation includes one sentence explaining why it was picked.
- The recommendation shows up within 10 seconds, with a loading message while the student waits.
- The AI checks for a course resource first, then a subject resource, then a general one.
- When the course has no dedicated resource, the recommendation includes the syllabus tip: "We couldn't find help specific to this class. Your syllabus usually lists TA office hours and course help. Check your class page on Blackboard or UA's Simple Syllabus page."
- If the AI isn't confident about the assignment type, it asks a follow-up question instead of recommending.
- If the student asks for help with the assignment itself, the AI declines and still gives the recommendation.
- If the request isn't about academic help, the AI doesn't recommend anything and gives a human contact instead.

### Story 3: Resource data
- Covers at least EN 101/102, CH 101/102, BSC 108/109, GEO 101/102, EC 110/111, ST 260, and MATH 100–125, plus general options (Writing Center, general tutoring).
- Each of those courses has its catalog description.
- Every resource has a name, building and room (or "online"), days and times, drop-in or appointment for each time slot, contact email or phone, source link, and last verified date.
- No field says blank or "Verify" for any resource used in testing.
- Every resource is checked against its official UA page by Mon 11/16, before testing starts on Wed 11/18.
- The data is loaded into the database tables from the ERD.

### Story 4: Resource info screen
- Shows the resource name, building and room (or "Online" with a link), drop-in or appointment, and contact email or phone.
- Shows the next time it's open (for example, "Next open: Tue 5–8 PM").
- The building name links to the UA campus map.
- If a booking page exists, a button links to it.
- Missing fields say "Contact the office to confirm" instead of being left blank.

### Story 5: Follow-up question
- If the AI isn't confident about the assignment type, it asks "What kind of assignment is this?" with clickable options (Essay or paper, Lab report, Problem set or homework, Coding project, Presentation, Exam prep, Other).
- The student's answer is used to pick the recommendation.
- No more than one follow-up question per upload.
- Picking Other gives general tutoring, a human contact, and the syllabus tip.
- With 5 deliberately vague test uploads, the tool asks instead of guessing on at least 4.

### Story 6: Refusal
- If asked to answer, solve, explain, edit, or check the assignment, the tool declines in one friendly sentence and still shows the recommendation.
- It never gives answers, worked solutions, hints, edits, or summaries of the assignment.
- Instructions written inside an uploaded file are ignored.
- The team tries at least 10 different ways to get answers out of it, and it refuses all 10.

### Story 7: Human handoff
- Non-academic requests get no tutoring recommendation, just the Capstone Center for Student Success contact info.
- Signs of distress skip the recommendation completely and show UA Counseling Center contact info and the 988 Suicide & Crisis Lifeline.
- The tool never counsels or diagnoses.
- Counseling Center contact info is verified by Mary.
- With 5 non-academic and 5 distress-style test messages, the tool gives the right handoff on all 10.

### Story 8 (stretch): Human contact option
- Every recommendation shows a "Talk to a person" option with that resource's contact info, or the Capstone Center's if the resource has none.
- It's visible without scrolling.

### Story 9 (stretch): Why it was recommended + last updated
- Every recommendation shows one sentence on why it was picked.
- Every recommendation shows "Info last checked: [date]."
- If the info is more than 30 days old, it adds: "Hours may have changed. Confirm before you go."

### Story 10 (stretch): What to expect
- Every recommendation shows 2–3 lines on whether it's drop-in or an appointment, what to bring, and roughly how long a visit takes.
- It includes the line: "You don't need to know exactly what to ask. Just bring the assignment."
- The text comes from the resource's what_to_expect field.

## Definition of Done
A story only counts as done when:
- It meets every acceptance criterion
- It's merged into main through a reviewed pull request
- It works in Chrome on a desktop browser
- No API keys or uploaded files are in the repo
- Any meaningful AI-generated code we changed or rejected is in the decision log

## Not in This Sprint
- Shoulds 11, 12, 21, 22, 24: core flow and guardrails come first
- Coulds 13–16, 23: not needed to test the core idea
- Won'ts 17–20: out of scope
