# Security & Performance Audit

**Systems audited:** `MohamedSaeed-dev/Jaberah-ASP` (ASP.NET Core 9 REST API) and `MohamedSaeed-dev/Jaberah-Flutter` (Flutter mobile client)
**Audit date:** 2026-09-04
**Commits audited:** `Jaberah-ASP` @ `d76af61` (branch `main-v2`) · `Jaberah-Flutter` @ `1bb83b0` (branch `main-v2`, app version 3.2.0)
**Production API:** `https://jaberah-new.tryasp.net/api` — the deployment both `main-v2` branches target. A second, older deployment (`https://jaberah-new.tryasp.net/api`) is served from the `master` branches; every reproduction below is written against the production host.
**Scope:** Full source of both repositories — authentication/authorization, all 12 API controllers, EF Core model and indexes, middleware pipeline, background jobs, external integrations (Firebase, Dropbox), CI/CD workflows, Android app configuration, and git history.
**Nature of this engagement:** Audit only. No application code was changed. This report is the sole deliverable.
**Revision 2 (branch correction):** the first pass reviewed the Flutter client on `master` @ `7eaf11d`. Production is `main-v2`, which is 23 commits ahead (`master` is a strict ancestor, with no commits of its own). Every client-side finding has been re-verified against `main-v2`: **SEC-017 is fixed there**, as are two Potential Risks; **SEC-003 is revised**, because the behaviour it describes turns out to be a feature the admin screen depends on. The backend was already audited on `main-v2` and is unchanged (`d76af61`, 0 new commits). See *Status on the production branch* below.

> This single report covers both repositories. It is committed to `Jaberah-ASP` because the majority of findings are server-side; findings SEC-004, SEC-012, SEC-017 and the CI observations in the Appendix belong to `Jaberah-Flutter`.

---

## Executive Summary

Jaberah is a Quran-circle (*halaqa*) management system for a single mosque: an ASP.NET Core 9 API (~8.1k LOC, SQL Server + EF Core 9, Hangfire, Serilog) fronted by a Flutter Android client (~18.4k LOC, GetX + Dio) distributed as a side-loaded APK. There are exactly two roles — `ADMIN` and `TEACHER` — and a single tenant. Authentication is a HS256 JWT presented as a bearer token; a `VerifyTokenAttribute` action filter re-loads the caller from the database on each request and stashes it in `HttpContext.Items["User"]`, and an `IsAdminAttribute` filter narrows individual actions to admins.

The codebase shows evidence of a prior hardening pass — the Hangfire dashboard authorization filter, the deploy-key filter, the CORS allow-list, log redaction, the pagination `ORDER BY` fixes and the cleaning-logs module all carry Arabic commentary describing a vulnerability that was closed, and there is a unit-test suite that specifically asserts group-ownership rules for cleaning logs. **That hardening was applied module by module and did not reach the rest of the application.** The cleaning-logs controller scopes every query to `CurrentUser.Id`; the reports, exams, follow-students and prayers controllers — which hold the entire academic and attendance record for all 122 students — perform no ownership check at all and trust whatever `studentId` or `groupId` the caller sends.

The two dominant issues are independent of each other and each is sufficient on its own to compromise the system:

1. **Both repositories are public on GitHub, and `Jaberah/SeedData/Teachers.json` contains the BCrypt password hashes, real phone numbers and live FCM device tokens of 10 real staff accounts — one of them an administrator.** Because `AddTeacher` sets a new teacher's initial password to their own phone number, and that phone number sits in the same file as the hash, anyone can verify offline in a single BCrypt operation whether an account still uses its default credential — and then log in. `SeedData/Students.json` additionally publishes 122 minors' names, guardian phone numbers, school levels and free-text notes.

2. **Any authenticated teacher can read and modify every student's academic record institution-wide.** Grades (`POST /api/exams/monthly-exam`, `POST /api/exams/mid-final-exam`), daily attendance and behaviour scores (`POST /api/follow-students/attendance`), memorisation records (`POST /api/follow-students`) and prayer attendance (`POST /api/prayers/upsert-daily`) all accept an arbitrary `studentId` with no ownership or admin check. The read side is equally open: `GET /api/reports/monthly-report?groupId=0` returns every student in the database with their full lesson history in a single request.

On the performance side the system is small enough today (10 teachers, 10 groups, 122 students) that most inefficiencies are latent rather than active. Two are not: `HttpClient` is registered as a **scoped** service and is therefore constructed and disposed on every request to the anonymous, called-on-every-app-launch `GET /api/versions` endpoint (socket exhaustion under load), and `GET /api/reports/monthly-report` will materialise every `SaveLesson` and `ReviewLesson` row in the database into managed memory when given a wide date range, with no date-range validation and with `take` applied only *after* the data is loaded.

No SQL, NoSQL, command or template injection was found. The only raw SQL in the codebase (`SeedData/DataSeeder.cs`) is fully parameterised and is not reachable from any HTTP endpoint. No secrets are present in `appsettings*.json` (correctly git-ignored) or anywhere in the commit history of either repository — the credential exposure comes entirely from the seed-data fixtures.

---

## Risk Summary

### Security

| Severity | Count | IDs |
|---|---|---|
| Critical | 2 | SEC-001, SEC-002 |
| High | 5 | SEC-003, SEC-004, SEC-005, SEC-006, SEC-007 |
| Medium | 5 | SEC-008, SEC-009, SEC-010, SEC-011, SEC-012 |
| Low | 5 | SEC-013, SEC-014, SEC-015, SEC-016, SEC-018 |
| **Total open** | **17** | |
| Resolved on `main-v2` | 1 | SEC-017 |

IDs are stable across revisions: SEC-017 keeps its number and is retained below marked resolved, rather than being deleted and the rest renumbered.

**Overall Security Risk: CRITICAL**

Justification: a publicly readable file yields a probable direct path to an administrator session (SEC-001), and even without it, every teacher account is effectively an administrator over all student academic data (SEC-002). There is no rate limiting, no password policy on change, and no token revocation, so neither issue is bounded by a compensating control.

### Performance

| Severity | Count | IDs |
|---|---|---|
| Critical | 1 | PERF-001 |
| High | 1 | PERF-002 |
| Medium | 6 | PERF-003, PERF-004, PERF-005, PERF-006, PERF-007, PERF-008 |
| Low | 4 | PERF-009, PERF-010, PERF-011, PERF-012 |
| **Total open** | **12** | |

**Overall Performance Risk: HIGH**

Justification: two findings (PERF-001, PERF-002) are triggerable by any authenticated caller and scale with data volume rather than with request count, and PERF-002 fires on the single most-hit endpoint in the system. At the current data volume the API is unlikely to be visibly slow; both findings degrade sharply and non-linearly as records accumulate, and PERF-001 is directly weaponisable as a denial of service today.

### Status on the production branch

Re-verified against `Jaberah-Flutter` @ `1bb83b0` (`main-v2`) after the branch correction. The client has moved substantially since `master`: 23 commits, app version 2.0.1 → 3.2.0, new cleaning-log, daily-prayer, prayer-report and salary screens, biometric confirmation, request timeouts, and a refresh-token lock.

**Already fixed on `main-v2` — no action needed:**

| Item | Evidence on `main-v2` |
|---|---|
| SEC-017 — token in plaintext `SharedPreferences` | New `lib/api/tokenStorage.dart` stores it via `flutter_secure_storage` with `AndroidOptions(encryptedSharedPreferences: true)`, and migrates any legacy plaintext token on first read (`:28-37`). `pubspec.yaml:44` adds the dependency. |
| Risk #4 — CI publish step sent no deploy key | `.github/workflows/flutter-build.yml:53-70` now sends `-H "X-Deploy-Key: ${{ secrets.DEPLOY_KEY }}"`, on separate `master` and `main-v2` publish steps, and the workflow gained `Analyze` and `Test` steps. |
| Risk #5 — monthly-report client/server contract mismatch | Both call sites now send `fromDate`/`toDate` (`admin/monthlyReportController.dart:80`, `user/monthlyStudentsReports.dart:62`), matching the action signature. |

**Re-verified as still present on `main-v2`:** SEC-004 (`android/app/build.gradle:46-52` unchanged — still `signingConfig = signingConfigs.debug`), SEC-012 (`AndroidManifest.xml` still carries `usesCleartextTraffic="true"`, `MANAGE_EXTERNAL_STORAGE` and `requestLegacyExternalStorage`; two biometric permissions were added), and the unencoded-`searchText` and in-memory-`CookieJar` items in Risk #9. Every server-side finding is unaffected — the backend was audited on `main-v2` at `d76af61`, which is still its head.

**Revised by the branch correction:** SEC-003. On `master` the `groupId=0` all-groups path looked like an unintended sentinel bypass. On `main-v2` the admin report screen has an explicit «كل الحلقات» (all circles) option that omits `groupId` on purpose, so the capability is a feature and the finding is really that a **non-admin** can reach it. Severity is unchanged; the recommended fix is different, and PERF-001's remediation is corrected to match.

### Top 5 Most Important Issues

| # | ID | Title | Why it leads |
|---|---|---|---|
| 1 | **SEC-001** | Production password hashes, staff phone numbers and 122 students' PII in a public repository | Requires no authentication to exploit, plausibly yields administrator access, and the personal-data exposure is already irreversible |
| 2 | **SEC-002** | Broken access control: any teacher can read and write any student's academic record | Every teacher account is an administrator over the data that is the entire point of the product; grades and attendance can be silently forged |
| 3 | **SEC-004** | Release APK is signed with the Android debug keystore | The signing key is universally known, so a trojanised build installs over the real app as a legitimate update |
| 4 | **SEC-003** | `groupId=0` turns `monthly-report` into a whole-database export | One request by any teacher extracts the full student body with complete lesson histories |
| 5 | **PERF-001** | `monthly-report` has no date-range bound and materialises all matching rows | Same request is a self-service denial of service against the API and its database |

SEC-005 (no password policy on change) and SEC-006 (no login rate limiting) do not appear in the top five in their own right, but both directly amplify SEC-001 and should be fixed in the same change window.

---

## Critical Findings

## [SEC-001] Production credential hashes, staff phone numbers, FCM tokens and 122 students' personal data committed to a public repository

- **Category:** Security
- **Severity:** Critical
- **Confidence:** High
- **Location:** `Jaberah/SeedData/Teachers.json` (all 10 records) · `Jaberah/SeedData/Students.json` (all 122 records) · exploit chain via `Jaberah/Controllers/TeachersController.cs:176` and `Jaberah/Controllers/AuthController.cs:19-37`

### Description

`Jaberah/SeedData/Teachers.json` is a committed fixture containing what is plainly a dump of the live `Teachers` table: real Arabic names, real Yemeni mobile numbers, `Role` values, **BCrypt password hashes**, **live Firebase Cloud Messaging device tokens**, and `LastLogin` timestamps from 2026. `Jaberah/SeedData/Students.json` is the same for the `Students` table: 122 students with names, guardian phone numbers (all 122 populated), school class, school level, study level and free-text notes.

Repository visibility was verified through the GitHub API: `MohamedSaeed-dev/Jaberah-ASP` returns `"private": false, "visibility": "public"`. `MohamedSaeed-dev/Jaberah-Flutter` is also public. Both files are therefore readable by anyone, with no authentication, and are already indexed and cloneable.

What converts this from a serious data-protection incident into a probable authentication bypass is the account-creation path. `TeachersController.AddTeacher` sets a new teacher's initial password to their own phone number:

```csharp
// Jaberah/Controllers/TeachersController.cs:176
var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.PhoneNumber);
```

There is no forced password change on first login, no password-age tracking and no `MustChangePassword` flag anywhere in the `Teacher` entity (`Jaberah/Models/JaberahModels/Teachers.cs:5-14`). Any account that has never voluntarily changed its password still has its phone number as its password — and `Teachers.json` publishes the login name **and** the phone number **and** the hash for verification, side by side.

### Evidence

```jsonc
// Jaberah/SeedData/Teachers.json — first record, values verbatim from the repository
{
  "Id": 2,
  "TeacherName": "مدرسة ابن الوليد",
  "PhoneNumber": "777652443",
  "Password": "$2a$11$rjzELJDfE/M7ta4ouCrksejYUUS6r..JREIGSMHNiwgfM8qf9SU4m",
  "Role": 1,                                  // 1 == Role.ADMIN
  "FCMToken": "elsqyT8TSbSIIt44CcoSlj:APA91bGtNretTctPlU6SBY72HS52kpmKjSVWn...",
  "LastLogin": "2026-03-12 14:03:01.4352258"
}
// 9 further records follow, all with Password, PhoneNumber and FCMToken populated.
```

```csharp
// Jaberah/Controllers/AuthController.cs:19-37 — login takes the teacher's *name* as the username
[AllowAnonymous]
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginDTO model)
{
    var teacher = await _db.Teachers.Include(x => x.Groups)
        .FirstOrDefaultAsync(t => t.Name == model.Username.Trim());
    ...
    var isPasswordValid = BCrypt.Net.BCrypt.Verify(model.Password, teacher.Password);
```

`Role = 1` is `Role.ADMIN` per `Jaberah/Models/JaberahModels/Teachers.cs:16-20`, so record `Id: 2` is an administrator account whose username and probable password are both published.

### Impact

- **Probable full administrative compromise.** If the administrator account `مدرسة ابن الوليد` has not changed its password, `POST /api/auth/login` with that name and `777652443` returns an admin access token. Admin authority in this system includes creating, deleting and permanently purging teachers, students and groups; setting and marking salaries paid; broadcasting push notifications to every installed app; and reading the request/response log through `GET /api/Logs`.
- **Offline attack on every remaining account.** The 10 hashes are BCrypt cost 11, which is a reasonable work factor, but the candidate space is not: passwords are known to be 9-digit numbers matching `^7\d{8}$` (`Jaberah/Helpers/StringExtensions.cs:21-28`), and the *exact* phone number for each hash is in the same file. Verifying "is this account still on its default?" is one hash per account, not a search.
- **Irreversible personal-data exposure.** 122 identifiable minors' names, their guardians' phone numbers, their school and study level, and subjective free-text notes ("ممتاز") are published. This exposure cannot be undone by rotating anything; the data is already redistributable.
- **Staff device tracking.** The FCM registration tokens identify specific staff devices. They cannot be used to send notifications without the Firebase service-account key (which is correctly *not* in the repository — see `Jaberah/.gitignore:16-17`), but they are persistent device identifiers tied to named individuals.

### Reproduction / Trigger

1. `git clone https://github.com/MohamedSaeed-dev/Jaberah-ASP.git` — no credentials required.
2. `cat Jaberah/SeedData/Teachers.json` — read name, phone number, role and hash for all 10 accounts.
3. For each record, one `BCrypt.Verify(phoneNumber, hash)` call reveals whether the account is still on its default password.
4. For any match, `POST https://jaberah-new.tryasp.net/api/auth/login` with `{"username": "<TeacherName>", "password": "<PhoneNumber>", "fcmToken": "x"}` returns a valid access token with that account's role.
5. Step 4 is unthrottled — see SEC-006.

### Recommended Remediation

1. **Treat every credential in the file as compromised and rotate now**, before any repository change. Force-reset all 10 teacher passwords to individually generated random values delivered out of band. Rotate the FCM tokens by having each device re-register (the client already calls `FirebaseMessaging.deleteToken()` on logout).
2. **Delete both seed files from the working tree and purge them from history** (`git filter-repo --path Jaberah/SeedData/Teachers.json --path Jaberah/SeedData/Students.json --invert-paths`, then force-push and ask GitHub Support to expire cached views). Add `Jaberah/SeedData/*.json` to `.gitignore`. Assume the data is already copied and do not rely on the purge as the primary control.
3. **Make both repositories private** unless there is a deliberate reason for them to be public. This system holds children's personal data; a public repository is the wrong default for it.
4. **Replace the fixtures with synthetic data** if seeding is still wanted — invented names, `+000000000` phone numbers, and a single hard-coded bootstrap admin whose password comes from configuration. Note that `DataSeeder.SeedAsync` is already commented out at `Jaberah/Program.cs:138`, so the files serve no runtime purpose today.
5. **Stop deriving initial passwords from public attributes.** Generate a random single-use password at `TeachersController.cs:176`, and add a `MustChangePassword` column that `AuthController.Login` honours by refusing anything other than the password-change endpoint until it is cleared.
6. **Consider notifying the affected individuals / guardians.** A public disclosure of minors' identifying data is likely to carry a notification obligation under applicable data-protection law; that call should be made by the project owner with legal input, not deferred.

### Exploitability

**Exploitable and observable today.** The files are public and their contents were read during this audit. Whether step 4 yields a live session depends on whether the specific accounts have changed their passwords since creation — which cannot be determined without attempting authentication, and was not attempted. Steps 1–3 and the PII exposure are confirmed unconditionally.

### References

- OWASP Top 10 2021 — A07:2021 Identification and Authentication Failures; A02:2021 Cryptographic Failures
- CWE-798: Use of Hard-coded Credentials · CWE-1392: Use of Default Credentials · CWE-359: Exposure of Private Personal Information

---

## [SEC-002] Broken access control: any authenticated teacher can read and modify every student's academic record

- **Category:** Security
- **Severity:** Critical
- **Confidence:** High
- **Location:** `Jaberah/Controllers/ExamsController.cs:20-21, 45-46` · `Jaberah/Controllers/FollowStudentsController.cs:18-19, 98-99, 202-203, 264-265, 340-341` · `Jaberah/Controllers/PrayersController.cs:54-55, 132-133, 170-171` · `Jaberah/Controllers/ReportsController.cs:19-20, 100-101`

### Description

Four controllers holding the complete academic record of every student are protected only by the controller-level `[ServiceFilter(typeof(VerifyTokenAttribute))]`, which proves *that* the caller is a valid teacher and nothing more. None of the actions listed above carry `[IsAdmin]`, and none check that the `studentId` or `groupId` in the request belongs to a group the caller teaches. The caller's own identity is available — `HttpContext.Items["User"]` is populated by the filter and `Jaberah/Helpers/CurrentUserExtensions.cs` exposes `CurrentUser()`, `IsCurrentUserAdmin()` and `CanActOnTeacher(int)` — but these helpers are simply not called in these files.

That the omission is an oversight rather than a design decision is demonstrable from the codebase itself. `CleaningLogsController` does the correct thing explicitly, with a comment stating the rule:

```csharp
// Jaberah/Controllers/CleaningLogsController.cs:195-202
// شاشة كشف النظافة شاشة معلم، فنطاقها حلقات المستدعي نفسه مهما كان دوره.
var callerId = CurrentUser.Id;

var studentsQuery = _db.Students
    .AsNoTracking()
    .Where(s => s.Group != null && s.Group.TeacherId == callerId);
```

and `Jaberah.Tests/CleaningLogRulesTests.cs` asserts it (`A_teacher_cannot_assign_a_student_from_another_group`, `A_teacher_cannot_take_over_a_task_another_group_already_holds`). `TeachersController` and `TeachersAttendancesController` likewise gate per-teacher reads with `this.CanActOnTeacher(teacherId)`. The four controllers above were never given the equivalent treatment.

The write endpoints are the more serious half, because the values they accept feed directly into the grade calculation in `ReportsController`.

### Evidence

```csharp
// Jaberah/Controllers/ExamsController.cs:20-31 — no [IsAdmin], no ownership check.
// model.StudentId is taken from the request body and used verbatim.
[HttpPost("monthly-exam")]
public async Task<IActionResult> UpsertMonthlyExams([FromBody] UpsertMonthlyExamsDTO model)
{
    if (model.StudentId <= 0) return BadRequest(new { message = "ادخل id صحيح" });

    var exam = await _db.Exams.FirstOrDefaultAsync(x => x.StudentId == model.StudentId && x.Date == model.Date);

    if (exam is not null) // update
    {
        exam.PaperExam = Math.Max(Math.Min(model.PaperExam ?? exam.PaperExam, 20), 0);
        exam.OralExam  = Math.Max(Math.Min(model.OralExam  ?? exam.OralExam,  10), 0);
```

```csharp
// Jaberah/Controllers/FollowStudentsController.cs:340-348 — attendance and behaviour for any student
[HttpPost("attendance")]
public async Task<IActionResult> UpsertAttendanceAndBehavior([FromQuery] DateTime date, [FromBody] UpsertAttendanceAndBehaviorDTO model)
{
    if (date.Equals(default)) return BadRequest(...);
    if (model.StudentId <= 0) return BadRequest(...);
    if (!await _db.Students.AnyAsync(x => x.Id == model.StudentId))   // existence only
        return BadRequest(new { message = "لايوجد طالب" });
```

```csharp
// Jaberah/Controllers/ReportsController.cs:19-24 — any teacher, any group's full semester report
[HttpGet("semester-report")]
public async Task<IActionResult> GetSemesterReport([FromQuery] int groupId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
{
    if (groupId <= 0)
        return BadRequest(new { message = "ادخل id صحيح" });
    if (!await _db.Groups.AnyAsync(x => x.Id == groupId))             // existence only
        return BadRequest(new { message = "لاتوجد حلقة" });
```

The complete set of unscoped actions:

| Endpoint | Verb | Line | Effect available to any teacher |
|---|---|---|---|
| `/api/exams/monthly-exam` | POST | `ExamsController.cs:20` | Write paper/oral exam marks for any student |
| `/api/exams/mid-final-exam` | POST | `ExamsController.cs:45` | Write mid-final grade for any student (unbounded value — see SEC-011) |
| `/api/follow-students` | POST | `FollowStudentsController.cs:264` | Write memorisation and revision records for any student |
| `/api/follow-students/attendance` | POST | `FollowStudentsController.cs:340` | Write attendance and behaviour scores for any student |
| `/api/prayers/upsert-daily` | POST | `PrayersController.cs:132` | Write prayer attendance for any student |
| `/api/follow-students/students/{id}/for-day` | GET | `FollowStudentsController.cs:18` | Read any student's daily record |
| `/api/follow-students/students/{id}/for-month` | GET | `FollowStudentsController.cs:98` | Read any student's monthly record |
| `/api/follow-students/groups/{id}/for-day` | GET | `FollowStudentsController.cs:202` | Read any group's whole daily sheet |
| `/api/prayers/daily` | GET | `PrayersController.cs:54` | Read all students' prayer attendance |
| `/api/prayers/monthly-report` | GET | `PrayersController.cs:170` | Read all students' monthly prayer statistics |
| `/api/reports/semester-report` | GET | `ReportsController.cs:19` | Read any group's full semester grades |
| `/api/reports/monthly-report` | GET | `ReportsController.cs:100` | Read any group's — or all groups' — full monthly report (see SEC-003) |

### Impact

- **Silent grade forgery.** A teacher can raise their own students' marks or lower a rival group's, and the change is indistinguishable from legitimate data entry: `BaseEntity` records only `CreatedAt`/`UpdatedAt`/`DeletedAt` (`Jaberah/Models/JaberahModels/BaseEntity.cs`) and no actor. There is no audit trail identifying who wrote a grade, so this is not merely undetected but **undetectable after the fact**.
- **Grades and attendance are the product.** `ReportsController.GetSemesterReport:64-95` computes each student's semester total from exactly these tables — `StudentAttendances`, `SaveLessons`, `Exams`, `MidFinals`. Everything the system exists to produce is writable by any teacher for any student.
- **Full academic-record disclosure.** Every teacher can read every other group's grades, attendance, behaviour scores and free-text notes. In a single-tenant mosque application this is a confidentiality breach between colleagues and, given the subjects are minors, a personal-data breach.
- **Amplification.** Combined with SEC-001, an unauthenticated party on the internet reaches all of the above.

### Reproduction / Trigger

With any teacher's access token (obtained legitimately or via SEC-001):

```bash
# Read a group the caller does not teach — replace 3 with any group id
curl -H "Authorization: Bearer $TEACHER_TOKEN" \
  "https://jaberah-new.tryasp.net/api/reports/semester-report?groupId=3&fromDate=2026-01-01&toDate=2026-05-01"

# Overwrite another group's student's exam marks — replace 47 with any student id
curl -X POST -H "Authorization: Bearer $TEACHER_TOKEN" -H "Content-Type: application/json" \
  -d '{"studentId":47,"paperExam":20,"oralExam":10,"date":"2026-03-01T00:00:00"}' \
  "https://jaberah-new.tryasp.net/api/exams/monthly-exam"

# Zero out another student's attendance and behaviour for a day
curl -X POST -H "Authorization: Bearer $TEACHER_TOKEN" -H "Content-Type: application/json" \
  -d '{"studentId":47,"attendance":0,"behavior":0}' \
  "https://jaberah-new.tryasp.net/api/follow-students/attendance?date=2026-03-01T00:00:00"
```

Both requests return `200 OK`. Only the caller's own token is needed; group ids 1–10 and student ids 1–122 are contiguous and, per SEC-001, published.

### Recommended Remediation

1. **Add an ownership predicate to every action listed above**, reusing the pattern already proven in `CleaningLogsController`. Two helpers on `JaberahDBContext` or an extension class cover all cases:
   ```csharp
   Task<bool> CallerOwnsStudentAsync(int studentId) =>
       IsCurrentUserAdmin() || _db.Students.AnyAsync(s =>
           s.Id == studentId && s.Group != null && s.Group.TeacherId == CurrentUser().Id);

   Task<bool> CallerOwnsGroupAsync(int groupId) =>
       IsCurrentUserAdmin() || _db.Groups.AnyAsync(g => g.Id == groupId && g.TeacherId == CurrentUser().Id);
   ```
   Return `Forbid()` when false — matching the convention at `TeachersController.cs:84-85`.
2. **Fail closed by default.** The safer structural fix is to apply `[IsAdmin]` at controller level and remove it only from actions that have an explicit ownership check, so that a newly added action is admin-only until someone deliberately opens it. That inverts the current default, in which a forgotten attribute silently exposes data.
3. **Extend the existing test suite.** `Jaberah.Tests/CleaningLogRulesTests.cs` is the right model — add a `cannot_touch_another_groups_student` test per module so a regression is caught in CI (the workflow already runs `dotnet test`, `.github/workflows/main.yml:34-35`).
4. **Add actor columns** (`CreatedByTeacherId`, `UpdatedByTeacherId`) to `Exam`, `MidFinal`, `SaveLesson`, `ReviewLesson`, `StudentAttendance` and `StudentPrayerAttendance` so that tampering is attributable in future.

### Exploitability

**Exploitable and observable today** with any valid teacher token. No preconditions beyond authentication; the checks are absent from source, not merely weak.

### References

- OWASP Top 10 2021 — A01:2021 Broken Access Control
- OWASP API Security Top 10 2023 — API1:2023 Broken Object Level Authorization; API5:2023 Broken Function Level Authorization
- CWE-639: Authorization Bypass Through User-Controlled Key · CWE-862: Missing Authorization

---

## [PERF-001] `monthly-report` has no date-range bound and materialises every matching lesson row into memory

- **Category:** Performance
- **Severity:** Critical
- **Confidence:** High
- **Location:** `Jaberah/Controllers/ReportsController.cs:100-267` (validation at 103-104, group filter at 110, projection at 130-190, `Take` at 263)

### Description

`GET /api/reports/monthly-report` validates only that `fromDate` and `toDate` are non-default. It does not check that `fromDate <= toDate`, does not bound the span, and — because `groupId` is optional in effect (line 110) — does not necessarily restrict the query to one group. It then projects, **for every matching student**, two full nested collections (`SaveLessons`, `ReviewLessons`) with six columns each, plus four aggregate subqueries, into managed objects. Only after the entire result set is in memory is `take` applied:

```csharp
// Jaberah/Controllers/ReportsController.cs:263 — inside the in-memory Select over `students`
.Take(take ?? int.MaxValue)]
```

So `take` limits the JSON payload but not the database work or the allocation. `List<T>` growth for the two nested collections happens per student regardless.

### Evidence

```csharp
// Jaberah/Controllers/ReportsController.cs:103-104 — the only date validation
if (fromDate == default || toDate == default)
    return BadRequest(new { message = "ادخل سنة وشهر صحيح" });

// :109-115 — group filter applies only when groupId != 0
var studentsQb = _db.Students.AsNoTracking().AsQueryable();
if(!groupId.Equals(default))
{
    ...
    studentsQb = studentsQb.Where(s => s.GroupId == groupId).AsQueryable();
```

```csharp
// :130-169 — two unbounded nested collections materialised per student
var students = await studentsQb
    .Include(s => s.Group)
    .Select(s => new
    {
        s.Id, s.Name, GroupName = s.Group.Name,
        SaveLessons = s.SaveLessons!
            .Where(l => l.Date >= fromDate && l.Date <= toDate && ...)
            .OrderBy(l => l.Date)
            .Select(l => new { l.SurahFrom, l.SurahTo, l.VerseFrom, l.VerseTo, l.Rate, l.Pages })
            .ToList(),
        ReviewLessons = s.ReviewLessons!
            .Where(l => l.Date >= fromDate && l.Date <= toDate && ...)
            .OrderBy(l => l.Date)
            .Select(l => new { ... })
            .ToList(),
        SaveCount   = s.SaveLessons!.Count(l => l.Date >= fromDate && l.Date <= toDate),
        ReviewCount = s.ReviewLessons!.Count(l => l.Date >= fromDate && l.Date <= toDate),
        Attendance  = s.StudentAttendances!.Where(...).Sum(a => (double?)a.Attendance) ?? 0,
        Behavior    = s.StudentAttendances!.Where(...).Sum(a => (double?)a.Behavior) ?? 0,
        Exam = s.Exams!.Where(...).Select(e => new { e.OralExam, e.PaperExam }).FirstOrDefault()
    })
    .ToListAsync();
```

`Include(s => s.Group)` on line 131 is also redundant — the projection on line 136 already pulls `s.Group.Name`, so EF resolves it with a join regardless; the `Include` is ignored for projected queries and only adds noise.

Note also that the deployed client never exercises the wide-range case: `lib/controllers/admin/monthlyReportController.dart:34` sends `year` and `month`, which do not bind to the `fromDate`/`toDate` parameters at all (see Appendix). The endpoint's exposure to a hand-crafted request is unaffected by that.

### Impact

- **Self-service denial of service.** A single request with a century-wide range and `groupId=0` forces SQL Server to scan `SaveLessons`, `ReviewLessons`, `StudentAttendances` and `Exams` for every student, then forces the API process to allocate two `List<>`s per student holding every row. Repeated concurrently, this exhausts the connection pool (default 100), the `LOH`, and the shared SQL Server instance that also backs Hangfire (`Jaberah/Program.cs:128` — same connection string).
- **Compounded by PERF-003.** `RequestResponseLoggingMiddleware` buffers the entire response body into a `MemoryStream` before writing it (`:42-43, 64`), so the serialised report is held in memory a second time on top of the entity graph.
- **Latent growth.** Even the legitimate one-month, one-group case grows linearly with retained history; nothing in the schema or the query ages data out.
- **No timeout backstop.** No `CommandTimeout` is configured on the `DbContext` (`Jaberah/Program.cs:25`), so the query runs to the ADO.NET default of 30 s per attempt while holding a pooled connection.

### Reproduction / Trigger

```bash
# Any teacher token. groupId=0 skips the group filter entirely; the range spans all data.
curl -H "Authorization: Bearer $TEACHER_TOKEN" \
  "https://jaberah-new.tryasp.net/api/reports/monthly-report?groupId=0&fromDate=0001-01-01&toDate=9999-12-31&take=1"
```

`take=1` returns a one-element payload while the server has already loaded and allocated the entire dataset — which is what makes this cheap for the attacker and expensive for the server. Run 20 of these in parallel to observe pool starvation.

### Recommended Remediation

1. **Bound the range server-side.** Reject `fromDate > toDate`, and cap the span at what the feature actually needs — the endpoint is named `monthly-report`, so `(toDate - fromDate).TotalDays <= 62` is a generous limit:
   ```csharp
   if (fromDate > toDate || (toDate - fromDate).TotalDays > 62)
       return BadRequest(new { message = "المدة يجب ان لا تتجاوز شهرين" });
   ```
2. **Gate the all-groups path by role rather than removing it.** Note the correction in SEC-003: the omitted-`groupId` branch is a deliberate admin feature on the production client, so it cannot simply be made required. Make `groupId` an `int?`, restrict the institution-wide branch to admins, and scope a teacher's omitted-`groupId` request to their own circles. That keeps the heavy path in admin hands, where a bounded range and a query-level `take` make it affordable.
3. **Push `take` into the query.** Ranking by `Total` requires the aggregates, so either compute the aggregate-only projection first, order and `Take` in SQL, then fetch the nested lesson lists for the surviving page — or drop `take` and paginate properly with `ToPagedListAsync` (`Jaberah/Helpers/PagedList.cs:30-42`), which is already used elsewhere in the codebase.
4. **Drop the redundant `Include(s => s.Group)`** on line 131.
5. **Set an explicit command timeout** — `x.UseSqlServer(cs, o => o.CommandTimeout(20))` at `Program.cs:25` — so a pathological query fails fast instead of occupying a connection.
6. Apply the same range check to `GET /api/reports/semester-report`, which is bounded to a 4-month difference (`:29-31`) but not to a maximum, and to `GET /api/reports/monthly-partial-exam` (`:412-418`), which validates ordering but not span.

### Exploitability

**Exploitable and observable today.** The absence of a range check and the post-materialisation `Take` are both directly visible in source. The precise wall-clock cost depends on retained row counts, which were not measurable from the repository (no production database access); the asymptotic behaviour is unconditional.

### References

- OWASP API Security Top 10 2023 — API4:2023 Unrestricted Resource Consumption
- CWE-770: Allocation of Resources Without Limits or Throttling · CWE-1050: Excessive Platform Resource Consumption within a Loop

---

## High Findings

## [SEC-003] The admin-only "all circles" report path is reachable by any teacher

- **Category:** Security
- **Severity:** High
- **Confidence:** High
- **Location:** `Jaberah/Controllers/ReportsController.cs:100-128` (specifically line 110) · client caller `Jaberah-Flutter/lib/controllers/admin/monthlyReportController.dart:76-87` (`main-v2`)

> **Revised in revision 2.** On the `master` client this looked like an unintended sentinel bypass. Re-verification against the production `main-v2` client shows the all-groups path is a deliberate admin feature, so the finding is not "an accidental bypass exists" but "an intended admin-only capability has no role check". The severity is unchanged; the remediation is materially different, and the original advice to make `groupId` required would have broken the admin report screen.

### Description

`GetMonthlyReport` treats `groupId` as optional by testing it against `default`. Because `groupId` is a non-nullable `int` bound from the query string, omitting it — or sending `groupId=0` — yields `0`, which *is* `default(int)`, so the entire `if` block that both validates the group and applies the `WHERE s.GroupId == groupId` filter is skipped. The query then runs against `_db.Students` unfiltered.

That is by design. The production admin client offers an explicit «كل الحلقات» (all circles) option and implements it by omitting the parameter, with a comment saying so:

```dart
// Jaberah-Flutter/lib/controllers/admin/monthlyReportController.dart:76-87 (main-v2)
// عند اختيار «كل الحلقات» نرسل groupId كقيمة فارغة ليتم تمريرها null في السيرفر.
final groupIdParam = isAllGroupsSelected ? null : '${selectedGroupId.value}';
var url = "/$monthlyReportURL?fromDate=$fromDate&toDate=$toDate";
if (!isAllStudentsTake) { url = "$url&take=${take.value}"; }
if (groupIdParam != null) { url = "$url&groupId=$groupIdParam"; }
```

The defect is therefore the missing authorization, not the sentinel: `monthly-report` carries neither `[IsAdmin]` nor an ownership check (see SEC-002), so an institution-wide export intended for the administrator's screen is equally available to every teacher account. The user-facing client, by contrast, always sends a `groupId` (`user/monthlyStudentsReports.dart:62`) — the capability is not one the teacher UI exposes, only one the API grants.

This remains distinct from SEC-002 in magnitude: SEC-002 lets a teacher read *another group's* report by naming it; SEC-003 lets them read *every group at once* without naming anything, and with no group-existence check to leave a trace of which id was probed.

### Evidence

```csharp
// Jaberah/Controllers/ReportsController.cs:107-128
List<BooksData> books = [];

var studentsQb = _db.Students.AsNoTracking().AsQueryable();
if(!groupId.Equals(default))          // ← false when groupId is 0 or absent
{
    if (!await _db.Groups.AnyAsync(x => x.Id == groupId))
        return BadRequest(new { message = "لاتوجد حلقة" });

    studentsQb = studentsQb.Where(s => s.GroupId == groupId).AsQueryable();
    books = await _db.Books ... ;
}
// falls through with studentsQb == all students
```

Contrast the sibling endpoint two methods down, which handles the same parameter correctly:

```csharp
// Jaberah/Controllers/ReportsController.cs:342-345 — GetBestStudentsForGroupReport
if (groupId <= 0)
    return BadRequest(new { message = "ادخل id صحيح" });
if (!await _db.Groups.AnyAsync(x => x.Id == groupId))
    return BadRequest(new { message = "لاتوجد حلقة" });
```

### Impact

One authenticated request returns, for **all 122 students** (and every student added since): full name, group name, every memorisation and revision entry in the date range (surah, verse range, rating, pages), aggregate attendance and behaviour, exam marks and computed total. That is a complete export of the system's academic dataset for a population of minors, available to the lowest-privileged authenticated role. Chained with SEC-001 it is available to the internet.

The same line is the trigger for PERF-001's worst case.

### Reproduction / Trigger

```bash
# groupId omitted entirely — no group id needs to be guessed
curl -H "Authorization: Bearer $TEACHER_TOKEN" \
  "https://jaberah-new.tryasp.net/api/reports/monthly-report?fromDate=2026-01-01&toDate=2026-02-01"
```

Returns `200 OK` with `data[]` covering every student in the database. Compare with `?groupId=3&...`, which returns only group 3 — confirming the filter is being skipped rather than the group simply being empty.

### Recommended Remediation

**Do not** simply make `groupId` required — that is what the first revision of this report advised, and it would break the admin screen's «كل الحلقات» option. Keep the capability and gate it by role. Change `groupId` to `int?` so "absent" is explicit rather than a sentinel, then branch:

```csharp
public async Task<IActionResult> GetMonthlyReport(
    [FromQuery] int? groupId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] int? take)
{
    if (groupId is { } gid)
    {
        if (gid <= 0) return BadRequest(new { message = "ادخل id صحيح" });
        if (!await _db.Groups.AnyAsync(x => x.Id == gid)) return BadRequest(new { message = "لاتوجد حلقة" });
        if (!await this.CallerOwnsGroupAsync(gid)) return Forbid();       // also closes SEC-002 here
        studentsQb = studentsQb.Where(s => s.GroupId == gid);
    }
    else if (this.IsCurrentUserAdmin())
    {
        // «كل الحلقات» — admin-only, institution-wide
    }
    else
    {
        // a teacher's "all" means all of *their* circles, as CleaningLogsController:198-202 already does
        var callerId = this.CurrentUser()!.Id;
        studentsQb = studentsQb.Where(s => s.Group != null && s.Group.TeacherId == callerId);
    }
```

Scoping a teacher's omitted-`groupId` request to their own circles rather than rejecting it is both safe and closer to what the word "all" means from their side of the screen. The date-range bound and query-level `take` from PERF-001 still apply, and matter *more* once the all-groups path is legitimately reachable.

Cleaner still, if you prefer not to overload one action: keep `monthly-report` strictly single-group and add `[IsAdmin] GET /api/reports/monthly-report/all-groups` with its own pagination. Either shape is fine; what must not survive is an unauthenticated-by-role institution-wide export.

### Exploitability

**Exploitable and observable today.** The sentinel-value bypass is unambiguous in source.

### References

- OWASP API Security Top 10 2023 — API3:2023 Broken Object Property Level Authorization
- CWE-1284: Improper Validation of Specified Quantity in Input · CWE-200: Exposure of Sensitive Information to an Unauthorized Actor

---

## [SEC-004] Release APK is signed with the Android debug keystore

- **Category:** Security
- **Severity:** High
- **Confidence:** High
- **Location:** `Jaberah-Flutter/android/app/build.gradle:46-52` · distribution chain: `.github/workflows/flutter-build.yml:35-46`, `Jaberah-ASP/Jaberah/Controllers/VersionsController.cs:42-76`, `Jaberah-Flutter/lib/controllers/versionsController.dart:95-143`

### Description

The Android release build type explicitly reuses the debug signing configuration. The template TODO that warns against this is still present, unactioned:

```gradle
// Jaberah-Flutter/android/app/build.gradle:46-52
buildTypes {
    release {
        // TODO: Add your own signing config for the release build.
        // Signing with the debug keys for now, so `flutter run --release` works.
        signingConfig = signingConfigs.debug
    }
}
```

The Android debug keystore is not a secret. It is generated by the SDK at `~/.android/debug.keystore` with the fixed, documented password `android`, alias `androiddebugkey` and key password `android`, and on a CI runner it is generated fresh — meaning the CI-produced APK is signed with whatever ephemeral debug key that runner happened to create, and there is no stable, controlled release identity at all.

This matters more here than in a Play Store app because the distribution channel is side-loading. `.github/workflows/flutter-build.yml:36` builds `flutter build apk --release`, uploads it to the backend, which stores it on Dropbox and returns a public share link (`VersionsController.cs:60-72`); the client then prompts the user to install it from that link, with a non-dismissable dialog when the update is mandatory (`versionsController.dart:95-118`). Users of this app are therefore already trained to accept an APK from a link.

### Evidence

```yaml
# Jaberah-Flutter/.github/workflows/flutter-build.yml:35-46
- name: Build APK
  run: flutter build apk --release --target=lib/login.dart --no-tree-shake-icons
- name: Rename APK
  run: mv build/app/outputs/flutter-apk/app-release.apk build/app/outputs/flutter-apk/jaberah-${VERSION}.apk
- name: Update Version in Backend
  run: |
    response=$(curl -X PUT "${{ secrets.BACKEND_API }}?version=${{ env.VERSION }}" ...)
```

```dart
// Jaberah-Flutter/lib/controllers/versionsController.dart:104-110 — mandatory, cannot be dismissed
TextButton(
  onPressed: () async {
    if (await canLaunchUrl(Uri.parse(versionData.value.url))) {
      await launchUrl(Uri.parse(versionData.value.url), mode: LaunchMode.externalApplication);
    }
  },
  child: const Text('تحديث الآن'),
),
```

The package name is also still the Flutter template default — `namespace = "com.example.jaberah"` (`build.gradle:23`, `AndroidManifest.xml:1`) — which is squattable on any distribution channel.

### Impact

- **Trojanised in-place update.** Android permits an update only when the new APK's package name and signing certificate match the installed app. Because the certificate is a well-known debug key, an attacker can build a modified app — one that exfiltrates the entered password and access token, for instance — sign it with the standard debug keystore, and have it install *over* the legitimate app, inheriting its data directory, its stored `accessToken` (SEC-017) and its granted `MANAGE_EXTERNAL_STORAGE` permission.
- **No integrity guarantee on the official channel.** Since the CI runner's debug key is ephemeral, successive official releases may not even share a signing identity with each other, which breaks legitimate updates and removes any signature-based way for a user to tell an official build from a forged one.
- **Public source lowers the bar further.** The client repository is public, so an attacker does not need to reverse-engineer anything to produce a convincing modified build.

### Reproduction / Trigger

1. Clone the public `Jaberah-Flutter` repository.
2. Add arbitrary code (e.g. POST the contents of `SharedPreferences` to an attacker endpoint in `AuthController.login`).
3. `flutter build apk --release` — Gradle signs it with the local debug keystore per line 50.
4. Install over an existing legitimate installation: `adb install -r jaberah-modified.apk` succeeds, because package name and certificate both match.
5. Deliver via any channel users already trust for this app (a link, a messaging group, or a compromised Dropbox share).

### Recommended Remediation

1. **Generate a release keystore**, store it outside the repository, and expose it to CI as base64 GitHub secrets:
   ```gradle
   signingConfigs {
       release {
           storeFile file(System.getenv("KEYSTORE_PATH") ?: "release.jks")
           storePassword System.getenv("KEYSTORE_PASSWORD")
           keyAlias System.getenv("KEY_ALIAS")
           keyPassword System.getenv("KEY_PASSWORD")
       }
   }
   buildTypes { release { signingConfig signingConfigs.release } }
   ```
   Add `*.jks`, `*.keystore` and `key.properties` to `.gitignore` (none are currently committed — verified — so this is preventive).
2. **Change the package name** from `com.example.jaberah` to a namespace you control before the first signed release; it cannot be changed afterwards without users reinstalling.
3. **Publish the APK's SHA-256 alongside the version record** (add a `Sha256` column to the `Version` entity, `Jaberah/Models/JaberahModels/Versions.cs`) and verify it in the client before prompting to install.
4. Prefer a managed channel (Play Store internal/closed testing, or Firebase App Distribution — Firebase is already a dependency) over a Dropbox share link.

### Exploitability

**Exploitable and observable today.** The signing configuration is explicit in source. Step 4 depends on the attacker's debug keystore matching the one used for the installed build; since CI generates a fresh key per run, in practice the attacker's own debug key will match some builds and not others — either way there is no controlled release identity, and a first-time install of a forged APK is unconditionally accepted.

### References

- OWASP MASVS-RESILIENCE-1 · OWASP Mobile Top 10 2024 — M8: Security Misconfiguration
- CWE-321: Use of Hard-coded Cryptographic Key · CWE-494: Download of Code Without Integrity Check
- Android: [Sign your app](https://developer.android.com/studio/publish/app-signing)

---

## [SEC-005] Password-change validation filter never rejects: no password policy is enforced on `PUT /api/teachers/{teacherId}`

- **Category:** Security
- **Severity:** High
- **Confidence:** High
- **Location:** `Jaberah/Validations/Teachers/UpdateTeacherValidation.cs:106-160` (missing block after line 156) · consumed at `Jaberah/Controllers/TeachersController.cs:189-190, 232-242`

### Description

`UpdateTeacherAttribute` is the action filter that guards the teacher-update endpoint, including password changes. It builds a `validationContent` list of failures across four checks — name format, phone format, password pairing, password length — and then **never inspects it**. There is no `if (validationContent.Count > 0)` block and `context.Result` is never assigned, so the filter falls straight through to `base.OnActionExecuting(context)` and the action proceeds with the request unmodified. Every validation in the file is dead code.

This is unique to this one file. All nine other validation filters in `Jaberah/Validations/` end with the short-circuit block; only `UpdateTeacherValidation.cs` omits it:

```
Validations/CleaningLogs/AddCleaningTaskValidation.cs      count-check=1  result-set=1
Validations/CleaningLogs/UpdateCleaningTaskValidation.cs   count-check=1  result-set=1
Validations/CleaningLogs/UpsertDailyCleaningLogValidation  count-check=1  result-set=1
Validations/Groups/AddGroupValidation.cs                   count-check=1  result-set=1
Validations/Groups/UpdateGroupValidation.cs                count-check=1  result-set=1
Validations/Students/AddStudentValidation.cs               count-check=1  result-set=1
Validations/Students/UpdateStudentValidation.cs            count-check=1  result-set=1
Validations/Teachers/AddTeacherValidation.cs               count-check=1  result-set=1
Validations/Teachers/UpdateTeacherValidation.cs            count-check=0  result-set=0   ← 
Validations/TeachersSalaries/UpsertTeachersSalaries.cs     count-check=1  result-set=1
```

Separately, the password-length rule is itself wrong even if it did run. Line 140 requires *both* the new **and** the old password to be shorter than 8 characters before complaining, so a 1-character new password passes whenever the old password happened to be 8 or more characters — which is the normal case.

### Evidence

```csharp
// Jaberah/Validations/Teachers/UpdateTeacherValidation.cs:132-160
                if (dto.NewPassword is null ^ dto.OldPassword is null)
                {
                    validationContent.Add(new ValidationModel { Key = "كلمة السر",
                        Message = "يجب ارسال كلمة السر الجديدة وكلمة السر القديمة معاً" });
                }
                else if (dto.NewPassword is not null && (dto.NewPassword.Length < 8 && dto.OldPassword!.Length < 8))
                {                                       //                          ^^ should be || and should not test OldPassword
                    validationContent.Add(new ValidationModel { Key = "كلمة السر",
                        Message = "كلمة السر يجب ان تكون اكبر من 8 احرف" });
                }

                if (dto.GroupsId is not null && dto.GroupsId.Any(x => x <= 0))
                {
                    validationContent.Add(new ValidationModel { Key = "الحلقات",
                        Message = "رقم الحلقة يجب ان يكون اكبر من صفر" });
                }

            }
            base.OnActionExecuting(context);      // ← no `if (validationContent.Count > 0)` block; list discarded
        }
```

Compare the correct shape, from the sibling file for the same entity:

```csharp
// Jaberah/Validations/Teachers/AddTeacherValidation.cs:85-93
            if (validationContent.Count > 0)
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Result = new JsonResult(new { validationContent, });
            }
            base.OnActionExecuting(context);
```

The controller then hashes whatever it was given, with no independent check:

```csharp
// Jaberah/Controllers/TeachersController.cs:232-242
if (!string.IsNullOrEmpty(model.OldPassword))
{
    var isPasswordCorrect = BCrypt.Net.BCrypt.Verify(model.OldPassword, teacher.Password);
    if (!isPasswordCorrect)
        return BadRequest(new { message = "كلمة المرور خاطئة" });

    teacher.Password = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);   // NewPassword may be "a" — or null
}
```

### Impact

- **No minimum password length.** A teacher (or an admin acting on a teacher) can set a one-character password. Combined with SEC-006 (no login rate limiting), such an account is brute-forceable in seconds. The system's only stated password rule is therefore not in force anywhere.
- **Unhandled 500 on a plausible request.** The `^` pairing check was the only thing preventing `OldPassword` from arriving without `NewPassword`. With the filter inert, that request reaches line 241 and calls `BCrypt.HashPassword(null)`, which throws `ArgumentNullException` → caught by `GlobalExceptionMiddleware` → `500`. The password is not changed (the exception precedes `SaveChangesAsync`), so this is an availability/robustness defect rather than a corruption one.
- **Name and phone format rules bypassed on update.** `ContainsArabicAndSpaces()` and `IsPhoneNumberStartingWith7()` are enforced on create (`AddTeacherValidation.cs:27, 44`) but not on update, so a teacher can set their display name or phone number to arbitrary text. Since the display name **is** the login username (`AuthController.cs:24`), this also lets a teacher rename themselves to any string not already taken.

### Reproduction / Trigger

```bash
# Teacher changes their own password to a single character. Returns 200.
curl -X PUT -H "Authorization: Bearer $TEACHER_TOKEN" -H "Content-Type: application/json" \
  -d '{"oldPassword":"<current>","newPassword":"a"}' \
  "https://jaberah-new.tryasp.net/api/teachers/$MY_ID"

# Then log in with it — succeeds.
curl -X POST -H "Content-Type: application/json" \
  -d '{"username":"<my name>","password":"a","fcmToken":"x"}' \
  "https://jaberah-new.tryasp.net/api/auth/login"

# Omitting newPassword yields 500 rather than 400.
curl -X PUT -H "Authorization: Bearer $TEACHER_TOKEN" -H "Content-Type: application/json" \
  -d '{"oldPassword":"<current>"}' "https://jaberah-new.tryasp.net/api/teachers/$MY_ID"
```

### Recommended Remediation

1. **Add the missing short-circuit** at the end of `OnActionExecuting`, copied verbatim from `AddTeacherValidation.cs:85-92`. This one change restores all four checks.
2. **Fix the length predicate** to test only the new password, with `||` semantics implied by testing a single value:
   ```csharp
   else if (dto.NewPassword is not null && dto.NewPassword.Length < 8)
   ```
3. **Do not rely on a filter alone for a security invariant.** Re-check in the controller immediately before hashing:
   ```csharp
   if (string.IsNullOrWhiteSpace(model.NewPassword) || model.NewPassword.Length < 8)
       return BadRequest(new { message = "كلمة السر يجب ان تكون 8 احرف على الاقل" });
   ```
4. **Add a regression test.** `Jaberah.Tests/AuthorizationFilterTests.cs` already tests filters in isolation by constructing an `ActionExecutingContext`; a test asserting that `UpdateTeacherAttribute` sets `context.Result` for a 1-character password would have caught this and will prevent its return.
5. Consider raising the minimum beyond 8 characters and rejecting the account's own phone number as a password, given SEC-001.

### Exploitability

**Exploitable and observable today.** The missing block is verifiable by reading the file; the 500 path and the 1-character-password path both follow directly.

### References

- OWASP Top 10 2021 — A07:2021 Identification and Authentication Failures; A04:2021 Insecure Design
- OWASP ASVS 5.0 — V2.1 Password Security Requirements
- CWE-521: Weak Password Requirements · CWE-1173: Improper Use of Validation Framework

---

## [SEC-006] No rate limiting, lockout or throttling on authentication

- **Category:** Security
- **Severity:** High
- **Confidence:** High
- **Location:** `Jaberah/Program.cs` (no rate-limiting registration anywhere in the file) · `Jaberah/Controllers/AuthController.cs:19-64`

### Description

Neither `builder.Services.AddRateLimiter(...)` nor `app.UseRateLimiter()` appears anywhere in the solution — a grep for `AddRateLimiter|UseRateLimiter|EnableRateLimiting` across all 109 `.cs` files returns nothing. ASP.NET Core 9 ships rate limiting in the box; it is simply not wired up.

`POST /api/auth/login` is `[AllowAnonymous]` and has no failed-attempt counter, no lockout, no CAPTCHA, no per-IP or per-account throttle and no delay on failure. The `Teacher` entity records `LastLogin` (`Jaberah/Models/JaberahModels/Teachers.cs:9`) but there is no `FailedLoginCount` or `LockedUntil` field to record or act on failures.

Failure is also cheaply distinguishable: a non-existent username returns at line 28 without running BCrypt, whereas a real username runs `BCrypt.Verify` at cost 11 before returning the same message at line 36. The identical error text is good practice, but the ~100 ms timing difference discloses username validity regardless.

### Evidence

```csharp
// Jaberah/Controllers/AuthController.cs:19-37 — no throttle, no attempt counter
[AllowAnonymous]
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginDTO model)
{
    var teacher = await _db.Teachers.Include(x => x.Groups)
        .FirstOrDefaultAsync(t => t.Name == model.Username.Trim());

    if (teacher == null)
    {
        return BadRequest(new { message = "اسم المستخدم او كلمة المرور خاطئة" });   // fast path
    }

    var isPasswordValid = BCrypt.Net.BCrypt.Verify(model.Password, teacher.Password);  // slow path

    if (!isPasswordValid)
    {
        return BadRequest(new { message = "اسم المستخدم او كلمة المرور خاطئة" });
    }
```

```
$ grep -rn "AddRateLimiter\|UseRateLimiter\|EnableRateLimiting" --include="*.cs" .
(no output)
```

### Impact

- **Unbounded credential guessing against a known, tiny user population.** There are 10 accounts and their usernames are published (SEC-001). Passwords default to a 9-digit number matching `^7\d{8}$`; a local Yemeni mobile-number prefix reduces that to a few million candidates, and a targeted guess (the teacher's actual number, which is also published) is a single request.
- **Amplifies SEC-005.** A 1-character password is unreachable in practice only if guessing is throttled. It is not.
- **Log-file growth as a side effect.** Every failed login returns 400, and `RequestResponseLoggingMiddleware:51-62` writes every non-2xx response to `Logs/error-requests.log`. A sustained guessing run therefore also inflates the log file and slows `GET /api/Logs` (PERF-006).
- **Every other endpoint is equally unthrottled**, including the resource-heavy report endpoints (PERF-001) and the 100 MB APK upload (`VersionsController.cs:45`).

### Reproduction / Trigger

```bash
for i in $(seq 1 1000); do
  curl -s -o /dev/null -w "%{http_code} " -X POST -H "Content-Type: application/json" \
    -d "{\"username\":\"مدرسة ابن الوليد\",\"password\":\"7$(printf %08d $i)\",\"fcmToken\":\"x\"}" \
    "https://jaberah-new.tryasp.net/api/auth/login"
done
```

Every request is served; no `429`, no lockout, no increasing delay. (Not executed during this audit — running it against production would itself be an attack. The absence of any throttling mechanism in source is what is being reported.)

### Recommended Remediation

1. **Add a fixed- or sliding-window limiter and apply it to the auth endpoints**:
   ```csharp
   builder.Services.AddRateLimiter(o =>
   {
       o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
       o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
           ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
           _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(5) }));
       o.GlobalLimiter = /* a looser global partition, e.g. 300/min per IP */;
   });
   ...
   app.UseRateLimiter();   // place after UseRouting, before UseAuthorization
   ```
   then `[EnableRateLimiting("login")]` on `AuthController.Login` and `Refresh`.
2. **Add per-account lockout** independent of IP (an attacker can rotate source addresses): `FailedLoginCount` and `LockedUntil` columns on `Teacher`, incremented on failure, cleared on success, refusing authentication for a growing interval after ~5 failures.
3. **Equalise the failure paths** by running a dummy BCrypt verify against a fixed hash when the username is unknown, so response time no longer discloses account existence.
4. **Apply a stricter limiter to the report endpoints** as defence in depth for PERF-001.
5. If the API sits behind a reverse proxy or CDN, configure `ForwardedHeaders` so the limiter partitions on the real client IP rather than the proxy's.

### Exploitability

**Exploitable today.** Confirmed by the complete absence of any rate-limiting or lockout mechanism in the source.

### References

- OWASP Top 10 2021 — A07:2021 Identification and Authentication Failures
- OWASP API Security Top 10 2023 — API4:2023 Unrestricted Resource Consumption
- CWE-307: Improper Restriction of Excessive Authentication Attempts · CWE-208: Observable Timing Discrepancy

---

## [SEC-007] Teachers can record attendance against groups they do not teach

- **Category:** Security
- **Severity:** High
- **Confidence:** High
- **Location:** `Jaberah/Controllers/TeachersAttendancesController.cs:282-350` (check-in) and `:353-394` (check-out)

### Description

`POST /api/teachers-attendances/check-in` takes the caller's identity from the JWT — correctly — but takes `GroupId` from the request body and validates only that such a group exists. It never checks that the caller teaches it. The same is true of check-out.

The controller demonstrates elsewhere that it knows how to do this: `GetTeacherAttendanceForDay:174-175` and `GetTeacherAttendanceForMonth:226-227` both call `this.CanActOnTeacher(teacherId)` and return `Forbid()`. The self-service check-in path skips the equivalent group check.

### Evidence

```csharp
// Jaberah/Controllers/TeachersAttendancesController.cs:282-303
[HttpPost("check-in")]
public async Task<IActionResult> TeacherCheckIn([FromBody] TeacherCheckInDTO model)
{
    var teacherId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    if (teacherId <= 0 || model.GroupId <= 0)
        return BadRequest(new { message = "ادخل id صحيح" });
    ...
    var group = await _db.Groups.FindAsync(model.GroupId);
    if (group == null)
        return NotFound(new { message = "الحلقة غير موجودة" });   // existence only — no ownership check

    var existing = await _db.TeacherAttendances
        .FirstOrDefaultAsync(a => a.Date == today
                               && a.TeacherId == teacherId
                               && a.GroupId == model.GroupId);
```

The record it writes is what the admin payroll and attendance views read:

```csharp
// Jaberah/Controllers/TeachersAttendancesController.cs:330-337
await _db.TeacherAttendances.AddAsync(new TeacherAttendance
{
    TeacherId = teacherId,
    GroupId = model.GroupId,
    Date = today,
    CheckInTime = now,
    Status = status
});
```

### Impact

- **Fabricated attendance records.** A teacher can create `Present` records for themselves against groups they have no relationship with, one per group per day. These feed `GET /api/teachers-attendances/for-month-report:29-42`, which aggregates `PresentNo`/`AbsentNo`/`LateNo` per teacher-group pair for the admin's monthly view.
- **Payroll relevance.** `TeacherSalary` is keyed on `(TeacherId, GroupId, Year, Month)` (`Jaberah/Models/MyDbContext/JaberahDBContext.cs:297-298`) and the salary screen enumerates teacher-group pairs. Salary amounts are still entered by an admin (`TeachersSalariesController.cs:59-106`), so this is not a direct route to being paid more — but it corrupts the attendance evidence an admin uses to decide those amounts.
- **Noise and false alerting.** Each check-in broadcasts a push notification naming the teacher and the group to the `check-attendance` topic (`:342-347`), so this is also a way to spam every subscribed admin device with plausible-looking but false activity.
- **`Late`/`Present` status is self-determined** from the group's `WindowEnd` and `FlexibleMinutes`, so a teacher checking in to a group with a permissive window is recorded `Present` regardless of the window on their real group.

### Reproduction / Trigger

```bash
# Any teacher token; groupId 3 belongs to a different teacher. Returns 200.
curl -X POST -H "Authorization: Bearer $TEACHER_TOKEN" -H "Content-Type: application/json" \
  -d '{"groupId":3}' "https://jaberah-new.tryasp.net/api/teachers-attendances/check-in"

# Confirm it landed, as the admin (or via the caller's own for-month view)
curl -H "Authorization: Bearer $ADMIN_TOKEN" \
  "https://jaberah-new.tryasp.net/api/teachers-attendances/for-month-report?fromDate=2026-09-01&toDate=2026-09-30"
```

Note the caller's own `for-day`/`for-month` views enumerate `teacher.Groups` and so will not display the foreign-group record — it is visible only in the admin report, which makes the tampering less likely to be noticed by the perpetrator's own screen.

### Recommended Remediation

1. **Scope the group to the caller** in both actions:
   ```csharp
   var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == model.GroupId && g.TeacherId == teacherId);
   if (group == null) return Forbid();
   ```
   Admins, if they need to check in on someone's behalf, already have `POST /api/teachers-attendances` (`:94-169`), which is `[IsAdmin]`.
2. **Take the caller id from one source.** These two actions read `User.FindFirstValue(ClaimTypes.NameIdentifier)` while the rest of the codebase uses `HttpContext.Items["User"]` via `this.CurrentUser()`. Two mechanisms for the same fact is a latent divergence; `int.Parse` on a missing claim would also throw `ArgumentNullException` → 500. Use `this.CurrentUser()!.Id` consistently (the same applies to `TeachersSalariesController.cs:111, 134`).
3. **Handle the check-in race.** Two concurrent check-ins both observe `existing == null` and both insert, violating the unique index on `(TeacherId, GroupId, Date)` (`JaberahDBContext.cs:252-254`) and surfacing as a 500. Catch `DbUpdateException` and translate to a 409, following the pattern already implemented at `CleaningLogsController.cs:334-342`.

### Exploitability

**Exploitable and observable today.** The missing ownership check is directly visible; the resulting row is observable in the admin monthly report.

### References

- OWASP Top 10 2021 — A01:2021 Broken Access Control
- OWASP API Security Top 10 2023 — API1:2023 Broken Object Level Authorization
- CWE-639: Authorization Bypass Through User-Controlled Key

---

## [PERF-002] `HttpClient` is registered as a scoped service and rebuilt on every request to the app's launch endpoint

- **Category:** Performance
- **Severity:** High
- **Confidence:** High
- **Location:** `Jaberah/Program.cs:32` · `Jaberah/Helpers/DropboxService.cs:5-10` · `Jaberah/Controllers/VersionsController.cs:11-14` · client trigger at `Jaberah-Flutter/lib/controllers/versionsController.dart:24-27, 48-49`

### Description

`HttpClient` is registered in the DI container as **scoped**:

```csharp
// Jaberah/Program.cs:30-32
builder.Services.AddScoped<DropboxService>();
builder.Services.AddScoped<FirebaseService>();
builder.Services.AddScoped<HttpClient>();
```

A scoped `HttpClient` is constructed once per request scope and, because `HttpClient` implements `IDisposable`, is **disposed** by the container at the end of the request — tearing down its `SocketsHttpHandler` and the connection pool with it. This is the canonical .NET socket-exhaustion antipattern; `IHttpClientFactory` exists specifically to avoid it and is not used.

The consequence is not confined to the rarely used upload path. `VersionsController`'s constructor takes `DropboxService`, which takes `HttpClient`, so the whole chain is resolved for **every action on the controller** — including `GET /api/versions`, which is `[AllowAnonymous]` and is called by every client on every app launch from `VersionsController.onInit()`. Every version check therefore allocates and destroys a fresh HTTP client stack that it never uses.

### Evidence

```csharp
// Jaberah/Helpers/DropboxService.cs:5-10 — HttpClient injected into a scoped service
public class DropboxService(HttpClient httpClient, IConfiguration configuration)
{
    private readonly HttpClient _httpClient = httpClient;
```

```csharp
// Jaberah/Controllers/VersionsController.cs:11-17 — resolved for the anonymous GET too
public class VersionsController(JaberahDBContext db, DropboxService dropboxService) : ControllerBase
{
    private readonly JaberahDBContext _db = db;
    private readonly DropboxService _dropboxService = dropboxService;
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetLastVersion([FromQuery] string version)
```

```dart
// Jaberah-Flutter/lib/controllers/versionsController.dart:23-27, 48-49 — fired on every app start
@override
void onInit() {
  checkVersion();
  super.onInit();
}
...
var response = await _apiClient.dio.get("/versions?version=${packageInfo.version}");
```

### Impact

- **Socket and port exhaustion under load.** Each disposed handler leaves its TCP connections in `TIME_WAIT` for the OS timeout (240 s on Windows by default, and the host is `windows-latest`/MonsterASP IIS per `.github/workflows/main.yml:17` and `site14114-WebDeploy.pubxml:15`). Sustained version checks — every client, every launch, plus retries — accumulate `TIME_WAIT` entries against the ephemeral port range and eventually produce `SocketException: Only one usage of each socket address is normally permitted`, which manifests as the *Dropbox upload* failing rather than as version checks failing, making it hard to diagnose.
- **Per-request allocation waste on the hottest endpoint.** A `SocketsHttpHandler` plus its connection pool is created and immediately destroyed for a request that makes no outbound call at all.
- **No DNS refresh and no resilience.** Because there is no factory, there is also no handler lifetime rotation, no Polly/retry pipeline, and no per-client timeout configured (`DropboxService` relies on the 100 s default) — so a hung Dropbox call holds the request thread and its DB scope for 100 s.

### Reproduction / Trigger

Load-test the anonymous endpoint and watch the socket table:

```bash
# 2000 requests, 50 concurrent — no auth needed
ab -n 2000 -c 50 "https://jaberah-new.tryasp.net/api/versions?version=2.0.1"
# On the host: netstat -an | find /c "TIME_WAIT"   (Windows)
```

`TIME_WAIT` count grows roughly with request count rather than plateauing, because each request's handler is torn down individually rather than pooled.

### Recommended Remediation

1. **Replace the scoped registration with `IHttpClientFactory`** — a named or typed client for Dropbox:
   ```csharp
   builder.Services.AddHttpClient<DropboxService>(c =>
   {
       c.BaseAddress = new Uri("https://api.dropboxapi.com/");
       c.Timeout = TimeSpan.FromMinutes(5);   // APK uploads are large; the version check makes no call
   });
   // remove: builder.Services.AddScoped<HttpClient>();
   // remove: builder.Services.AddScoped<DropboxService>();
   ```
   `AddHttpClient<T>` registers `T` as transient with a pooled, rotated handler, which fixes the leak without touching `DropboxService`'s constructor.
2. **Break the dependency on the hot path.** Split the anonymous `GET /api/versions` into its own controller that takes only `JaberahDBContext`, leaving `PUT /api/versions` with the Dropbox dependency. Alternatively inject `Lazy<DropboxService>` or `IServiceProvider`. This is worth doing on its own merits: the read path should not construct upload machinery.
3. **Cache the version row.** `GET /api/versions` runs `OrderByDescending(v => v.UpdatedAt).FirstOrDefaultAsync()` on every app launch over a table with a handful of rows and no index on `UpdatedAt`. `IMemoryCache` is already registered (`Program.cs:33`); a 5-minute entry invalidated by `PUT` removes the database round trip from the hottest endpoint entirely.

### Exploitability

**Observable today** — the registration and the dependency chain are explicit in source, and the endpoint is unauthenticated, so the load can be generated by anyone. Whether port exhaustion is currently being reached depends on live traffic volume, which was not measurable from the repository.

### References
 
- [.NET: Use `IHttpClientFactory` to implement resilient HTTP requests](https://learn.microsoft.com/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests)
- CWE-404: Improper Resource Shutdown or Release · CWE-772: Missing Release of Resource after Effective Lifetime

---

## Medium Findings

## [SEC-008] Book endpoints have no authorization: any teacher can create, edit or permanently delete any group's books

- **Category:** Security
- **Severity:** Medium
- **Confidence:** High
- **Location:** `Jaberah/Controllers/GroupsController.cs:332-354` (create), `:356-370` (update), `:372-383` (delete)

### Description

`GroupsController` applies `[IsAdmin]` to all fifteen of its group and student actions. The three book actions appended at the end of the file are the only ones without it, and they have no ownership check either — so they inherit only the controller-level `[ServiceFilter(typeof(VerifyTokenAttribute))]`. The update and delete actions do not even take a `groupId`: they address a book directly by its own id, so there is nothing scoping them to a group at all.

Unlike most entities in this schema, `Book` deletion is a **hard** delete — `_db.Books.Remove(book)` rather than `_db.SoftDelete(book)` — so the row is unrecoverable.

### Evidence

```csharp
// Jaberah/Controllers/GroupsController.cs:332-338 — create: no [IsAdmin], groupId unchecked against caller
[HttpPost("{groupId}/books")]
public async Task<IActionResult> CreateBook([FromRoute] int groupId, [FromBody] UpsertBookDTO dto)
{
    if (groupId <= 0) return BadRequest(new { message = "ادخل id صحيح" });

    var group = await _db.Groups.FindAsync(groupId);
    if (group == null) return NotFound(new { message = "لاتوجد حلقة" });
```

```csharp
// :372-383 — delete: hard delete, addressed by bookId alone, no [IsAdmin]
[HttpDelete("books/{bookId}")]
public async Task<IActionResult> DeleteBook([FromRoute] int bookId)
{
    var book = await _db.Books.FindAsync(bookId);
    if (book == null)
        return NotFound(new { message = "الكتاب غير موجود" });

    _db.Books.Remove(book);          // hard delete — contrast _db.SoftDelete used elsewhere
    await _db.SaveChangesAsync();
```

Compare the immediately preceding action in the same file:

```csharp
// :315-317
[IsAdmin]
[HttpPatch("{groupId}/restore")]
public async Task<IActionResult> RestoreGroup([FromRoute] int groupId)
```

### Impact

Books appear in the monthly report for a group (`ReportsController.cs:116-127` populates `report.Books`), so this is destructive interference with another teacher's reporting output rather than an isolated nuisance. Any teacher can enumerate `bookId` values (sequential integers) and delete every book in the institution with a loop, irrecoverably. They can equally inject arbitrary text into another group's report: `dto.Title`, `dto.From` and `dto.To` are copied through with only a `?? ""` fallback and no length or content validation, against columns limited to 250/100/100 characters (`JaberahDBContext.cs:92-94`) — over-long input produces a `DbUpdateException` → 500 rather than a 400.

### Reproduction / Trigger

```bash
# Any teacher token. Create a book on a group the caller does not teach:
curl -X POST -H "Authorization: Bearer $TEACHER_TOKEN" -H "Content-Type: application/json" \
  -d '{"title":"injected","from":"1","to":"2","date":"2026-09-01"}' \
  "https://jaberah-new.tryasp.net/api/groups/3/books"

# Permanently delete any book by id:
curl -X DELETE -H "Authorization: Bearer $TEACHER_TOKEN" \
  "https://jaberah-new.tryasp.net/api/groups/books/1"

# Trigger a 500 with over-long input:
curl -X PUT -H "Authorization: Bearer $TEACHER_TOKEN" -H "Content-Type: application/json" \
  -d "{\"title\":\"$(python3 -c 'print("A"*300)')\"}" \
  "https://jaberah-new.tryasp.net/api/groups/books/2"
```

### Recommended Remediation

1. Decide the intended actor. If books are an admin concern, add `[IsAdmin]` to all three actions. If teachers maintain their own group's books — which the `POST /api/groups/{groupId}/books` route shape suggests — add an ownership check, and for update/delete resolve ownership through the book's group:
   ```csharp
   var book = await _db.Books.Include(b => b.Group)
       .FirstOrDefaultAsync(b => b.Id == bookId);
   if (book is null) return NotFound(...);
   if (!this.IsCurrentUserAdmin() && book.Group.TeacherId != this.CurrentUser()!.Id) return Forbid();
   ```
2. Use `_db.SoftDelete(book)` for consistency with every other entity, so a mistaken delete is recoverable.
3. Add a validation filter for `UpsertBookDTO` following the existing `Validations/` pattern — required `Title`, and length caps matching the column definitions.

### Exploitability

**Exploitable and observable today.** The missing attribute is verifiable by inspection; there are no book-related tests.

### References

- OWASP Top 10 2021 — A01:2021 Broken Access Control · CWE-862: Missing Authorization

---

## [SEC-009] Access and refresh tokens are indistinguishable and non-revocable, so one stolen token grants indefinite access

- **Category:** Security
- **Severity:** Medium
- **Confidence:** High
- **Location:** `Jaberah/Middlewares/VerifyToken.cs:58-73` (issuance) · `Jaberah/Controllers/AuthController.cs:39-40, 66-93` (usage)

### Description

`TokenHelper.GenerateToken` produces one token shape with exactly two claims — `nameid` and `unique_name` — differing only in expiry. `Login` calls it twice: once with 7 days as the "access token" and once with 30 days as the "refresh token". There is no `typ` claim, no `jti`, no audience and no issuer (issuer and audience validation are both switched off at `Program.cs:68-69` and `VerifyToken.cs:83-84`).

Because the two are structurally identical, **any valid token is accepted anywhere a token is accepted**. In particular a 7-day access token, presented as the `refreshToken` cookie to `POST /api/auth/refresh`, verifies successfully and mints a fresh 7-day access token plus a fresh 30-day refresh token. Repeating that before each expiry extends a single initially-stolen token into unlimited access.

There is also no revocation path. No token store, no `jti` deny-list, no `TokenVersion`/`SecurityStamp` column on `Teacher`, and no logout endpoint on the server — `AuthController` exposes only `login`, `refresh` and `fcm-token`, and the client's `logout()` (`Jaberah-Flutter/lib/controllers/authController.dart:101-140`) only clears local storage. A password change does not invalidate outstanding tokens either. The only revocation that exists is soft-deleting the teacher, which works because `VerifyToken:96-100` re-reads the row through the global soft-delete query filter and returns `null`.

### Evidence

```csharp
// Jaberah/Middlewares/VerifyToken.cs:58-73 — one token shape, differing only in `days`
public string GenerateToken(string id, string name, int days)
{
    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.UTF8.GetBytes(_config["TokenKey"]!);
    var refreshTokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, id),
            new Claim(ClaimTypes.Name, name)
        ]),                                            // no typ, no jti, no aud/iss
        Expires = DateTime.UtcNow.AddHours(3).AddDays(days),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };
    return tokenHandler.WriteToken(tokenHandler.CreateToken(refreshTokenDescriptor));
}
```

```csharp
// Jaberah/Controllers/AuthController.cs:66-81 — refresh accepts any token that VerifyToken accepts
[AllowAnonymous]
[HttpPost("refresh")]
public async Task<IActionResult> Refresh()
{
    var refreshToken = Request.Cookies["refreshToken"];
    if (string.IsNullOrWhiteSpace(refreshToken)) return Unauthorized();
    var user = await _token.VerifyToken(refreshToken);      // same verification as an access token
    if (user == default) return Forbid();
    var newAccessToken  = _token.GenerateToken(user.Id.ToString(), user.Name, 7);
    var newRefreshToken = _token.GenerateToken(user.Id.ToString(), user.Name, 30);
```

Note also `Expires = DateTime.UtcNow.AddHours(3).AddDays(days)` — the `+3h` shifts the *expiry* three hours into the future in UTC terms, so every token lives 3 hours longer than the nominal 7 or 30 days. The `exp` claim is always UTC; adding a local-time offset to it is a bug, not a timezone conversion. The same `AddHours(3)` pattern appears throughout the codebase for display timestamps, where it is intentional.

### Impact

- **Indefinite session from a single leak.** A token captured from a device backup, a rooted phone (SEC-017), a shared log, or a support screenshot can be renewed forever with no way for an administrator to stop it short of soft-deleting and recreating the account.
- **No response capability.** If SEC-001 has already been exploited, there is no way to invalidate whatever sessions the attacker established; rotating `TokenKey` is the only lever and it logs out every legitimate user simultaneously.
- **Password change is not a remedy.** A user who suspects compromise and changes their password remains compromised, because outstanding tokens are unaffected.
- Cookie handling itself is sound: `HttpOnly`, `Secure`, `SameSite=Strict` and a `Path` that matches the real route (`AuthController.cs:53-62`), so CSRF against `/refresh` is not viable.

### Reproduction / Trigger

```bash
# 1. Log in and keep only the accessToken.
ACCESS=$(curl -s -X POST -H "Content-Type: application/json" \
  -d '{"username":"...","password":"...","fcmToken":"x"}' \
  https://jaberah-new.tryasp.net/api/auth/login | jq -r .accessToken)

# 2. Present the ACCESS token as the refreshToken cookie — it is accepted.
curl -s -X POST -b "refreshToken=$ACCESS" https://jaberah-new.tryasp.net/api/auth/refresh
# → 200 {"accessToken":"<new 7-day token>"}  + Set-Cookie: refreshToken=<new 30-day token>
```

### Recommended Remediation

1. **Distinguish the two token types.** Add a claim at issuance (`new Claim("typ", "access" | "refresh")`) and have `Refresh` reject anything that is not `typ=refresh`. Pass the type into `GenerateToken` alongside `days`.
2. **Make refresh tokens revocable and single-use.** Store a hash of each issued refresh token with its `TeacherId`, expiry and a `RevokedAt`; on refresh, verify it is present and unrevoked, then revoke it and issue a replacement (rotation). Reuse of a revoked token should revoke the whole family — that is what turns theft into a detectable event.
3. **Add a `SecurityStamp` column to `Teacher`,** include it as a claim, and compare it in `VerifyToken`. Rotating the stamp on password change and on admin request gives you both "log me out everywhere" and an incident-response lever, at the cost of one column and one comparison — and `VerifyToken` already reads the row on every request, so it is free.
4. **Shorten the access-token lifetime** to hours rather than 7 days once refresh actually works; the client already handles 401-driven refresh transparently (`Jaberah-Flutter/lib/api/Dio.dart:41-69`).
5. **Fix the expiry arithmetic** — use `DateTime.UtcNow.AddDays(days)` and drop the `AddHours(3)`.
6. **Set `ValidateIssuer`/`ValidateAudience` to true** with configured values, so a token minted for another system with the same key cannot be replayed here.

### Exploitability

**Exploitable and observable today.** Step 2 of the reproduction follows directly from `Refresh` using the same `VerifyToken` as every other endpoint, with no type discriminator to check.

### References

- OWASP Top 10 2021 — A07:2021 Identification and Authentication Failures
- OWASP ASVS 5.0 — V3.5 Token-based Session Management
- CWE-613: Insufficient Session Expiration · CWE-384: Session Fixation (rotation absence)

---

## [SEC-010] The JWT signing key is read as UTF-8 when signing and as ASCII when verifying

- **Category:** Security
- **Severity:** Medium
- **Confidence:** High
- **Location:** `Jaberah/Middlewares/VerifyToken.cs:61` (sign, UTF-8) vs `:77` (verify, ASCII) · `Jaberah/Program.cs:64` (JwtBearer, UTF-8)

### Description

The same configuration value, `TokenKey`, is converted to bytes with two different encodings in the same class:

| Location | Encoding | Purpose |
|---|---|---|
| `VerifyToken.cs:61` | `Encoding.UTF8` | signing, in `GenerateToken` |
| `Program.cs:64` | `Encoding.UTF8` | `JwtBearer` middleware validation |
| `VerifyToken.cs:77` | **`Encoding.ASCII`** | validation, in `VerifyToken` — the path every request goes through |

For a key composed only of ASCII characters the two produce identical bytes and nothing is wrong. For a key containing any character outside U+0000–U+007F they diverge, and they diverge **silently**: `Encoding.ASCII.GetBytes` does not throw on out-of-range input, it substitutes `?` (0x3F) for each such character.

This is not a hypothetical key shape. The project's own README (present in the commit history of this repository) documented the configuration with an Arabic-language placeholder for this exact setting — `"TokenKey": "مفتاح توقيع طويل عشوائي"` — before it was translated to English.

### Evidence

```csharp
// Jaberah/Middlewares/VerifyToken.cs:58-61 — signing
public string GenerateToken(string id, string name, int days)
{
    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.UTF8.GetBytes(_config["TokenKey"]!);
```

```csharp
// Jaberah/Middlewares/VerifyToken.cs:74-82 — verification, on every authenticated request
public async Task<UserViewModel?> VerifyToken(string token)
{
    var tokenHandler = new JwtSecurityTokenHandler();
    var keyBytes = Encoding.ASCII.GetBytes(_config["TokenKey"]!);      // ← ASCII, not UTF8
    try
    {
        var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
```

```csharp
// Jaberah/Program.cs:62-72 — the bearer middleware agrees with the signer, not with VerifyToken
.AddJwtBearer(options =>
{
    var key = Encoding.UTF8.GetBytes(builder.Configuration["TokenKey"]!);
```

### Impact

Two distinct failure modes, depending on the deployed key:

- **If `TokenKey` contains any non-ASCII character: total, silent authentication outage.** Tokens are signed with the UTF-8 bytes and verified against the ASCII-mangled bytes, so every signature check in `VerifyToken` fails, the `catch` at `:102-105` swallows the exception and returns `null`, and `VerifyTokenAttribute:40-43` converts that to `ForbidResult`. Every protected endpoint returns 403 for every user, immediately after a config change, with no error in the logs explaining why — the exception is discarded. The `[AllowAnonymous]` endpoints keep working, which makes the diagnosis harder still.
- **If `TokenKey` is ASCII: correct today, but silently entropy-limited.** Because the `catch` hides the mismatch, the codebase gives no signal that non-ASCII keys are unusable. Any future key rotation that uses a passphrase, an emoji, or a base64 alphabet extension collapses non-ASCII characters into repeated `?`, reducing the key's effective entropy in the verification path.

The `catch (Exception) { return null; }` at `:102-105` is a contributing factor in its own right: it merges "signature invalid", "token expired", "key misconfigured" and "database unavailable" into a single indistinguishable `null`, so a configuration fault presents as an authorization failure.

### Reproduction / Trigger

1. Set `TokenKey` in `appsettings.Production.json` to a value containing a non-ASCII character of at least 32 bytes, e.g. `"مفتاح-توقيع-طويل-وعشوائي-جدا-لهذا-النظام"`.
2. Restart, then `POST /api/auth/login` — succeeds and returns a token (signing uses UTF-8).
3. Call any protected endpoint with that token, e.g. `GET /api/notifications` — returns `403 Forbidden`.
4. Change `:77` to `Encoding.UTF8` and repeat step 3 — returns `200`.

### Recommended Remediation

1. **Use one encoding.** Change `:77` to `Encoding.UTF8.GetBytes(...)` to match the signer and the bearer middleware.
2. **Resolve the key once at startup** rather than per call. Bind it to an options type, validate at boot that it is present and at least 32 bytes, and inject the `SymmetricSecurityKey` — a misconfiguration should refuse to start, not degrade to 403 at runtime. `Program.cs:75-79` already does exactly this for the Firebase service-account path; apply the same discipline here.
3. **Stop swallowing the exception.** Log the `SecurityTokenException` at debug and distinguish `SecurityTokenExpiredException` (a 401 the client should refresh) from `SecurityTokenSignatureKeyNotFoundException`/`SecurityTokenInvalidSignatureException` (a 403 or a startup fault) so the next configuration mistake is diagnosable.
4. **Consider deleting `VerifyToken`'s duplicate validation entirely.** `Program.cs:85-91` already installs a fallback policy requiring an authenticated user, so `JwtBearer` has validated the signature and expiry before the filter runs. The filter's remaining value is loading the `UserViewModel` — which does not require re-validating the token, only reading `User.FindFirstValue(ClaimTypes.NameIdentifier)`. Removing the second validation eliminates this class of divergence and halves the per-request crypto work (see PERF-008).

### Exploitability

**Observable today** by configuration change; the code divergence is unconditional. Whether the outage mode is currently active depends on the deployed `TokenKey`, which is not in the repository — but since the application is reported working, the deployed key must be ASCII-only, placing this in the "latent, triggered by the next rotation" category.

### References

- CWE-326: Inadequate Encryption Strength · CWE-1204: Generation of Weak Initialization Vector (analogous key-derivation class) · CWE-390: Detection of Error Condition Without Action

---

## [SEC-011] Unvalidated identifiers and unbounded grades in the exam and prayer write paths produce 500s and nonsense data

- **Category:** Security
- **Severity:** Medium
- **Confidence:** High
- **Location:** `Jaberah/Controllers/PrayersController.cs:132-168` · `Jaberah/Controllers/ExamsController.cs:20-44, 45-81`

### Description

Three related input-validation gaps on write endpoints that (per SEC-002) any teacher can reach:

**(a) `POST /api/prayers/upsert-daily` validates nothing.** It has no validation filter, does not check that `dto.StudentId` exists, does not check that each `PrayerId` exists, does not bound `dto.Prayers`, and — critically — resolves "does this row already exist?" against `existingAttendances`, which was loaded from the database *before* the loop. Two entries with the same `PrayerId` in one request therefore both take the insert branch, and the unique index on `(PrayerDate, StudentId, PrayerId)` (`JaberahDBContext.cs:386-387`) rejects the second. The resulting `DbUpdateException` is not caught, so it surfaces as a 500.

**(b) `POST /api/exams/monthly-exam` does not verify the student exists.** Marks are clamped correctly (`Math.Max(Math.Min(...))`), but `newExam.StudentId = model.StudentId` is inserted with no existence check, so a non-existent id violates the `Exams → Students` foreign key → 500.

**(c) `POST /api/exams/mid-final-exam` accepts an unbounded grade.** `grade.Grade` is a `float?` written straight through with `?? 0`, with no clamp — in contrast to the monthly-exam path immediately above it, which clamps to 20 and 10.

### Evidence

```csharp
// Jaberah/Controllers/PrayersController.cs:132-157 — no student/prayer validation, in-request duplicates unhandled
[HttpPost("upsert-daily")]
public async Task<IActionResult> UpsertDaily(StudentDailyUpsertDTO dto)
{
    var existingAttendances = await _db.StudentPrayerAttendances
        .Where(x => x.StudentId == dto.StudentId && x.PrayerDate == dto.Date)
        .ToListAsync();

    foreach (var prayer in dto.Prayers)
    {
        var existing = existingAttendances                      // ← DB snapshot only; ignores prior loop iterations
            .FirstOrDefault(x => x.PrayerId == prayer.PrayerId);

        if (existing == null)
        {
            _db.StudentPrayerAttendances.Add(new StudentPrayerAttendance
            {
                StudentId = dto.StudentId,                      // never verified to exist
                PrayerId = prayer.PrayerId,                     // never verified to exist
```

```csharp
// Jaberah/Controllers/ExamsController.cs:33-41 — insert with an unverified StudentId
else // insert
{
    model.PaperExam = Math.Max(Math.Min(model.PaperExam ?? 0, 20), 0);
    model.OralExam  = Math.Max(Math.Min(model.OralExam  ?? 0, 10), 0);
    var newExam = _mapper.Map<Exam>(model);
    newExam.StudentId = model.StudentId;
    newExam.Date = model.Date;
    await _db.Exams.AddAsync(newExam);
}
```

```csharp
// Jaberah/Controllers/ExamsController.cs:63-77 — mid-final grade written with no bound
var final = await _db.MidFinals.FirstOrDefaultAsync(x => x.StudentId == studentId && x.FromDate == fromDate && x.ToDate == toDate);
if (final is null)
{
    await _db.MidFinals.AddAsync(new MidFinal { ..., Grade = grade.Grade ?? 0 });
}
else
{
    final.Grade = grade.Grade ?? final.Grade;
}
Console.WriteLine(grade.Grade);      // see SEC-018
```

Note how much better the neighbouring cleaning-logs module handles the identical concerns — an explicit validation filter that rejects in-request duplicates (`Validations/CleaningLogs/UpsertDailyCleaningLogValidation.cs:64-71`), caps the list at 100 entries (`:36-43`), and a `catch (DbUpdateException ex) when (IsDuplicateKeyViolation(ex))` that returns 409 (`CleaningLogsController.cs:334-342`).

### Impact

- **Trivial unhandled 500s** from any authenticated caller, each of which also appends to `Logs/error-requests.log` (feeding PERF-006 and consuming disk). Not a crash — `GlobalExceptionMiddleware` catches it — but it is unhandled error-path noise that masks real faults and is indistinguishable in the logs from a genuine failure.
- **Report corruption from the unbounded grade.** `MidFinal.Grade` enters the semester total at `ReportsController.cs:77-79` as `midFinal`, and the result is `(attendance + behavior + grade + oral + paper + midFinal) * 100.0 / 400.0` with no cap on the mid-final term. Setting `Grade = 1e9` yields a semester percentage of ~250,000,000, which the client renders directly. Since `Grade` is a `float` mapped to SQL `real`, `Infinity` is also representable via a large literal.
- **Unbounded request lists.** `dto.Prayers` has no length cap, so a single request can carry an arbitrarily long array, each element becoming a tracked entity in the change tracker before one `SaveChangesAsync`.

### Reproduction / Trigger

```bash
# (a) duplicate PrayerId in one request → 500
curl -X POST -H "Authorization: Bearer $TEACHER_TOKEN" -H "Content-Type: application/json" \
  -d '{"studentId":1,"date":"2026-09-04","prayers":[{"prayerId":1,"rakatCount":2,"isInGroup":true},{"prayerId":1,"rakatCount":2,"isInGroup":false}]}' \
  "https://jaberah-new.tryasp.net/api/prayers/upsert-daily"

# (a) non-existent student → FK violation → 500
curl -X POST -H "Authorization: Bearer $TEACHER_TOKEN" -H "Content-Type: application/json" \
  -d '{"studentId":999999,"date":"2026-09-04","prayers":[{"prayerId":1,"rakatCount":2,"isInGroup":true}]}' \
  "https://jaberah-new.tryasp.net/api/prayers/upsert-daily"

# (c) unbounded mid-final grade → 200, then a nonsense semester report
curl -X POST -H "Authorization: Bearer $TEACHER_TOKEN" -H "Content-Type: application/json" \
  -d '1000000000' \
  "https://jaberah-new.tryasp.net/api/exams/mid-final-exam?studentId=1&fromDate=2026-01-01&toDate=2026-05-01"
```

### Recommended Remediation

1. **Add a validation filter for `StudentDailyUpsertDTO`** modelled on `UpsertDailyCleaningLogValidation`: require a non-default date, require a non-empty `Prayers` list, cap its length, reject non-positive ids, and reject duplicate `PrayerId` values within the request.
2. **Verify referenced rows exist** before insert in all three actions — `if (!await _db.Students.AnyAsync(x => x.Id == dto.StudentId)) return BadRequest(...)`, and validate `PrayerId` against the cached prayer list.
3. **Clamp the mid-final grade** to its real domain. The commit history (`7eaf11d "fix smester report dates & accept 40 degree in mid final"`) indicates the intended maximum is 40: `Grade = Math.Clamp(grade.Grade ?? 0, 0, 40)`. Consider a database `CHECK` constraint too — `TeacherSalary` already uses this technique (`JaberahDBContext.cs:278-286`).
4. **Catch `DbUpdateException` on the upsert paths** and translate unique-index and FK violations to 409/400, reusing `CleaningLogsController.IsDuplicateKeyViolation`.

### Exploitability

**Exploitable and observable today** by any authenticated teacher; all three paths follow directly from the source.

### References

- OWASP API Security Top 10 2023 — API6:2023 Unrestricted Access to Sensitive Business Flows
- CWE-20: Improper Input Validation · CWE-1284: Improper Validation of Specified Quantity in Input · CWE-248: Uncaught Exception

---

## [SEC-012] Mobile client: student grade reports written to world-readable shared storage, cleartext HTTP enabled app-wide

- **Category:** Security
- **Severity:** Medium
- **Confidence:** High
- **Location:** `Jaberah-Flutter/lib/api/URLs.dart:1` · `Jaberah-Flutter/android/app/src/main/AndroidManifest.xml:12-18` · writers at `lib/controllers/admin/monthlyReportController.dart:504-510`, `semesterReportController.dart:323-328`, `bestStudentsController.dart:344-349`, `studentsPartialExamsController.dart:297-302`

### Description

Generated PDF reports are written to a fixed path in shared external storage:

```dart
// Jaberah-Flutter/lib/api/URLs.dart:1
const appFolder = '/storage/emulated/0/حلقات مسجد جابرة';
```

`/storage/emulated/0/...` is the shared user volume, not app-private storage. Files there survive uninstall and, on the API levels this app targets (`minSdk = 21`, with `requestLegacyExternalStorage="true"` and `MANAGE_EXTERNAL_STORAGE`), are readable by any other application that holds a storage permission. The reports contain named students' grades, attendance, behaviour scores and totals.

The manifest also requests the three broadest storage permissions available and enables cleartext HTTP for the entire application:

```xml
<!-- Jaberah-Flutter/android/app/src/main/AndroidManifest.xml:12-18 -->
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />
<uses-permission android:name="android.permission.MANAGE_EXTERNAL_STORAGE"/>
<application
    android:enableOnBackInvokedCallback="true"
    android:requestLegacyExternalStorage="true"
    android:usesCleartextTraffic="true"
```

### Evidence

```dart
// Jaberah-Flutter/lib/controllers/admin/monthlyReportController.dart:501-510
await requestStoragePermission();
final directory = Directory(appFolder);
if (!await directory.exists()) {
  await directory.create(recursive: true);
}
...
final file = File(filePath);
await file.writeAsBytes(await pdf.save());
```

`MANAGE_EXTERNAL_STORAGE` (`android:name="android.permission.MANAGE_EXTERNAL_STORAGE"`) is Android's "All files access" — it grants read/write over the entire shared volume, is gated behind a special Settings screen, and is restricted on Google Play to a narrow set of app categories. This app needs it only to write its own report folder.

### Impact

- **On-device disclosure of minors' academic records.** Any other installed app with storage access — or anyone who plugs the phone into a computer, or restores a cloud backup of the shared volume — can read every exported report. `android:allowBackup="false"` (line 19) correctly excludes the app's *private* data from backup, but has no effect on files written to shared storage.
- **Files persist after uninstall,** so removing the app does not remove the data.
- **`usesCleartextTraffic="true"` removes the platform's HTTPS-only guarantee.** The configured base URL is HTTPS (`URLs.dart:9`), so normal API traffic is encrypted; the risk is that any URL the app is *told* to open is permitted to be plaintext — including the APK update link, which comes from the server's `Version.URL` column (`Jaberah-ASP/Jaberah/Controllers/VersionsController.cs:35`) and is opened with `launchUrl` (`versionsController.dart:106-108`). Combined with SEC-004 there is no signature check on that download either.
- **`MANAGE_EXTERNAL_STORAGE` is an outsized blast radius** if the app itself is ever compromised — for example by a forged build per SEC-004, which would inherit the granted permission.

### Reproduction / Trigger

1. Export any report from the admin UI (Monthly Reports → export).
2. `adb shell ls "/storage/emulated/0/حلقات مسجد جابرة"` — the PDFs are listed.
3. `adb pull "/storage/emulated/0/حلقات مسجد جابرة"` — readable with no app-specific privilege; any app holding `READ_EXTERNAL_STORAGE` can do the same on-device.

### Recommended Remediation

1. **Write reports to app-private storage** — `path_provider`'s `getApplicationDocumentsDirectory()` (the package is already a dependency) — and surface them through the in-app list that `exportedReportsPageController.dart` already implements. Nothing in the current UX requires the files to be visible to other apps.
2. **Share via `FileProvider`/share-sheet** rather than a shared-volume path when a user genuinely wants to send a report out, so access is per-file and per-recipient.
3. **Drop `MANAGE_EXTERNAL_STORAGE`, `WRITE_EXTERNAL_STORAGE`, `READ_EXTERNAL_STORAGE` and `requestLegacyExternalStorage`** once (1) is done — with app-private storage none are needed on any supported API level.
4. **Remove `android:usesCleartextTraffic="true"`.** If a specific host must be reachable over HTTP during development, express that in a `network_security_config.xml` scoped to that host and applied only to the debug build type.
5. Also remove the unused `android.permission.USE_CREDENTIALS` (line 10), which has been non-functional since API 23.

### Exploitability

**Exploitable and observable today** on any device with the app installed and at least one report exported.

### References

- OWASP MASVS-STORAGE-1, MASVS-STORAGE-2 · OWASP Mobile Top 10 2024 — M9: Insecure Data Storage
- CWE-922: Insecure Storage of Sensitive Information · CWE-732: Incorrect Permission Assignment for Critical Resource · CWE-319: Cleartext Transmission of Sensitive Information

---

## [PERF-003] Every response is buffered into memory and every request body is re-buffered, including 100 MB APK uploads

- **Category:** Performance
- **Severity:** Medium
- **Confidence:** High
- **Location:** `Jaberah/Middlewares/RequestResponseLoggingMiddleware.cs:28-65` · registered first in the pipeline at `Jaberah/Program.cs:151`

### Description

The logging middleware wraps **every** request in the application, not only failing ones. For each request it calls `EnableBuffering()` on the request body and swaps `Response.Body` for a `MemoryStream`, lets the whole pipeline write into that stream, reads it back in full as a string, and only then copies it to the real response:

```csharp
// Jaberah/Middlewares/RequestResponseLoggingMiddleware.cs:28-49, 64
public async Task Invoke(HttpContext context)
{
    context.Request.EnableBuffering();
    ...
    var originalBodyStream = context.Response.Body;
    using var responseBody = new MemoryStream();
    context.Response.Body = responseBody;

    await _next(context);

    context.Response.Body.Seek(0, SeekOrigin.Begin);
    string responseText = await new StreamReader(context.Response.Body).ReadToEndAsync();
    context.Response.Body.Seek(0, SeekOrigin.Begin);

    if (context.Response.StatusCode < 200 || context.Response.StatusCode >= 300)
    { /* log */ }

    await responseBody.CopyToAsync(originalBodyStream);
}
```

The status check that decides whether to *log* happens at line 51 — after the body has already been fully buffered and fully read into a `string`. So the cost is paid on every successful request too, for a log line that is then not written.

The `IsLoggableBody` and `Redact` helpers (lines 69-109) are well done — they correctly exclude `multipart/*` and `octet-stream`, cap at 4 KB, and mask password and token fields. They govern only whether the request body is *read into a variable*; they do not prevent `EnableBuffering()`, and they do not apply to the response path at all.

### Evidence

- `EnableBuffering()` with no arguments uses a 30 KB memory threshold and then spools to a temp file on disk, with **no size limit**. The APK upload endpoint accepts 100 MB (`VersionsController.cs:45 [RequestSizeLimit(100_000_000)]`), so every release publish writes a ~50–100 MB temp file that the application never reads (line 33's `IsLoggableBody` returns `false` for `multipart/`).
- `responseText` is a full `string` copy of the response. For `GET /api/reports/monthly-report` (PERF-001) the serialised JSON is held three times concurrently: the entity graph, the `MemoryStream`, and the `string`. A .NET `string` is UTF-16, so the string copy is roughly **twice** the byte size of the JSON.
- Buffering also defeats streaming: nothing is flushed to the client until the action has completed and the copy runs, so time-to-first-byte equals time-to-last-byte for every response.

### Impact

- **~3× peak memory on large responses,** with the string copy landing on the Large Object Heap for anything over 85 KB — which every report response exceeds. This directly compounds PERF-001.
- **Wasted disk I/O and temp-file churn** on APK uploads, plus disk pressure on a shared host.
- **No streaming, worse tail latency** on every endpoint in the application.
- **A fragile edge case:** if an exception escaped `GlobalExceptionMiddleware` (registered *inside* this middleware at `Program.cs:152`), the `CopyToAsync` on line 64 would be skipped and the client would receive an empty body. In practice `GlobalExceptionMiddleware` catches everything downstream, so this is latent rather than active.

### Reproduction / Trigger

```bash
# Watch working set while requesting a large report repeatedly
for i in $(seq 1 20); do
  curl -s -o /dev/null -H "Authorization: Bearer $T" \
    "https://jaberah-new.tryasp.net/api/reports/monthly-report?groupId=0&fromDate=2020-01-01&toDate=2030-01-01" &
done; wait
```

Peak managed memory grows well beyond the size of the data itself. For the upload path, publish a release and observe a ~100 MB file appear under the ASP.NET Core temp directory for the duration of the request.

### Recommended Remediation

1. **Stop buffering the response.** The middleware only needs the response *body* for non-2xx statuses, which are small JSON error objects. Register an `OnStarting`/`OnCompleted` callback to log method, path, status and the (already-captured, already-redacted) request body, and drop the `MemoryStream` swap entirely. If error bodies are genuinely wanted, buffer conditionally — let the pipeline run, and only capture when the status warrants it, which requires an `IHttpResponseBodyFeature` wrapper rather than a blanket swap.
2. **Bound `EnableBuffering`.** Call it only when `IsLoggableBody(context.Request)` is true, and pass an explicit limit: `context.Request.EnableBuffering(bufferThreshold: 32 * 1024, bufferLimit: 64 * 1024)`. That removes the 100 MB temp file entirely.
3. **Use the built-in middleware** for the common case: `app.UseHttpLogging()` with `HttpLoggingFields.RequestPropertiesAndHeaders | ResponsePropertiesAndHeaders` plus a `RedactedHeaders`/`Interceptor` gives structured request logging without hand-rolled body buffering.
4. **Swap the middleware order** so `GlobalExceptionMiddleware` is outermost (`Program.cs:151-152`), which makes the copy-skip edge case unreachable by construction.

### Exploitability

**Observable today.** The unconditional buffering is explicit in source and applies to 100% of requests.

### References

- CWE-770: Allocation of Resources Without Limits or Throttling · CWE-789: Memory Allocation with Excessive Size Value
- [ASP.NET Core HTTP logging](https://learn.microsoft.com/aspnet/core/fundamentals/http-logging/)

---

## [PERF-004] `GET /api/prayers` caches a *paginated* result under a single fixed key for 30 days

- **Category:** Performance
- **Severity:** Medium
- **Confidence:** High
- **Location:** `Jaberah/Controllers/PrayersController.cs:21-52`

### Description

`GetAllPrayers` accepts `PageNumber` and `PageSize`, applies `Skip`/`Take`, and then stores the resulting page in the shared `IMemoryCache` under the constant key `"allPrayers"` — with no pagination parameters in the key.

The first caller after a cold start therefore decides what every subsequent caller receives, for up to 30 days (absolute) with a 24-hour sliding renewal. There is no invalidation path for this key anywhere in the codebase.

### Evidence

```csharp
// Jaberah/Controllers/PrayersController.cs:21-51
[HttpGet]
public async Task<IActionResult> GetAllPrayers([FromQuery] PaginationDTO query)
{
    var (skip, take) = (query.PageNumber, query.PageSize);
    const string cacheKey = "allPrayers";                       // ← no page/size in the key

    if (!_cache.TryGetValue(cacheKey, out List<Prayer>? prayers))
    {
        prayers = await _db.Prayers.AsNoTracking()
            .Select(p => new Prayer { Id = p.Id, NameAr = p.NameAr, NameEn = p.NameEn,
                                      DefaultRakats = p.DefaultRakats, DisplayOrder = p.DisplayOrder })
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Id)
            .Skip((skip - 1) * take)                            // ← page-dependent result...
            .Take(take)
            .ToListAsync();

        _cache.Set(cacheKey, prayers, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30),   // ...cached for 30 days
            SlidingExpiration = TimeSpan.FromHours(24)
        });
    }
    return Ok(prayers);
}
```

Contrast the correct treatment of the same concern two files over, where the cache key varies with the parameter that varies the result:

```csharp
// Jaberah/Controllers/GroupsController.cs:38
var cacheKey = withoutTeacher ? $"{CacheKey}_WithoutTeacher" : CacheKey;
```

### Impact

- **Shared-cache poisoning by any authenticated user.** One request with `?pageSize=1` makes `GET /api/prayers` return a single prayer to every user of the system until the entry expires or the process recycles. The five prayers are reference data the daily-prayer entry screen depends on, so the practical effect is a broken feature for everyone, triggered by one malformed or curious request.
- **Not self-healing.** The 24-hour sliding expiration is *renewed on every read*, so as long as the endpoint keeps being called — which it is, on every visit to the prayers screen — the poisoned entry survives until the 30-day absolute cap or an app-pool recycle. Nothing in the codebase removes this key.
- **`pageNumber=0` is a 500 rather than a poisoned cache:** `Skip((0 - 1) * 10)` → `Skip(-10)`, which SQL Server rejects ("The offset specified in a OFFSET clause may not be negative"), and the exception escapes as a 500 before anything is cached. See PERF-005.
- The pagination itself is pointless here: `Prayer` is seeded with exactly five immutable rows (`JaberahDBContext.cs:357-363`) and has no write endpoint.

### Reproduction / Trigger

```bash
# Poison the shared cache (any authenticated token)
curl -H "Authorization: Bearer $T" "https://jaberah-new.tryasp.net/api/prayers?pageNumber=1&pageSize=1"
# → [{"id":1,"nameAr":"الفجر",...}]

# Every other user, with default parameters, now gets the same single-element array
curl -H "Authorization: Bearer $OTHER_T" "https://jaberah-new.tryasp.net/api/prayers"
# → [{"id":1,...}]   (expected: all five prayers)

# And the negative-offset 500
curl -H "Authorization: Bearer $T" "https://jaberah-new.tryasp.net/api/prayers?pageNumber=0"
```

### Recommended Remediation

1. **Remove the pagination.** Five rows of static reference data should be returned whole:
   ```csharp
   [HttpGet]
   public async Task<IActionResult> GetAllPrayers()
   {
       const string cacheKey = "allPrayers";
       if (!_cache.TryGetValue(cacheKey, out List<PrayerDto>? prayers))
       {
           prayers = await _db.Prayers.AsNoTracking()
               .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Id)
               .Select(p => new PrayerDto { ... }).ToListAsync();
           _cache.Set(cacheKey, prayers, TimeSpan.FromHours(12));
       }
       return Ok(prayers);
   }
   ```
   `CleaningLogsController.GetAllTasksAsync:417-443` already demonstrates this shape for the analogous `CleaningTasks` reference table — cache the whole list once, filter in memory.
2. **If pagination must stay, put the parameters in the key** (`$"allPrayers_{skip}_{take}"`) and clamp them (`Math.Max(pageNumber, 1)`, `Math.Clamp(pageSize, 1, 100)`).
3. **Audit the other single-key caches** for the same class of bug. `GroupsController:38` and `CleaningLogsController:24` are correct; the deleted-entity caches (`StudentsController:151`, `TeachersController:288`, `GroupsController:268`) take no parameters and are therefore safe, though their 7-day absolute expirations are longer than the data's volatility warrants.
4. **Return a DTO, not the entity.** `List<Prayer>` is cached with its `Attendances` navigation collection attached (empty here, but the type invites accidental graph retention).

### Exploitability

**Exploitable and observable today** by any authenticated user with a single GET request.

### References

- CWE-524: Use of Cache Containing Sensitive Information (variant: incorrect cache keying) · CWE-444-adjacent cache-key confusion
- OWASP: Web Cache Poisoning (the same keying flaw, applied to an in-process cache)

---

## [PERF-005] Unbounded `pageSize` and unvalidated `pageNumber` across four list endpoints

- **Category:** Performance
- **Severity:** Medium
- **Confidence:** High
- **Location:** `Jaberah/Controllers/StudentsController.cs:26, 47-53` · `Jaberah/Controllers/TeachersController.cs:27, 45-50` · `Jaberah/Controllers/GroupsController.cs:109, 134-139` · `Jaberah/Controllers/NotificationsController.cs:68-70` · `Jaberah/Helpers/PaginationDTO.cs:5-6` · `Jaberah/Helpers/PagedList.cs:30-42`

### Description

Four list endpoints accept `pageSize` directly from the query string with a default of 10 and **no upper bound**, and `pageNumber` with no lower bound. `ToPagedListAsync` and the inline paging code both compute `Skip((pageNumber - 1) * pageSize)` without clamping.

Two of the codebase's own endpoints show the correct handling, which makes this an inconsistency rather than an unknown:

```csharp
// Jaberah/Controllers/CleaningLogsController.cs:193
var (pageNumber, pageSize) = (Math.Max(query.PageNumber, 1), Math.Clamp(query.PageSize, 1, MaxPageSize));

// Jaberah/Controllers/LogsController.cs:27-28
if (pageNumber < 1) pageNumber = 1;
pageSize = Math.Clamp(pageSize, 1, 100);
```

### Evidence

```csharp
// Jaberah/Controllers/StudentsController.cs:25-53 — no clamp on either parameter
[HttpGet]
public async Task<IActionResult> GetStudents([FromQuery] string searchText = "", [FromQuery] bool withoutGroup = false,
                                             [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
{
    ...
    var pagedStudents = (await selectedQuery
            .OrderByDescending(s => s.MemoRate)
            .ThenBy(s => s.Id)
            .Skip((pageNumber - 1) * pageSize)      // pageNumber=0 → Skip(-10)
            .Take(pageSize)                         // pageSize=1000000 → Take(1000000)
            .ToListAsync())
        .ToPagedList(await selectedQuery.CountAsync(), pageNumber, pageSize);
```

```csharp
// Jaberah/Controllers/NotificationsController.cs:67-71 — same, via the shared helper
[HttpGet]
public async Task<IActionResult> GetNotifications([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
{
    return Ok(await _db.Notifications.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToPagedListAsync(pageNumber, pageSize));
}
```

```csharp
// Jaberah/Helpers/PagedList.cs:30-42 — the helper does not clamp either
public static async Task<PagedList<T>> ToPagedListAsync<T>(this IQueryable<T> source, int pageNumber, int pageSize)
{
    var count = await source.CountAsync();
    var data = await source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
```

Also note that `PagedList<T>` divides by `pageSize` when computing `TotalPages` (`:21`), so `pageSize=0` is a `DivideByZeroException`… except that `Take(0)` and `Skip(-0)` execute first, so the actual failure is the divide → 500.

### Impact

- **Pagination can be switched off by the caller.** `GET /api/students?pageSize=1000000` returns the entire student table in one payload; the same for teachers (with each teacher's group list nested), any group's students, and notifications. This defeats the pagination the client relies on, and each such response is then buffered twice more by PERF-003.
- **Two easy 500s** on every one of these endpoints: `pageNumber=0` (negative `OFFSET`, rejected by SQL Server) and `pageSize=0` (`DivideByZeroException` in `PagedList`). Both are reachable by any authenticated user, and each writes a log entry (PERF-006).
- **Doubled query cost per page.** Each of these endpoints executes the projection twice — once for the page and once for `CountAsync()` — against the same filtered query. For `TeachersController` the counted query includes the nested `Groups.Select(...)` projection, which EF must still plan.

### Reproduction / Trigger

```bash
T="Authorization: Bearer $ADMIN_TOKEN"
curl -H "$T" "https://jaberah-new.tryasp.net/api/students?pageSize=1000000"   # whole table
curl -H "$T" "https://jaberah-new.tryasp.net/api/students?pageNumber=0"       # 500 (negative OFFSET)
curl -H "$T" "https://jaberah-new.tryasp.net/api/students?pageSize=0"         # 500 (divide by zero)
curl -H "Authorization: Bearer $TEACHER_TOKEN" \
     "https://jaberah-new.tryasp.net/api/notifications?pageSize=1000000"      # teacher-reachable
```

### Recommended Remediation

1. **Clamp inside `PagedList`,** so every current and future caller is covered by construction:
   ```csharp
   public static async Task<PagedList<T>> ToPagedListAsync<T>(this IQueryable<T> source, int pageNumber, int pageSize)
   {
       pageNumber = Math.Max(pageNumber, 1);
       pageSize   = Math.Clamp(pageSize, 1, 100);
       ...
   }
   ```
   and mirror it in `PagedList<T>`'s constructor and the `ToPagedList` overload.
2. **Clamp in `PaginationDTO`** via property setters, so DTO-bound endpoints (`QueryAssignableStudentsDTO`, `QueryDayilyPrayersDTO`, `QueryMonthlyPrayersReportDTO`) inherit the bound.
3. **Add the same clamp to the four inline paging sites** listed above, matching `CleaningLogsController.cs:193`.
4. Consider replacing `CountAsync()` on hot list endpoints with a "has next page" probe (`Take(pageSize + 1)`), which removes the second query where an exact total is not displayed.

### Exploitability

**Exploitable and observable today** by any authenticated user.

### References

- OWASP API Security Top 10 2023 — API4:2023 Unrestricted Resource Consumption
- CWE-770: Allocation of Resources Without Limits or Throttling · CWE-1284: Improper Validation of Specified Quantity in Input · CWE-369: Divide By Zero

---

## [PERF-006] `GET /api/Logs` reads the entire log file on every request, and the log file has no rolling policy

- **Category:** Performance
- **Severity:** Medium
- **Confidence:** High
- **Location:** `Jaberah/Controllers/LogsController.cs:19-83` (specifically 34-44) · `Jaberah/Middlewares/RequestResponseLoggingMiddleware.cs:19-25`

### Description

`LogsController.GetLogs` uses a fixed-capacity ring buffer to keep only the last 5,000 lines, which correctly bounds **memory**. It does not bound **I/O**: to reach the last 5,000 lines it reads the file from the beginning, line by line, on every request.

```csharp
// Jaberah/Controllers/LogsController.cs:30-44
// حلقة ثابتة السعة: تحتفظ بآخر MaxScannedLines سطرًا فقط مهما بلغ حجم الملف.
var recent = new string[MaxScannedLines];
var seen = 0;

using (var fileStream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
using (var reader = new StreamReader(fileStream))
{
    string? line;
    while ((line = reader.ReadLine()) != null)     // ← full sequential scan of the whole file
    {
        if (line.Length == 0) continue;
        recent[seen % MaxScannedLines] = line;
        seen++;
    }
}
```

The file the scan traverses is unbounded in practice. The Serilog sink is configured without a size limit or rolling policy:

```csharp
// Jaberah/Middlewares/RequestResponseLoggingMiddleware.cs:19-25
_requestLogger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        formatter: new JsonFormatter(),
        path: "Logs/error-requests.log",
        shared: true)                       // no rollingInterval, no fileSizeLimitBytes override,
    .CreateLogger();                        // no rollOnFileSizeLimit, no retainedFileCountLimit
```

Serilog's file sink defaults to a 1 GiB `fileSizeLimitBytes` with `rollOnFileSizeLimit: false`, so the file grows to 1 GiB and then **silently stops recording anything** — the worse of the two failure modes, because logging dies without notice.

### Evidence

Every non-2xx response in the entire application appends a line here (`RequestResponseLoggingMiddleware.cs:51-62`), including each entry with a truncated-to-4 KB request body and a truncated-to-4 KB response body. Since a great many of the findings in this report produce 400s and 500s on demand (SEC-006, SEC-011, SEC-014, PERF-005), an attacker can drive growth deliberately.

`_requestLogger` is also constructed in the middleware **constructor**, which for middleware registered via `UseMiddleware<T>` runs once per application lifetime — so that part is fine — but the logger is never disposed and coexists with the `Serilog` host logger configured at `Program.cs:17`, giving two independent sinks.

### Impact

- **Admin request latency proportional to log size.** At 100 MB the scan is hundreds of milliseconds of synchronous, blocking file I/O on a thread-pool thread (the action is not `async` — `IActionResult GetLogs`, no `await` anywhere), for every page the admin views.
- **Logging silently stops at 1 GiB,** losing all subsequent error visibility. There is no alert; the only symptom is that `/api/Logs` stops showing new entries.
- **Disk exhaustion risk** on a shared host at up to 1 GiB per deployment slot, which on MonsterASP-class hosting is a meaningful fraction of the quota. `DELETE /api/Logs` (`:86-97`) exists but is manual and truncates the whole file.
- **Blocking I/O on the request thread.** `ReadLine()` and `File.WriteAllText` (`:94`) are synchronous; the latter also has no file-share mode, so it can collide with the sink's own writes.

### Reproduction / Trigger

```bash
# Generate log volume cheaply — each 400 appends a line
for i in $(seq 1 20000); do
  curl -s -o /dev/null "https://jaberah-new.tryasp.net/api/versions?version=x" &
  [ $((i % 50)) -eq 0 ] && wait
done

# Then time the admin log page
time curl -s -o /dev/null -H "Authorization: Bearer $ADMIN_TOKEN" \
  "https://jaberah-new.tryasp.net/api/Logs?pageNumber=1&pageSize=10"
```

Response time grows with accumulated file size even though the returned page is always 10 entries.

### Recommended Remediation

1. **Add rolling and retention to the sink:**
   ```csharp
   .WriteTo.File(
       formatter: new JsonFormatter(),
       path: "Logs/error-requests-.log",
       rollingInterval: RollingInterval.Day,
       fileSizeLimitBytes: 20 * 1024 * 1024,
       rollOnFileSizeLimit: true,
       retainedFileCountLimit: 14,
       shared: true)
   ```
   This bounds total disk use, guarantees logging never silently stops, and makes the newest file small — which alone fixes most of the read cost.
2. **Read backwards instead of forwards.** Seek to `fileStream.Length`, read fixed-size blocks in reverse, and stop once `pageNumber * pageSize` newlines have been counted. Cost then depends on the page requested, not on file size.
3. **Make the action `async`** and use `ReadLineAsync`/`File.WriteAllTextAsync`, so a large read does not occupy a thread-pool thread.
4. **Prefer a structured sink over a file** if this endpoint is a real operational need — Serilog is already registered on the host (`Program.cs:17`) and could write to a table or an external collector, which gives indexed queries instead of a scan.
5. **Reconsider exposing error bodies over HTTP at all.** `GET /api/Logs` is admin-only and the sensitive-field redaction at `RequestResponseLoggingMiddleware.cs:69-91` is sound, but the endpoint still surfaces other users' request and response bodies — student data included — to anyone holding an admin token, which is exactly what SEC-001 puts at risk.

### Exploitability

**Observable today.** The full-file scan and the absent rolling policy are both explicit in source. Current file size was not measurable from the repository (`Logs/` is git-ignored).

### References

- CWE-400: Uncontrolled Resource Consumption · CWE-779: Logging of Excessive Data · CWE-1050: Excessive Platform Resource Consumption within a Loop

---

## [PERF-007] Cache invalidation is incomplete: adding a teacher or student leaves group caches stale for up to 7 days

- **Category:** Performance
- **Severity:** Medium
- **Confidence:** High
- **Location:** `Jaberah/Controllers/TeachersController.cs:348-353` · `Jaberah/Controllers/StudentsController.cs:209-213` · cache writers at `Jaberah/Controllers/GroupsController.cs:71-75, 96-100, 151-161`

### Description

Five distinct memory-cache keys hold group-related projections, each with a 7-day absolute expiration. Three controllers write to the underlying data, and each has its own private `InvalidateCache()` that evicts a *different subset* of those keys:

| Key | Written at | Evicted by `GroupsController` | by `TeachersController` | by `StudentsController` |
|---|---|---|---|---|
| `GroupsCache` | `GroupsController:71` | ✔ | ✔ | ✔ |
| `GroupsCache_WithoutTeacher` | `GroupsController:71` | ✔ | ✔ | ✔ |
| `GroupsForGeneralUse` | `GroupsController:96` | ✔ | ✖ | ✖ |
| `GroupsWithNoTeacher` | `GroupsController:160` | ✔ | partly¹ | ✖ |
| `TeachersForGeneralUse` | `TeachersController:70` | ✖ | ✔ | ✖ |
| `GroupsCache_DeletedGroups` | `GroupsController:287` | ✔ | ✖ | ✖ |

¹ `TeachersController.UpdateTeacher` removes `GroupsWithNoTeacher` explicitly at line 260, but `AddTeacher`, `DeleteTeacher` and `RestoreTeacher` — which all change which groups have a teacher — do not.

### Evidence

```csharp
// Jaberah/Controllers/TeachersController.cs:347-353 — misses GroupsForGeneralUse and GroupsWithNoTeacher
private void InvalidateCache()
{
    _cache.Remove("GroupsCache");
    _cache.Remove("GroupsCache_WithoutTeacher");
    _cache.Remove("TeachersForGeneralUse");
}
```

```csharp
// Jaberah/Controllers/GroupsController.cs:388-395 — misses TeachersForGeneralUse
private void InvalidateCache()
{
    _cache.Remove(CacheKey);
    _cache.Remove($"{CacheKey}_WithoutTeacher");
    _cache.Remove("GroupsForGeneralUse");
    _cache.Remove("GroupsWithNoTeacher");
    _cache.Remove($"{CacheKey}_DeletedGroups");
}
```

```csharp
// Jaberah/Controllers/GroupsController.cs:151-161 — the stale entry, held for 7 days
const string cacheKey = "GroupsWithNoTeacher";
if (!_cache.TryGetValue(cacheKey, out var groups))
{
    groups = await _db.Groups.AsNoTracking()
        .Where(g => !g.TeacherId.HasValue)
        .Select(g => new { g.Id, g.Name })
        .ToListAsync();
    _cache.Set(cacheKey, groups, TimeSpan.FromDays(7));
}
```

Note this call also uses `out var groups`, so `groups` is typed `object` and the cached value is an anonymous type — which makes the entry unusable by any other code path and easy to break silently on refactor.

### Impact

- **Admin UI shows wrong data for up to 7 days.** After `POST /api/teachers` assigns groups to a new teacher, `GET /api/groups/has-no-teacher-data` still lists those groups as unassigned, and `GET /api/groups/for-general-use` may omit groups added since. The admin's "assign teacher" workflow is driven by exactly these lists, so the practical result is an admin attempting to assign an already-assigned group, which the server then rejects with "هناك معلمين مرتبطين ببعض هذه الحلقات" (`TeachersController:172`) — a confusing failure with no obvious cause.
- **`DeleteTeacher` is the worst case**: it clears a teacher's groups, so those groups genuinely become unassigned, but `GroupsWithNoTeacher` is not evicted and continues to omit them — they become invisible for reassignment.
- **Correctness masquerading as a caching win.** The cached queries are trivial: `Groups` and `Teachers` hold ten rows each. A 7-day cache over a ten-row projection buys nothing measurable and costs correctness. `AbsoluteExpirationRelativeToNow = 7 days` combined with `SlidingExpiration = 12 hours` (`GroupsController:73-74`) also means a regularly used entry is renewed indefinitely up to the 7-day cap.
- **No `SizeLimit` on the cache.** `AddMemoryCache()` (`Program.cs:33`) is registered with defaults, so entries have no size accounting and the cache is bounded only by expiration and GC pressure. That is acceptable at this data volume but is not a property to rely on.

### Reproduction / Trigger

```bash
A="Authorization: Bearer $ADMIN_TOKEN"
# 1. Populate the cache
curl -H "$A" "https://jaberah-new.tryasp.net/api/groups/has-no-teacher-data"   # note which groups appear
# 2. Assign one of those groups to a new teacher
curl -X POST -H "$A" -H "Content-Type: application/json" \
  -d '{"teacherName":"معلم تجريبي","phoneNumber":"711111111","groupsId":[<id from step 1>]}' \
  "https://jaberah-new.tryasp.net/api/teachers"
# 3. Re-read — the group is still listed as having no teacher
curl -H "$A" "https://jaberah-new.tryasp.net/api/groups/has-no-teacher-data"
```

### Recommended Remediation

1. **Centralise invalidation.** Replace the three private `InvalidateCache()` methods with one service that owns every key and exposes intent-named methods (`InvalidateGroupProjections()`, `InvalidateTeacherProjections()`), each evicting the full dependent set. Three private methods maintaining overlapping key lists by hand is the root cause.
2. **Or use a `CancellationChangeToken` per aggregate.** Register every group-derived entry with `options.AddExpirationToken(new CancellationChangeToken(_groupsCts.Token))`; cancelling the source evicts all of them atomically and removes the possibility of a missed key.
3. **Shorten the expirations dramatically** — 30–60 seconds is ample for a ten-row list and makes any missed invalidation self-correcting. Given the data volume, dropping the caches for `GroupsWithNoTeacher` and `GroupsForGeneralUse` entirely is a defensible simplification.
4. **Type the cached values.** Replace the anonymous type at `GroupsController:157` with a named DTO so the entry is typed, reusable and refactor-safe.
5. **Note that `IMemoryCache` is per-process.** If the app is ever scaled to more than one instance or the IIS app pool runs multiple worker processes, invalidation in one process will not reach the others; a distributed cache or short expirations would be required.

### Exploitability

**Observable today** via the reproduction above; the missing `Remove` calls are verifiable by inspection.

### References

- CWE-672: Operation on a Resource after Expiration or Release (stale-read class)
- [ASP.NET Core in-memory cache dependencies](https://learn.microsoft.com/aspnet/core/performance/caching/memory#cache-dependencies)

---

## [PERF-008] Every authenticated request validates the JWT twice and issues an extra database round trip

- **Category:** Performance
- **Severity:** Medium
- **Confidence:** High
- **Location:** `Jaberah/Middlewares/VerifyToken.cs:17-49, 74-106` · `Jaberah/Program.cs:57-91`

### Description

The application performs authentication twice per request, through two independent mechanisms:

1. **`JwtBearer` middleware** (`Program.cs:57-74`) validates the signature and lifetime, and builds a `ClaimsPrincipal`. The fallback authorization policy (`Program.cs:85-91`) makes this mandatory for every endpoint without `[AllowAnonymous]`.
2. **`VerifyTokenAttribute`** (`VerifyToken.cs:17-49`), applied at class level on ten of the twelve controllers, re-parses the `Authorization` header, calls `ValidateToken` **again** — a second HMAC-SHA256 verification, a second base64 decode, a second claims-parse — and then issues a database query to load the user:

```csharp
// Jaberah/Middlewares/VerifyToken.cs:96-100 — one query per request, no caching
return await _db.Teachers
    .AsNoTracking()
    .Where(x => x.Id == teacherId)
    .Select(x => new UserViewModel { Id = x.Id, Name = x.Name, PhoneNumber = x.PhoneNumber, Role = x.Role.ToString() })
    .FirstOrDefaultAsync();
```

The only information the filter adds beyond what the principal already carries is `Role` and `PhoneNumber`. `Id` and `Name` are already in the token as `nameid` and `unique_name` — indeed `TeachersAttendancesController.cs:285` and `TeachersSalariesController.cs:111, 134` read the id straight from `User.FindFirstValue(ClaimTypes.NameIdentifier)` without touching `HttpContext.Items` at all, proving the claim is available.

### Evidence

```csharp
// Jaberah/Middlewares/VerifyToken.cs:19-38 — header re-parsed and token re-validated after JwtBearer already did both
var authHeader = context.HttpContext.Request.Headers.Authorization.ToString();
if (string.IsNullOrWhiteSpace(authHeader)) { context.Result = new UnauthorizedResult(); return; }
var parts = authHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries);
var token = parts.Length == 2 && parts[0].Equals("bearer", StringComparison.OrdinalIgnoreCase) ? parts[1] : null;
if (token == null) { context.Result = new UnauthorizedResult(); return; }
var user = await _token.VerifyToken(token);      // second full validation + DB query
```

Applied at class level on: `StudentsController:17`, `TeachersController:18`, `GroupsController:18`, `ReportsController:14`, `ExamsController:14`, `FollowStudentsController:13`, `PrayersController:15`, `CleaningLogsController:18`, `NotificationsController:14`, `TeachersAttendancesController:16`, `TeachersSalariesController:16`, `LogsController:8` — i.e. essentially the whole API.

The `HangfireAuthorizationFilter` adds a third path, calling `VerifyToken` synchronously per dashboard request (see PERF-011).

### Impact

- **One unavoidable database round trip per authenticated request,** on top of whatever the action itself queries. On the `GET /api/versions`-style hot paths this is the dominant cost; across the API it adds a fixed latency floor and one connection-pool acquisition to every single call.
- **Duplicated HMAC verification.** HS256 over a small token is cheap in absolute terms (tens of microseconds) but it is pure waste, doubled on 100% of requests.
- **The database becomes an availability dependency of authorization,** not just of data: a slow or unavailable SQL Server turns every request into a 403 (`VerifyToken`'s `catch` returns `null` → `ForbidResult`), which is both a misleading status code and an outage amplifier.
- **A whole class of bug is enabled by having two sources of identity** — SEC-010 (the encoding divergence between the two validators) exists only because there are two validators.

There is a genuine benefit to the DB read that should not be discarded: it makes soft-deleting a teacher revoke their access immediately, since the global query filter excludes them. That is currently the system's *only* revocation mechanism (SEC-009).

### Reproduction / Trigger

Enable EF command logging (or attach SQL Server Profiler) and issue a single simple authenticated request:

```bash
curl -H "Authorization: Bearer $T" "https://jaberah-new.tryasp.net/api/prayers"
```

Two queries are logged: `SELECT TOP(1) ... FROM [Teachers] WHERE [Id] = @id` from the filter, then the action's own query — even though the second is served from cache.

### Recommended Remediation

1. **Stop re-validating the token.** Rewrite the filter to read the already-validated principal rather than the raw header:
   ```csharp
   var id = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
   if (!int.TryParse(id, out var teacherId)) { context.Result = new UnauthorizedResult(); return; }
   ```
   This removes the duplicate crypto and, as a side effect, eliminates SEC-010 by leaving exactly one place where `TokenKey` is interpreted.
2. **Cache the user lookup briefly.** `IMemoryCache` is already registered; a 30–60 second entry keyed on `teacherId` removes the per-request query while keeping soft-delete revocation acceptably prompt:
   ```csharp
   var user = await _cache.GetOrCreateAsync($"user:{teacherId}", e => {
       e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
       return LoadUserAsync(teacherId);
   });
   ```
   Evict the key in `UpdateTeacher`, `DeleteTeacher` and `RestoreTeacher`, so role and status changes take effect at once.
3. **Or put `Role` in the token** and drop the DB read from the hot path entirely — combined with the `SecurityStamp` recommended in SEC-009, that gives both revocation and zero-query authorization. This is the cleanest end state.
4. **Return 401, not 403, when the token is unparseable or expired.** `VerifyTokenAttribute:40-43` returns `ForbidResult` for what are authentication failures; the client's Dio interceptor only triggers a refresh on 401 (`Jaberah-Flutter/lib/api/Dio.dart:43`), so an expired token currently produces a 403 that the client cannot recover from and treats as a hard failure.

### Exploitability

**Observable today** in any request trace; the duplication is structural.

### References

- CWE-1050: Excessive Platform Resource Consumption within a Loop (per-request analogue)
- [ASP.NET Core authorization filters vs. middleware](https://learn.microsoft.com/aspnet/core/mvc/controllers/filters)

---

## Low Findings

The Low findings are presented in a compressed format. Each retains an exact location, a code reference and a reproduction, but the discussion is shortened in proportion to impact.

## [SEC-013] Unvalidated `year`, `month` and `daysInMonth` produce unhandled 500s

- **Category:** Security · **Severity:** Low · **Confidence:** High
- **Location:** `Jaberah/Controllers/ReportsController.cs:273-278, 346-351` · `Jaberah/Controllers/PrayersController.cs:171-181` · `Jaberah/Controllers/TeachersSalariesController.cs:23-26, 67-68`

### Description & Evidence

Four endpoints validate `year`/`month` only for positivity and then construct a `DateTime` from them:

```csharp
// Jaberah/Controllers/ReportsController.cs:273-278
if (year <= 0 || month <= 0)
    return BadRequest(new { message = "ادخل سنة وشهر صحيح" });   // month > 12 and year > 9999 pass

var fromDate = new DateTime(year, month, 1);                      // ArgumentOutOfRangeException
var toDate = fromDate.AddMonths(1);
var daysInMonth = DateTime.DaysInMonth(year, month);              // also throws; and is never used
```

`PrayersController.GetMonthlyPrayersReport` is worse, because it takes the *number of days in the month* from the client and does arithmetic with it:

```csharp
// Jaberah/Controllers/PrayersController.cs:173-181
var (date, daysInMonth, groupId, skip, take) = (query.Date, query.DaysInMonth, ...);
if (date.Equals(default)) return BadRequest(new { message = "ادخل تاريخ صحيح" });

var prayersPerDay = await _db.Prayers.AsNoTracking().ToListAsync();
var totalPossibleRakats = daysInMonth * prayersPerDay.Sum(p => p.DefaultRakats);   // unchecked multiply
var start = date;
var end = start.AddDays(daysInMonth);                                              // ArgumentOutOfRangeException
```

`DaysInMonth` is `required` on the DTO (`Jaberah/Models/DTOs/Prayers.cs:28`) but has no range attribute, so any `int` is accepted. Two further defects sit in the same method: `missedPrayers = totalPossibleRakats - totalPrayed` (`:225`) subtracts a *prayer count* from a *rakat count*, producing meaningless `MissedPercentage` values; and `AverageCommitmentPercentage` is computed identically twice (`:261-264` and `:268-271`) with `totalPossibleAllStudents` (`:266`) assigned and never read.

### Impact

Unhandled `ArgumentOutOfRangeException` → `GlobalExceptionMiddleware` → 500 on every one of these endpoints, reachable by an authenticated caller with a one-character change to a query string; each occurrence appends to the error log (PERF-006). The prayer-report arithmetic errors mean the figures the report displays are wrong regardless of input validity.

### Reproduction

```bash
curl -H "Authorization: Bearer $ADMIN_TOKEN" \
  "https://jaberah-new.tryasp.net/api/reports/best-students-report?year=2026&month=13"            # 500
curl -H "Authorization: Bearer $ADMIN_TOKEN" \
  "https://jaberah-new.tryasp.net/api/teachers-salaries/for-month?year=99999&month=1"             # 500
curl -H "Authorization: Bearer $TEACHER_TOKEN" \
  "https://jaberah-new.tryasp.net/api/prayers/monthly-report?date=2026-09-01&daysInMonth=999999999"  # 500
```

### Recommended Remediation

Validate ranges explicitly — `if (month is < 1 or > 12 || year is < 2000 or > 2100) return BadRequest(...)` — and derive `daysInMonth` server-side from `DateTime.DaysInMonth(date.Year, date.Month)` rather than accepting it from the client. Add `[Range]` attributes to the DTO properties. Delete the unused `daysInMonth` locals in `ReportsController` (`:278`, `:351`) and the duplicated `AverageCommitmentPercentage` block, and fix the `missedPrayers` unit mismatch. **Exploitable and observable today.** — *CWE-20, CWE-1284, CWE-248*

---

## [SEC-014] Unauthenticated 500 on the version endpoint via unparseable version strings

- **Category:** Security · **Severity:** Low · **Confidence:** High
- **Location:** `Jaberah/Controllers/VersionsController.cs:15-37, 78-91`

### Description & Evidence

`GET /api/versions` is `[AllowAnonymous]` and passes the caller-supplied `version` string straight to `int.Parse` on each dot-separated segment:

```csharp
// Jaberah/Controllers/VersionsController.cs:79-82
private int CompareVersions(string currentVersion, string requiredVersion)
{
    var currentParts  = currentVersion.Split('.').Select(int.Parse).ToArray();   // FormatException / OverflowException
    var requiredParts = requiredVersion.Split('.').Select(int.Parse).ToArray();
```

Any non-numeric segment throws `FormatException`; a segment exceeding `int.MaxValue` throws `OverflowException`. Both escape to `GlobalExceptionMiddleware` as a 500. The parameter is implicitly required (non-nullable reference type under `[ApiController]`), so omitting it correctly yields 400 — it is malformed-but-present input that fails.

### Impact

Anyone on the internet can force a 500 on the API's most-called endpoint, with no authentication. Each occurrence appends a log line (PERF-006), so this is the cheapest available lever for driving the log file toward its 1 GiB silent-stop threshold. It also constructs and destroys an `HttpClient` per attempt (PERF-002), making it the cheapest lever for socket exhaustion too.

### Reproduction

```bash
curl -i "https://jaberah-new.tryasp.net/api/versions?version=abc"          # 500
curl -i "https://jaberah-new.tryasp.net/api/versions?version=99999999999"  # 500 (overflow)
curl -i "https://jaberah-new.tryasp.net/api/versions?version="             # 500 (empty segment)
```

### Recommended Remediation

Use `System.Version.TryParse` (which handles the whole comparison correctly, including differing segment counts) and return 400 on failure:

```csharp
if (!System.Version.TryParse(version, out var client)) return BadRequest(new { message = "invalid version" });
if (!System.Version.TryParse(appVersion.MinRequiredVersion, out var minRequired)) return StatusCode(500);
bool isUpdateRequired = client < minRequired;
```

Then delete `CompareVersions` entirely. **Exploitable and observable today, without authentication.** — *CWE-20, CWE-248, OWASP API4:2023*

---

## [SEC-015] A teacher can mark their own salary paid using the same fields the administrator relies on

- **Category:** Security · **Severity:** Low · **Confidence:** High
- **Location:** `Jaberah/Controllers/TeachersSalariesController.cs:131-146`

### Description & Evidence

```csharp
// Jaberah/Controllers/TeachersSalariesController.cs:131-145
[HttpPatch("my-salaries/{salaryId}/mark-as-paid")]
public async Task<IActionResult> MarkAsPaid([FromRoute] int salaryId)
{
    var teacherId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    ...
    var result = await _db.TeacherSalaries.Where(ts => ts.Id == salaryId && ts.TeacherId == teacherId).FirstOrDefaultAsync();
    if (result == null) return BadRequest(new { message = "لايوجد راتب" });

    result.PaidAt = DateTime.Now;      // note: DateTime.Now, not the codebase's UtcNow.AddHours(3) convention
    result.IsPaid = true;
```

The ownership scoping here is **correct** — `ts.TeacherId == teacherId` means a teacher can only touch their own row. The issue is that the payee writes the same `IsPaid`/`PaidAt` columns that the administrator writes at `:82-101` and reads at `:52-54`. There is no separate "acknowledged by recipient" field, so the two meanings are conflated in one flag, and the transition is one-way — nothing can un-mark a salary.

### Impact

The financial record loses its meaning: an admin viewing `GET /api/teachers-salaries/for-month` cannot distinguish "the mosque paid this" from "the teacher pressed the button". A teacher can also mark a salary paid before receiving it (accidentally or otherwise), permanently, with no reversal path and no record of who set the flag. The incentive runs against the teacher, so deliberate abuse is unlikely — this is a bookkeeping-integrity defect rather than a fraud vector.

`DateTime.Now` on line 141 is also inconsistent with the rest of the codebase, which uses `DateTime.UtcNow.AddHours(3)`; on a UTC-configured host these differ by three hours, so the recorded receipt time is wrong.

### Reproduction

```bash
curl -H "Authorization: Bearer $TEACHER_TOKEN" "https://jaberah-new.tryasp.net/api/teachers-salaries/my-salaries?year=2026"
curl -X PATCH -H "Authorization: Bearer $TEACHER_TOKEN" \
  "https://jaberah-new.tryasp.net/api/teachers-salaries/my-salaries/<id>/mark-as-paid"
# Then read the admin view: IsPaid is now true with no admin action.
```

### Note on the production client's biometric gate

`main-v2` adds a biometric confirmation in front of this call, which is a genuine improvement to the *intent* signal — but it is not a control on the API:

```dart
// Jaberah-Flutter/lib/controllers/user/mySalaryController.dart:84-96 (main-v2)
final canUseBiometrics = await biometric.canUseBiometrics();
if (canUseBiometrics) {
  final isAuthenticated = await biometric.authenticate(reason: '...');
  if (!isAuthenticated) { messageSnackBar("فشل التحقق من البصمة..."); return; }
}
await markAsPaid(id);
```

Two reasons it does not change this finding's severity or remediation: the gate is skipped entirely when the device has no enrolled biometrics (`if (canUseBiometrics)`), and it lives in the client, so `PATCH /api/teachers-salaries/my-salaries/{id}/mark-as-paid` remains directly callable with nothing but a bearer token. The server-side fix below is still the one that matters.

### Recommended Remediation

Separate the two facts: add `AcknowledgedByTeacherAt` to `TeacherSalary` and have `MarkAsPaid` write only that, leaving `IsPaid`/`PaidAt` under admin control. Show both columns in the admin view so a discrepancy is visible. Replace `DateTime.Now` with `JaberahDBContext.GetCurrentDateTime()`, which already encapsulates the project's convention. **Observable today.** — *CWE-284: Improper Access Control (business-logic variant)*

---

## [SEC-016] No HSTS or security response headers; Swagger UI is mounted unconditionally at the site root

- **Category:** Security · **Severity:** Low · **Confidence:** Medium
- **Location:** `Jaberah/Program.cs:144-181` (Swagger at 145-149 and again at 170-181; `UseHttpsRedirection` at 159; no `UseHsts`)

### Description & Evidence

Swagger is registered twice — once correctly guarded by the environment check, and once unconditionally at the end of the pipeline, mapped to the site root with authorization persistence enabled:

```csharp
// Jaberah/Program.cs:145-149
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
...
// :170-181 — no environment guard
app.UseSwagger().UseSwaggerUI(sw =>
{
    sw.SwaggerEndpoint("/swagger/v1/swagger.json", "Jaberah API");
    sw.RoutePrefix = string.Empty;                  // ← served at "/"
    ...
    sw.ConfigObject.PersistAuthorization = true;
});
```

Anonymous access is nonetheless blocked, but only as a side effect of middleware ordering: these calls sit *after* `app.UseAuthorization()` (line 162), and the fallback policy (`:85-91`) requires an authenticated user. Because no endpoint matches `/` or `/swagger/v1/swagger.json`, `AuthorizationMiddleware` applies the fallback policy and returns 401. This is the same mechanism the code's own comments rely on for the Hangfire dashboard (`Jaberah/Jobs/HangfireAuthorizationFilter.cs:10-15`).

Separately, `app.UseHsts()` is absent, and no middleware sets `X-Content-Type-Options`, `X-Frame-Options`/`frame-ancestors`, `Referrer-Policy` or a CSP. A grep for `UseHsts|X-Frame-Options|Content-Security-Policy|AddAntiforgery` across the solution returns nothing.

### Impact

Low, for two reasons. The API serves JSON to a mobile client, so header-based browser protections have limited bearing — though Swagger UI *is* an HTML page served from this origin, which makes a CSP and `X-Frame-Options` relevant to it. And the API surface Swagger discloses is already public in `Jaberah-Flutter/lib/api/URLs.dart`, so the marginal disclosure to an authenticated teacher is minimal. The real concern is **fragility**: the only thing preventing anonymous exposure of the full API contract at the site root is the relative order of two `Use*` calls. Moving `UseSwagger()` above `UseAuthorization()` — a natural-looking tidy-up — would silently publish it.

Missing HSTS means the first request to `http://jaberah-new.tryasp.net` is redirected rather than refused, leaving a one-request stripping window. The redirect is configured (`:159`) but the policy is not pinned.

### Reproduction

```bash
curl -i "https://jaberah-new.tryasp.net/"                        # expect 401 (fallback policy)
curl -i "https://jaberah-new.tryasp.net/swagger/v1/swagger.json" # expect 401
curl -i -H "Authorization: Bearer $TEACHER_TOKEN" "https://jaberah-new.tryasp.net/swagger/v1/swagger.json"  # expect 200
curl -sI "https://jaberah-new.tryasp.net/api/versions?version=2.0.1" | grep -i "strict-transport\|x-content-type"  # absent
```

Confidence is Medium specifically on the anonymous behaviour: the fallback-policy-without-endpoint reasoning is documented ASP.NET Core behaviour and is what the existing Hangfire comments depend on, but it is worth confirming with the first two curls above before deciding this is Low rather than High.

### Recommended Remediation

Delete the unconditional block at `:170-181` and keep only the development-guarded registration, or guard it with an `[Authorize]`-equivalent and a non-root `RoutePrefix`. Add `app.UseHsts()` (production only) and a small header middleware setting `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, and `Content-Security-Policy: frame-ancestors 'none'`. **Verify before triaging** — if the first curl returns 200, this is a High-severity anonymous disclosure, not a Low one. — *CWE-16: Configuration, CWE-1032: OWASP A05:2021 Security Misconfiguration*

---

## [SEC-017] Access token stored unencrypted in `SharedPreferences` — RESOLVED on `main-v2`

- **Category:** Security · **Severity:** Low · **Confidence:** High
- **Status:** **Fixed on the production branch.** Retained for the record; excluded from the open-findings counts.
- **Location (as found on `master` @ `7eaf11d`):** `Jaberah-Flutter/lib/controllers/authController.dart:39-46` · read at `Jaberah-Flutter/lib/api/Dio.dart:32-37`
- **Fixed at:** `Jaberah-Flutter/lib/api/tokenStorage.dart` (`main-v2` @ `1bb83b0`)

> This finding came from the first pass, which reviewed `master`. It does not apply to production.

### Description & Evidence

```dart
// Jaberah-Flutter/lib/controllers/authController.dart:39-46
SharedPreferences prefs = await SharedPreferences.getInstance();
await prefs.setString('accessToken', data.accessToken);
...
await prefs.setString("phone", data.user.phoneNumber);
await prefs.setString("role", data.user.role.toString());
```

`SharedPreferences` maps to an unencrypted XML file in the app's private data directory. On a non-rooted device with `allowBackup="false"` (which is correctly set, `AndroidManifest.xml:19`) this is reasonably protected by the app sandbox; on a rooted or compromised device, or via a forged build inheriting the data directory (SEC-004), it is plain text.

The stored `role` is used for client-side navigation only (`checkLoginStatus:146-148`); tampering with it unlocks admin *screens* but not admin *data*, since `[IsAdmin]` is enforced server-side — except for the endpoints identified in SEC-002, which have no server-side check at all. That is a property of SEC-002, not of this storage choice.

### Impact

Combined with SEC-009 (tokens are non-revocable and self-renewing), a token extracted once yields indefinite access. That combination is what raises this above a routine hardening note.

### Reproduction

On a rooted device or emulator: `adb shell run-as com.example.jaberah cat /data/data/com.example.jaberah/shared_prefs/FlutterSharedPreferences.xml` — the token is readable in the clear.

### Resolution on `main-v2`

`main-v2` introduces `lib/api/tokenStorage.dart`, which is exactly the recommended fix:

```dart
// Jaberah-Flutter/lib/api/tokenStorage.dart:14-18, 40-50 (main-v2)
static const _key = 'accessToken';
static const FlutterSecureStorage _secure = FlutterSecureStorage(
  aOptions: AndroidOptions(encryptedSharedPreferences: true),
);
static Future<void> write(String token) async {
  if (await _writeSecure(token)) {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_key);          // no plaintext copy left behind
    return;
  }
  final prefs = await SharedPreferences.getInstance();
  await prefs.setString(_key, token);  // documented fallback
}
```

`read()` also migrates a legacy plaintext token into the encrypted store and deletes the old copy (`:28-37`), so upgrading devices are cleaned up rather than logged out, and `Dio.dart:42` reads through it. There is a deliberate fallback to `SharedPreferences` when the platform keystore throws, with a comment explaining the trade-off (a corrupt Keystore after an OS upgrade would otherwise sign the user out). That is a reasonable call and leaves the affected device no worse than before, but it does mean the plaintext path still exists on devices where the keystore fails; if you want to close it completely, treat a keystore failure as "re-authenticate" instead of "store in the clear". Nothing further is required for this finding.

The pairing recommendation still stands on its own merits: combined with the shortened access-token lifetime in SEC-009, the window of value for a stolen token shrinks from 7 days to minutes. — *OWASP MASVS-STORAGE-1; CWE-312: Cleartext Storage of Sensitive Information*

---

## [SEC-018] Request data written to standard output via leftover `Console.WriteLine` calls

- **Category:** Security · **Severity:** Low · **Confidence:** High
- **Location:** `Jaberah/Controllers/ExamsController.cs:78, 136` · also `Jaberah/SeedData/DataSeeder.cs:71, 96, 126`

### Description & Evidence

```csharp
// Jaberah/Controllers/ExamsController.cs:78 — inside UpsertMidFinalExam, after the DB writes are staged
Console.WriteLine(grade.Grade);
await _db.SaveChangesAsync();
```

```csharp
// Jaberah/Controllers/ExamsController.cs:136 — inside UpdatePartialExam
Console.WriteLine(dto.Rate);
var partialExam = await _db.PartialExams.FindAsync(dto.Id);
```

### Impact

Low. The values logged are a grade and a rating string, not credentials, and they go to stdout rather than to the HTTP-readable log file — so they land in the IIS/host process log, outside the redaction that `RequestResponseLoggingMiddleware:69-91` applies to the application log. The concern is the pattern rather than these two values: `Console.WriteLine` bypasses Serilog entirely, so it is unstructured, unredacted, unrotated and invisible to the logging configuration. It is also a synchronous write on the request path.

### Reproduction

Call `POST /api/exams/mid-final-exam` or `PUT /api/exams/partial-exam` and inspect the host's stdout log (`stdout` capture in `web.config`, or the MonsterASP log viewer).

### Recommended Remediation

Delete both lines. If the information is wanted, use the injected `ILogger<ExamsController>` at debug level so it participates in Serilog's configuration and level filtering. The `DataSeeder` calls are acceptable — that code is a disabled, manually invoked utility — but would be better as `ILogger` too. — *CWE-532: Insertion of Sensitive Information into Log File*

---

## [PERF-009] Non-sargable `LIKE '%%'` scans on every list and search endpoint

- **Category:** Performance · **Severity:** Low · **Confidence:** High
- **Location:** `Jaberah/Controllers/StudentsController.cs:28` · `Jaberah/Controllers/TeachersController.cs:29` · `Jaberah/Controllers/GroupsController.cs:119` · `Jaberah/Controllers/FollowStudentsController.cs:214`

### Description & Evidence

`searchText` defaults to the empty string and is applied unconditionally:

```csharp
// Jaberah/Controllers/StudentsController.cs:26-28
public async Task<IActionResult> GetStudents([FromQuery] string searchText = "", ...)
{
    var query = _db.Students.AsNoTracking().Where(x => x.Name.Contains(searchText)).AsQueryable();
```

`Contains` on a parameter translates to a leading-wildcard predicate, which cannot use the `UQ_Students_Name` / `UQ_Teachers_Name` unique indexes (`JaberahDBContext.cs:229, 247`) for a seek. Every request — including the common case where no search term was entered — performs an index or table scan.

`CleaningLogsController:207-208` and `PrayersController:72-73` show the correct pattern, applying the predicate only when the term is non-empty.

### Impact

Negligible at 122 students and 10 teachers; the scan is a handful of pages. It scales linearly with row count and executes twice per request (once for the page, once for `CountAsync()`, per PERF-005), so it is a latent cost that grows with retained history rather than a present problem.

### Reproduction

Capture the generated SQL for `GET /api/students` with no parameters: the `WHERE` clause is present with an empty parameter value, and the plan shows a scan rather than a seek.

### Recommended Remediation

Guard the predicate — `if (!string.IsNullOrWhiteSpace(searchText)) query = query.Where(x => x.Name.Contains(searchText));` — matching `CleaningLogsController:207`. If prefix search is acceptable UX, `StartsWith` is sargable and will seek the existing unique index. For substring search at scale, a SQL Server full-text index on `Students.Name` would be the durable answer. — *CWE-1049-adjacent; index-usage antipattern*

---

## [PERF-010] Login eagerly loads the teacher's groups and never uses them

- **Category:** Performance · **Severity:** Low · **Confidence:** High
- **Location:** `Jaberah/Controllers/AuthController.cs:23-24, 46-52`

### Description & Evidence

```csharp
// Jaberah/Controllers/AuthController.cs:23-24
var teacher = await _db.Teachers.Include(x => x.Groups)
    .FirstOrDefaultAsync(t => t.Name == model.Username.Trim());
```

The response is built from four scalar properties only:

```csharp
// :46-52
var userData = new AuthTeacher
{
    Id = teacher.Id,
    TeacherName = teacher.Name,
    PhoneNumber = teacher.PhoneNumber,
    Role = teacher.Role,
};
```

`teacher.Groups` is never read. The `Include` adds a `LEFT JOIN` to `Groups` and materialises every group entity into the change tracker, which then participates in the `SaveChangesAsync` at line 44 (that call is needed, for `FCMToken` and `LastLogin`).

### Impact

Small and bounded — a handful of rows per login — but it is on the authentication path, and it loads tracked entities that the subsequent `SaveChanges` must scan for changes.

### Reproduction

Capture the SQL for `POST /api/auth/login`: a join against `Groups` is present.

### Recommended Remediation

Delete `.Include(x => x.Groups)`. The entity must remain tracked (lines 42-44 mutate it), so a projection is not appropriate here — removing the `Include` is the whole fix. — *unnecessary data fetching*

---

## [PERF-011] The Hangfire dashboard filter blocks a thread-pool thread on every poll

- **Category:** Performance · **Severity:** Low · **Confidence:** High
- **Location:** `Jaberah/Jobs/HangfireAuthorizationFilter.cs:28-32`

### Description & Evidence

```csharp
// Jaberah/Jobs/HangfireAuthorizationFilter.cs:28-32
// IDashboardAuthorizationFilter متزامن ولا يوفّر نسخة async، فلا مفرّ من
// الانتظار هنا. اللوحة صفحة إدارية نادرة الاستخدام فالأثر مقبول.
UserViewModel? user = tokenHelper.VerifyToken(token).GetAwaiter().GetResult();
```

`VerifyToken` performs a database query, so this is sync-over-async on an I/O-bound call — the classic thread-pool starvation pattern.

### Impact

The accompanying comment's judgement is sound and this is genuinely Low: the dashboard is admin-only and rarely used. The qualification worth recording is that the Hangfire dashboard **auto-polls** `/hangfire/stats` roughly every two seconds while open, so "rarely used" means "rarely open" rather than "rarely requested" — an open dashboard blocks a thread every two seconds for the duration.

### Reproduction

Open `/hangfire` with a valid admin bearer token and observe repeated `stats` requests, each blocking a thread on a DB round trip.

### Recommended Remediation

Hangfire 1.8 provides `IDashboardAsyncAuthorizationFilter`; implement that interface instead and `await` the verification. Alternatively, since the fallback policy has already authenticated the caller before this filter runs, read the role from `httpContext.User` claims (which requires no I/O at all) — this depends on the `Role` claim being added to the token, as recommended in SEC-009 and PERF-008. — *CWE-1049-adjacent; sync-over-async antipattern*

---

## [PERF-012] The nightly absence job performs an in-memory O(n×m) anti-join

- **Category:** Performance · **Severity:** Low · **Confidence:** High
- **Location:** `Jaberah/Jobs/AttendancesJob.cs:26-45`

### Description & Evidence

```csharp
// Jaberah/Jobs/AttendancesJob.cs:26-41
var allTeacherGroups = await _db.Teachers
    .SelectMany(teacher => teacher.Groups, (teacher, group) => new { TeacherId = teacher.Id, GroupId = group.Id })
    .ToListAsync();                                       // no AsNoTracking

var presentTeacherGroups = await _db.TeacherAttendances
    .Where(a => a.Date == today)
    .Select(a => new { a.TeacherId, a.GroupId })
    .ToListAsync();

var absentTeacherGroups = allTeacherGroups
    .Where(tg => !presentTeacherGroups
        .Any(p => p.TeacherId == tg.TeacherId && p.GroupId == tg.GroupId))   // O(n × m) linear scan per element
    .ToList();
```

### Impact

Negligible today (10 teachers × ~10 groups against at most the same number of attendance rows). The job runs once nightly at 23:59, so even a poor constant factor is invisible. Recorded for completeness because the pattern is quadratic and both collections grow with the institution.

The job's correctness is fine, and notably it is idempotent: a second run finds the newly inserted `Absent` rows in `presentTeacherGroups` (which selects all statuses, not just present ones) and no-ops, so a Hangfire retry cannot double-insert against the unique index on `(TeacherId, GroupId, Date)`.

### Reproduction

Not observable at current scale; visible in a profiler with a synthetic dataset of a few thousand teacher-group pairs.

### Recommended Remediation

Either build a `HashSet<(int, int)>` from `presentTeacherGroups` and test membership in O(1), or push the anti-join into SQL:

```csharp
var absentTeacherGroups = await _db.Teachers
    .SelectMany(t => t.Groups, (t, g) => new { TeacherId = t.Id, GroupId = g.Id })
    .Where(tg => !_db.TeacherAttendances.Any(a => a.Date == today && a.TeacherId == tg.TeacherId && a.GroupId == tg.GroupId))
    .AsNoTracking()
    .ToListAsync();
```

Add `AsNoTracking()` to the first query regardless — the entities are only read. — *CWE-407: Inefficient Algorithmic Complexity*

---

## Potential Risks / Further Investigation

Items below are **not** presented as confirmed findings. Each is either unverifiable from the repository alone or requires a runtime observation this audit could not make. They are recorded so the team can close them out with a quick check.

### 1. `GET /api/exams/partial-exam/{id}` probably returns 500 on a JSON serialization cycle

`Jaberah/Controllers/ExamsController.cs:226-235` returns a `PartialExam` entity with `Include(e => e.Student)`. `Student` declares an inverse `ICollection<PartialExam> PartialExams` (`Jaberah/Models/JaberahModels/Students.cs:15`) and the relationship is configured with `WithMany(s => s.PartialExams)` (`JaberahDBContext.cs:81-84`). EF Core performs navigation fix-up for included navigations even under `AsNoTracking`, which would populate `Student.PartialExams` with the loaded exam and create a cycle that `System.Text.Json` rejects with "A possible object cycle was detected". **Check:** call the endpoint once with a valid id. If it 500s, the fix is to project to `GetStudentsPartialExams` (which already exists) rather than returning the entity, and to configure `ReferenceHandler.IgnoreCycles` as a backstop. Returning entities directly also over-discloses — the `Student` graph carries `PhoneNumber` and `Notes` that this screen does not need.

### 2. Concurrent check-in produces a 500 rather than a 409

`Jaberah/Controllers/TeachersAttendancesController.cs:300-338` reads-then-inserts without handling the unique index on `(TeacherId, GroupId, Date)`. Two simultaneous taps would both find `existing == null` and the second insert would violate the index. The window is small and the trigger requires genuine concurrency, so this was not reproducible by inspection. `CleaningLogsController.cs:334-342` already contains the correct handler to copy. Same shape in `ExamsController.AddPartialExam:92-123` (unique index `UQ_PartialExams_StudentId_ExamDate`) and `FollowStudentsController.UpsertAttendanceAndBehavior:349-366` (`UQ_StudentAttendance_StudentId_Date`).

### 3. `DeleteGroup` orphans its students instead of detaching them

```csharp
// Jaberah/Controllers/GroupsController.cs:252-257
var group = await _db.Groups.FindAsync(groupId);
if (group == null) return NotFound(...);
group.Students = null;          // navigation was never loaded — this is a no-op
_db.SoftDelete(group);
```

`FindAsync` does not load the `Students` collection, so assigning `null` changes nothing and EF detects no modification. Students therefore retain a `GroupId` pointing at a soft-deleted group: they will not appear in `GET /api/students?withoutGroup=true` (which tests `!x.GroupId.HasValue`, `StudentsController.cs:29`) and so cannot be found for reassignment. `DeleteStudent` handles the mirror case correctly by setting `student.GroupId = null` explicitly (`StudentsController.cs:140`). **Check:** delete a group with students, then query `?withoutGroup=true`. The fix is `await _db.Students.Where(s => s.GroupId == groupId).ExecuteUpdateAsync(s => s.SetProperty(x => x.GroupId, (int?)null));` before the soft delete.

### 4. RESOLVED on `main-v2` — the Flutter release pipeline was not sending the deploy key

```yaml
# Jaberah-Flutter/.github/workflows/flutter-build.yml:41-46
- name: Update Version in Backend
  run: |
    response=$(curl -X PUT "${{ secrets.BACKEND_API }}?version=${{ env.VERSION }}" \
      -H "Content-Type: multipart/form-data" \
      -F "apkFile=@build/app/outputs/flutter-apk/jaberah-${VERSION}.apk")
    echo "Response from backend: $response"
```

The snippet above is from `master`. `PUT /api/versions` is guarded by `RequireDeployKeyAttribute`, which requires an `X-Deploy-Key` header matching the `DeployKey` setting and returns 401 without it (`Jaberah/Middlewares/RequireDeployKey.cs:30-36`) — or 503 if no key is configured (`:24-28`), and `master`'s workflow sends no such header.

**On `main-v2` this is already fixed.** The workflow now has separate publish steps per branch, each sending the key, and gained `Analyze` and `Test` steps:

```yaml
# Jaberah-Flutter/.github/workflows/flutter-build.yml:62-70 (main-v2)
- name: Update Version in Backend (MAIN)
  if: github.ref == 'refs/heads/main-v2'
  run: |
    response=$(curl -X PUT "${{ secrets.BACKEND_API_V2 }}?version=${{ env.VERSION }}" \
      -H "X-Deploy-Key: ${{ secrets.DEPLOY_KEY }}" \
      -H "Content-Type: multipart/form-data" \
      -F "apkFile=@build/app/outputs/flutter-apk/jaberah-${VERSION}.apk")
    echo "Response from backend: $response"
```

One residual item, worth 30 seconds: `curl` still runs without `--fail`, so a 401, a 503 or a 500 from the backend leaves the step green and the failure visible only in the echoed body. Add `--fail-with-body` so a publish failure actually fails the run. Confirm `DEPLOY_KEY` and `BACKEND_API_V2` are set as repository secrets and that `BACKEND_API_V2` points at the production host.

### 5. RESOLVED on `main-v2` — the monthly-report client/server contract did not match

On `master`, `lib/controllers/admin/monthlyReportController.dart:34` and `lib/controllers/user/monthlyStudentsReports.dart:31` both requested `?groupId=…&year=…&month=…`, while the action signature is `GetMonthlyReport(int groupId, DateTime fromDate, DateTime toDate, int? take)` (`ReportsController.cs:101`) — so `year`/`month` bound to nothing and the guard at `:103-104` returned 400. That was migration drift from the older `JaberahApp-Server` (TypeScript, private), which is still referenced as `server_node` in `URLs.dart:3`.

**On `main-v2` both call sites send `fromDate`/`toDate`** (`admin/monthlyReportController.dart:80`, `user/monthlyStudentsReports.dart:62`), matching the action. Nothing to do. It never affected SEC-003 or PERF-001, which concern hand-crafted requests.

### 6. Dependency currency could not be verified

No .NET SDK was available in the audit environment, so `dotnet list package --vulnerable --include-transitive` could not be run. The pinned versions in `Jaberah/Jaberah.csproj:9-35` are all release versions with no *known* advisories at the time of writing, but the Microsoft packages are pinned at `9.0.0` — the initial .NET 9 release — and several patch releases have shipped since. **Check:** run `dotnet list package --vulnerable --include-transitive` and `dotnet list package --outdated` in CI, and add a scheduled Dependabot or `dotnet-outdated` job. On the Flutter side, `pubspec.yaml` declares most dependencies with no version constraint at all (`http:`, `dio:`, `get:`, `firebase_core:`), which would be a reproducibility problem except that `pubspec.lock` **is** committed and `flutter pub get` honours it — so builds are pinned in practice. Adding explicit caret constraints would still make the intent legible.

### 7. Firebase Android API key in `google-services.json`

`Jaberah-Flutter/android/app/google-services.json` contains `"current_key": "AIzaSy…"`. This is expected and unavoidable — Firebase Android API keys are designed to ship inside the app and are not secrets; they identify the project rather than authorising privileged access. **This is not reported as a finding.** What cannot be assessed from this repository is whether the Firebase project's security rules and App Check configuration are appropriately restrictive, since neither lives here. **Check:** confirm App Check is enabled and that no Firebase product with permissive rules (Firestore, Storage, Realtime Database) is reachable with this key. The Firebase *service account* key — the one that would matter — is correctly kept out of the repository (`Jaberah/.gitignore:16-17`, and the file path is read from configuration at `Program.cs:75-83`).

### 8. Committed build artifacts

125 files under `Jaberah-Flutter/android/app/.cxx/` (CMake configuration output from a May 2025 build) are tracked. No security impact; it inflates every clone and produces noisy diffs. Add `android/app/.cxx/` to `.gitignore` and `git rm -r --cached` it.

### 9. Client-side robustness notes (Flutter) — re-verified on `main-v2`

`main-v2` fixed several items in this group on its own: request timeouts are now configured (`lib/api/Dio.dart:18-19`), concurrent 401s are serialised behind a refresh lock instead of stampeding (`:33-34, 63-95`), logout clears the cookie jar and the encrypted store (`:172-183`), and a non-JSON error body no longer crashes the app (`apiErrorMessage`, covered by `test/api_error_message_test.dart`). What remains:

- **`CookieJar()` is still in-memory** (`lib/api/Dio.dart:21` on `main-v2`), not `PersistCookieJar`, so the `refreshToken` cookie is discarded when the app process ends. The refresh flow can therefore only succeed within a single session; after a restart with an expired access token, `_refreshToken()` returns null and `_logout()` sends the user to the login screen. Given SEC-009 recommends shortening the access-token lifetime, this needs fixing first or the UX will regress badly. Use `PersistCookieJar` with a directory from `path_provider`.
- **`searchText` is still interpolated into URLs unencoded** (`studentsController.dart:52`, `teachersController.dart:49`, `groupStudentsController.dart:60` on `main-v2`). A name containing `&`, `#` or `+` will corrupt the client's own query string. Use Dio's `queryParameters` option rather than string interpolation — the two new cleaning-log controllers already use `Uri.encodeQueryComponent`, so the pattern exists in the codebase and just needs applying consistently.
- **Two `Dio` instances are still constructed per retry** (`lib/api/Dio.dart:108, 138` on `main-v2`), each with its own connection pool, on every 401. Reuse the single configured client.

### 10. Timezone handling is consistent but fragile

`DateTime.UtcNow.AddHours(3)` appears in 14 places to approximate Arabia Standard Time (`JaberahDBContext.cs:508`, `AuthController.cs:43`, and elsewhere), while `RequestResponseLoggingMiddleware.cs:55` applies it **twice** (`DateTime.UtcNow.AddHours(3).AddHours(3)`), making every log timestamp six hours off, and `TeachersSalariesController.cs:141` uses `DateTime.Now` instead. Arabia Standard Time has no DST so the offset itself is correct, but the pattern is unenforced. Centralise on `JaberahDBContext.GetCurrentDateTime()` (which already exists) or, better, store `DateTimeOffset`/UTC and convert at the presentation layer. This is a correctness/observability concern rather than a security or performance one, so it is not filed as a finding; the doubled offset in the logging middleware is worth a one-line fix regardless.

---

## Recommended Remediation Plan

Ordered by urgency rather than by severity label — a few Low-severity items sit early because they are one-line changes that close off amplification paths.

### Immediate (within 24–48 hours)

These address active exposure. Steps 1 and 2 should not wait on a release cycle.

| # | Action | Findings addressed | Effort |
|---|---|---|---|
| 1 | **Rotate all 10 teacher passwords** to individually generated random values, delivered out of band. Do this **before** touching the repository, so rotation is not signalled by a commit. | SEC-001 | 1 h |
| 2 | **Make both repositories private**, then purge `SeedData/Teachers.json` and `SeedData/Students.json` from working tree and history and request cache expiry from GitHub Support. Treat the data as already copied. | SEC-001 | 2–3 h |
| 3 | **Add ownership checks** to the 12 endpoints listed in SEC-002, and role-gate `monthly-report`'s all-groups path (admins institution-wide, teachers scoped to their own circles — *not* `groupId` required, which would break the admin screen). This is the single highest-value code change in this report. | SEC-002, SEC-003 | 1 day |
| 4 | **Add the missing short-circuit block** to `UpdateTeacherValidation.cs` and fix the `&&` → single-value length test. Four lines. | SEC-005 | 15 min |
| 5 | **Bound the report date ranges** (≤62 days for monthly, ≤1 semester for semester) and set a `CommandTimeout`. | PERF-001 | 2 h |
| 6 | **Add `AddRateLimiter` / `UseRateLimiter`** with a strict policy on `/api/auth/*` and a loose global policy. | SEC-006 | 2 h |
| 7 | **Replace `AddScoped<HttpClient>` with `AddHttpClient<DropboxService>`.** One-line registration change; removes the socket leak from the hottest endpoint. | PERF-002 | 30 min |
| 8 | **Fix the ASCII/UTF-8 key encoding mismatch** at `VerifyToken.cs:77`. One word. Prevents a total-outage footgun on the next key rotation. | SEC-010 | 5 min |
| 9 | **Add rolling and retention to the Serilog file sink.** Prevents logging from silently dying at 1 GiB while the other fixes land. | PERF-006 | 15 min |

### Short Term (2–4 weeks)

| # | Action | Findings addressed | Effort |
|---|---|---|---|
| 10 | **Generate a release keystore, change the package name off `com.example.*`, wire signing into CI**, and publish an APK SHA-256 that the client verifies. | SEC-004 | 1 day |
| 11 | **Add `[IsAdmin]` at controller level as the default** and remove it only from actions with an explicit ownership check, so a forgotten attribute fails closed. Extend `Jaberah.Tests` with a `cannot_touch_another_groups_student` case per module. | SEC-002 (structural) | 2 days |
| 12 | **Add authorization to the three book endpoints** and switch `DeleteBook` to a soft delete. | SEC-008 | 2 h |
| 13 | **Introduce token types, refresh-token rotation with a revocation store, and a `SecurityStamp`** on `Teacher`; shorten the access token to hours. Fix the `AddHours(3)` expiry arithmetic. Switch the client to `PersistCookieJar` at the same time — with a short access token, the in-memory jar (Risk #9) would force a re-login on every app restart. | SEC-009, Risk #9 | 3 days |
| 14 | **Clamp pagination inside `PagedList`/`PaginationDTO`** and at the four inline sites. | PERF-005 | 3 h |
| 15 | **Validate `year`/`month`/`daysInMonth`; derive `daysInMonth` server-side.** Fix the `missedPrayers` unit mismatch and remove the duplicated `AverageCommitmentPercentage`. | SEC-013 | 4 h |
| 16 | **Replace `CompareVersions` with `System.Version.TryParse`** and return 400 on malformed input. | SEC-014 | 30 min |
| 17 | **Add validation and duplicate-key handling** to the prayer and exam upsert paths; clamp the mid-final grade to 40 with a DB `CHECK` constraint. | SEC-011, Risk #2 | 1 day |
| 18 | **Scope teacher check-in/check-out to the caller's own groups**, unify identity access on `this.CurrentUser()`, and add 409 handling for the race. | SEC-007 | 4 h |
| 19 | **Move report PDFs to app-private storage**; drop the three storage permissions, `requestLegacyExternalStorage` and `usesCleartextTraffic`. | SEC-012 | 1 day |
| 20 | **Stop buffering responses** in the logging middleware; bound `EnableBuffering`; put `GlobalExceptionMiddleware` outermost. | PERF-003 | 4 h |
| 21 | **Fix the `allPrayers` cache key** (or drop the pagination); centralise cache invalidation across the three controllers. | PERF-004, PERF-007 | 4 h |
| 22 | **Add `--fail-with-body`** to the Flutter workflow's publish steps so a rejected publish fails the run (the `X-Deploy-Key` header is already present on `main-v2`); confirm `BACKEND_API_V2` points at the production host. | Risk #4 | 15 min |
| 23 | **Port the `main-v2` client fixes to `master`** if that branch and its `jaberah.runasp.net` deployment are still in use — secure token storage, the deploy-key header, the report contract and the non-JSON error handling all landed only on `main-v2`. If `master` is dead, delete the branch and its workflow triggers so it cannot be published from by accident. | branch hygiene | 2 h |

### Long Term (1–3 months)

| # | Action | Findings addressed | Effort |
|---|---|---|---|
| 24 | **Add actor columns** (`CreatedByTeacherId`, `UpdatedByTeacherId`) to the six academic tables, and an append-only audit log for grade and salary changes. Without this, past tampering under SEC-002 remains unattributable. | SEC-002 (forensics) | 1 week |
| 25 | **Collapse the two authentication mechanisms into one.** Put `Role` and `SecurityStamp` in the token, delete the duplicate `ValidateToken`, and cache or eliminate the per-request user query. Return 401 for authentication failures so the client's refresh interceptor works. | PERF-008, SEC-010 | 3 days |
| 26 | **Introduce a service layer** between controllers and `DbContext`, with authorization decisions expressed once per aggregate rather than per action. The recurring shape of SEC-002, SEC-007 and SEC-008 is that authorization lives in attributes on 60+ individual actions; that does not scale and will regress. | structural | 2 weeks |
| 27 | **Add `dotnet list package --vulnerable` and `flutter pub outdated` to CI**, plus Dependabot; pin Flutter dependency constraints explicitly. | Risk #6 | 1 day |
| 28 | **Add HSTS and security headers; remove the unconditional Swagger registration.** Verify anonymous access to `/` returns 401 first. | SEC-016 | 3 h |
| 29 | **Replace `DateTime.UtcNow.AddHours(3)` with a single time abstraction** (`TimeProvider` and `DateTimeOffset`), and fix the doubled offset in the logging middleware. | Risk #10 | 3 days |
| 30 | **Add integration tests** covering the authorization matrix (teacher vs. admin × each endpoint). `Jaberah.Tests` already has an in-memory `TestDatabase` harness to build on; the gap is coverage, not infrastructure. | regression prevention | 1 week |
| 31 | **Migrate reference-data caches to short expirations or change tokens**, and set a `SizeLimit` on `IMemoryCache`. | PERF-004, PERF-007 | 1 day |
| 32 | **Consider a data-retention policy.** Nothing ages out `SaveLessons`, `ReviewLessons`, `StudentAttendances` or `StudentPrayerAttendances`; all the report queries are unbounded in history, so PERF-001's severity grows every term. | PERF-001 (long-run) | 1 week |

---

## Appendix

### Audited Areas

| Area | Coverage | Notes |
|---|---|---|
| Authentication (login, refresh, token issue/verify) | Full | `AuthController`, `TokenHelper`, `VerifyTokenAttribute`, JwtBearer configuration |
| Authorization (role checks, object-level ownership) | Full | All 60+ actions across 12 controllers enumerated and their attributes tabulated |
| Input validation | Full | All 10 validation filters, all 14 DTO files, all `[FromQuery]`/`[FromRoute]`/`[FromBody]` bindings |
| Injection (SQL, NoSQL, command, template, LDAP) | Full | **None found.** Only raw SQL is `SeedData/DataSeeder.cs`, fully parameterised with `{0}`-style placeholders, no string interpolation into SQL anywhere (`grep 'ExecuteSqlRawAsync($\|FromSqlRaw($'` returns nothing), and unreachable from HTTP. All other data access is EF Core LINQ. No `Process.Start`, no template engine, no shell invocation. |
| Secrets management | Full | `appsettings*.json` and Firebase service-account files correctly git-ignored (`.gitignore:11-17`); full commit-history sweep for `private_key`, `client_email`, `Server=`, `TokenKey`, `DeployKey` found only documentation placeholders. The exposure in SEC-001 is via seed fixtures, not config. |
| Multi-tenant / data isolation | Full | Single-tenant application; the isolation boundary that matters is teacher-to-teacher, covered by SEC-002, SEC-007, SEC-008 |
| CORS and security headers | Full | CORS allow-list is correct and fails closed on empty configuration (`Program.cs:38-56`); headers covered in SEC-016 |
| Session / cookie / JWT security | Full | Cookie flags are correct (`HttpOnly`, `Secure`, `SameSite=Strict`, matching `Path`); token design covered in SEC-009, SEC-010 |
| Rate limiting and abuse prevention | Full | Absent — SEC-006 |
| Error handling / information leakage | Full | `GlobalExceptionMiddleware` correctly withholds exception detail in production (`Helpers/GlobalException.cs:39-50`); the residual issue is the *number* of reachable unhandled exceptions (SEC-011, SEC-013, SEC-014) |
| File upload / download | Full | APK upload is deploy-key gated, size-limited and streamed rather than buffered (`DropboxService.cs:35-47`); no user-controlled file paths, no path traversal surface — the only file paths in the API are the two constants in `LogsController` |
| SSRF | Full | **None found.** All outbound HTTP targets are hard-coded Dropbox and Firebase hostnames; no user input reaches a URL. The one caller-influenced value returned to a client is `Version.URL`, written only by the deploy-key-gated endpoint. |
| XSS / CSRF / prototype pollution | Full | Not applicable in substance: the API returns JSON to a native client and serves no user-generated HTML; `SameSite=Strict` plus a bearer-token scheme leaves no CSRF surface. Prototype pollution is not a C#/Dart concern. |
| Race conditions | Full | Three read-then-insert races identified (Risk #2); the cleaning-logs module already handles its own correctly |
| Database schema, indexes, migrations | Full | `JaberahDBContext.OnModelCreating` reviewed in full. Indexing is genuinely good: composite indexes on every `(StudentId, Date)` access path, unique constraints where the domain requires them, a filtered unique index correctly accounting for soft deletes (`:454-457`), and `CHECK` constraints on salary. Migrations are git-ignored so history could not be reviewed. |
| Background jobs | Full | `RecurringJobs`, `AttendanceJobService`, `HangfireAuthorizationFilter` — dashboard authorization is correct; job is idempotent |
| External integrations | Full | Firebase (topic + token messaging), Dropbox (OAuth refresh, upload, share link) |
| CI/CD | Full | Both workflows; deployment credentials correctly in GitHub secrets; findings in SEC-004 and Risk #4 |
| Mobile client | Full | All 73 Dart files; Android manifest, Gradle configuration, `google-services.json` |
| Performance: N+1 queries | Full | **Server: none found in the classic sense** — the report controllers deliberately use single-round-trip projections with correlated subqueries, which the code comments call out ("Single query — pull everything needed per student in one round-trip"). **Client: none found** — no per-item API calls. The performance issues here are unbounded result sets and post-materialisation filtering, not N+1. |
| Performance: caching | Full | Six cache keys reviewed; PERF-004, PERF-007 |
| Performance: pagination | Full | PERF-005; note that the pagination `ORDER BY` bugs referenced in the code comments were already fixed before this audit |
| Performance: resource leaks | Full | PERF-002 (sockets), PERF-003 (memory), PERF-006 (disk) |

### Files Reviewed

**`Jaberah-ASP` — all 109 `.cs` files, 5 `.json`, 1 `.csproj`, 1 `.yml`, `.gitignore`, 1 `.pubxml`.** Read in full:

```
Jaberah/Program.cs
Jaberah/Controllers/    AuthController.cs, StudentsController.cs, TeachersController.cs,
                        GroupsController.cs, ReportsController.cs, ExamsController.cs,
                        FollowStudentsController.cs, PrayersController.cs, CleaningLogsController.cs,
                        NotificationsController.cs, TeachersAttendancesController.cs,
                        TeachersSalariesController.cs, LogsController.cs, VersionsController.cs
Jaberah/Middlewares/    VerifyToken.cs, isAdmin.cs, RequireDeployKey.cs,
                        RequestResponseLoggingMiddleware.cs
Jaberah/Helpers/        GlobalException.cs, CurrentUserExtensions.cs, StringExtensions.cs,
                        PagedList.cs, PaginationDTO.cs, AutoMapper.cs, DropboxService.cs,
                        FirebaseService.cs
Jaberah/Models/         MyDbContext/JaberahDBContext.cs, all 18 JaberahModels/*.cs,
                        all 12 DTOs/*.cs, ViewModels/UserViewModel.cs (+ ViewModels surveyed)
Jaberah/Validations/    all 10 filters across Teachers/, Students/, Groups/,
                        CleaningLogs/, TeachersSalaries/
Jaberah/Jobs/           RecurringJobs.cs, AttendancesJob.cs, HangfireAuthorizationFilter.cs
Jaberah/SeedData/       DataSeeder.cs, Teachers.json, Students.json, Groups.json
Jaberah.Tests/          AuthorizationFilterTests.cs, CleaningLogRulesTests.cs,
                        CurrentUserExtensionsTests.cs, RecurringJobsTests.cs, TestDatabase.cs
.github/workflows/main.yml, Jaberah/Jaberah.csproj, .gitignore,
Jaberah/Properties/launchSettings.json, PublishProfiles/site14114-WebDeploy.pubxml
```

**`Jaberah-Flutter` — reviewed on `master` @ `7eaf11d` in the first pass, then re-verified against the production branch `main-v2` @ `1bb83b0` in revision 2.** `master` is a strict ancestor of `main-v2` (23 commits behind, nothing unique), so the first pass covered a subset of production code; revision 2 diffed the two branches in full (102 files, +9,238/−3,153) and re-checked every client-side finding, the Android and CI configuration, `lib/api/`, and the six controllers new to `main-v2` (`user/cleaningLogController.dart`, `user/dailyPrayersController.dart`, `user/mySalaryController.dart`, `user/prayersStudentsReportsController.dart`, `admin/cleaningLogReportController.dart`, `admin/prayersMonthlyReportController.dart`, `admin/monthlyPartialExamReportController.dart`). The new screens consume endpoints already covered by SEC-002 and SEC-011 and introduced no finding of their own. Files read in full (paths as on `master`; re-read on `main-v2` where changed):

```
lib/api/            Dio.dart, URLs.dart
lib/controllers/    authController.dart, versionsController.dart, exportedReportsPageController.dart,
                    admin/monthlyReportController.dart, admin/semesterReportController.dart,
                    admin/bestStudentsController.dart, admin/studentsPartialExamsController.dart,
                    admin/studentsController.dart, admin/teachersController.dart,
                    admin/groupStudentsController.dart, admin/notificationsAdminController.dart,
                    user/monthlyStudentsReports.dart, user/semesterStudentsReportsController.dart
                    (+ remaining 20 controllers surveyed for API-call and pagination patterns)
lib/pages/          surveyed (30 files) — no security logic; UI only
android/            app/build.gradle, app/src/main/AndroidManifest.xml,
                    app/google-services.json, app/src/debug|profile/AndroidManifest.xml
.github/workflows/flutter-build.yml, pubspec.yaml, pubspec.lock, .gitignore
```

**Additional evidence gathered outside the working tree:**

- `git log --all -p` sweep of both repositories for credential-shaped strings (see Secrets management, above).
- `git log --all --diff-filter=A --name-only` to enumerate every file ever added, checking for deleted-but-historical secrets.
- GitHub REST API query confirming repository visibility for `Jaberah-ASP`, `Jaberah-Flutter` and `JaberahApp-Server`.

### Limitations

This audit was static. The following could not be established from the repositories and are called out where they affect a finding's confidence:

1. **No running instance was tested.** No request was sent to `https://jaberah-new.tryasp.net`. Every reproduction in this report is derived from source and is presented as a procedure to run, not as an observed result. Findings marked "Confidence: High" are high because the defect is unambiguous in code, not because it was executed.
2. **No database access.** Row counts, index usage, actual query plans and current log-file size are unknown. This is why PERF-001, PERF-006 and PERF-009 state asymptotic behaviour rather than measured timings, and why no finding claims a specific latency figure.
3. **No .NET SDK in the audit environment**, so `dotnet build`, `dotnet test` and `dotnet list package --vulnerable` could not be run. Dependency-vulnerability status is therefore recorded under Potential Risks (#6) rather than asserted. No claim in this report depends on compilation.
4. **EF Core migrations are git-ignored** (`.gitignore:29-30`), so the deployed schema was inferred from `OnModelCreating` and may have drifted from it. Index and constraint claims describe the model configuration.
5. **`appsettings*.json` is git-ignored,** so the deployed `TokenKey`, `DeployKey`, connection string, Dropbox credentials and CORS allow-list were not seen. SEC-010's impact assessment is explicitly conditional on the deployed key's character set, and SEC-016's severity depends on a runtime check.
6. **No mobile device or emulator.** SEC-004, SEC-012 and SEC-017 are derived from manifest and Gradle configuration; the reproduction steps were not executed.
7. **The Firebase project configuration is out of scope** — security rules and App Check settings live in the Firebase console, not in either repository (Potential Risk #7).
8. **The older `MohamedSaeed-dev/JaberahApp-Server`** (TypeScript, private, last pushed November 2024) is referenced by `lib/api/URLs.dart:3` as `server_node` but is not the active backend. It was not audited.

11. **Two live deployments exist and only one was in scope.** `URLs.dart` on `main-v2` defines both `server_asp = "https://jaberah.runasp.net/api"` and `newServerASP = "https://jaberah-new.tryasp.net/api"`, with `baseUrl = newServerASP`. Both branch pairs deploy independently — `master` → Server 1, `main-v2` → Server 2 (`Jaberah-ASP/.github/workflows/main.yml:41-59`). This audit covers the `main-v2` pair. Whether Server 1 is still running, still reachable, and still holding a copy of the same production data was not determined; if it is, every server-side finding applies there too, against an older and less-hardened client.

12. **Revision 2 was a differential re-verification, not a fresh audit of `main-v2`.** The server-side findings were unaffected (identical commit), and the client-side findings were each re-checked against the new code. The ~9,200 added client lines were reviewed for security-relevant behaviour — API calls, storage, permissions, authorization assumptions — not line by line for correctness.
9. **UI code was surveyed rather than read line by line.** The 30 files under `lib/pages/` were checked for API calls, storage writes and permission requests; presentation logic was not reviewed for correctness.
10. **No load testing.** Concurrency findings (Risk #2) and resource-exhaustion findings (PERF-001, PERF-002) describe mechanisms verified in code; the thresholds at which they become visible were not measured.

---

## Remediation Priority Matrix

Priority is a function of severity, exploitability and effort — a one-line fix for a Low-severity amplifier can outrank a Medium-severity refactor. **P0** = immediate, **P1** = this sprint, **P2** = next sprint, **P3** = backlog.

| ID | Severity | Category | Impact | Effort | Priority |
|---|---|---|---|---|---|
| SEC-001 | Critical | Security | Probable admin compromise + irreversible PII exposure of 122 minors | High (rotate + purge + notify) | **P0** |
| SEC-002 | Critical | Security | Every teacher can read/write all students' grades and attendance, unattributably | High (12 endpoints) | **P0** |
| PERF-001 | Critical | Performance | Self-service DoS; unbounded memory and DB scan | Low (add range check) | **P0** |
| SEC-005 | High | Security | No password policy; 1-char passwords accepted; 500 on a plausible request | Trivial (4 lines) | **P0** |
| SEC-010 | Medium | Security | Next key rotation with a non-ASCII key = total auth outage | Trivial (1 word) | **P0** |
| PERF-002 | High | Performance | Socket exhaustion on the most-called endpoint | Trivial (1 registration) | **P0** |
| SEC-003 | High | Security | Any teacher reaches the admin-only institution-wide export | Low (role-gate the path) | **P0** |
| SEC-006 | High | Security | Unthrottled credential guessing against 10 known accounts | Low (built-in limiter) | **P0** |
| PERF-006 | Medium | Performance | Logging silently stops at 1 GiB; O(file) read per admin request | Low (sink options) | **P0** |
| SEC-004 | High | Security | Trojanised APK installs over the real app as an update | Medium (keystore + CI) | **P1** |
| SEC-007 | High | Security | Fabricated attendance against unowned groups; corrupts payroll evidence | Low | **P1** |
| SEC-011 | Medium | Security | Unbounded grades corrupt reports; trivial 500s | Low | **P1** |
| SEC-013 | Low | Security | Trivial unauthenticated-adjacent 500s on four endpoints | Low | **P1** |
| SEC-014 | Low | Security | **Unauthenticated** 500 on the hottest endpoint; log/socket amplifier | Trivial | **P1** |
| PERF-005 | Medium | Performance | Pagination disableable by caller; two easy 500s | Low (clamp in helper) | **P1** |
| PERF-004 | Medium | Performance | Any user can poison a shared cache for 30 days | Trivial | **P1** |
| SEC-008 | Medium | Security | Any teacher hard-deletes any group's books | Low | **P1** |
| Risk #4 | — | Reliability | *Resolved on `main-v2`*; residual: `curl` lacks `--fail-with-body` | Trivial | **P3** |
| SEC-009 | Medium | Security | One stolen token = indefinite, unrevocable access | High (token store) | **P2** |
| SEC-012 | Medium | Security | Student grade reports readable by any app on the device | Medium | **P2** |
| PERF-003 | Medium | Performance | ~3× peak memory on every response; 100 MB temp files | Medium | **P2** |
| PERF-007 | Medium | Performance | Admin sees stale group assignments for up to 7 days | Low | **P2** |
| PERF-008 | Medium | Performance | Double JWT validation + one extra DB query per request | Medium | **P2** |
| SEC-016 | Low | Security | API contract exposed to any teacher; anonymous exposure one reorder away | Low | **P2** |
| ~~SEC-017~~ | Low | Security | *Resolved on `main-v2`* — token moved to the encrypted store | — | **Done** |
| Risk #1 | — | Correctness | `partial-exam/{id}` likely 500s on a serialization cycle | Low | **P2** |
| Risk #9 | — | Reliability | In-memory cookie jar breaks refresh across app restarts | Low | **P2** |
| Risk #3 | — | Correctness | Deleting a group orphans its students permanently | Low | **P2** |
| SEC-015 | Low | Security | Payee can set the admin's "paid" flag; no reversal | Low | **P3** |
| SEC-018 | Low | Security | Request data on stdout, outside Serilog and its redaction | Trivial | **P3** |
| PERF-009 | Low | Performance | `LIKE '%%'` scans on every list request; grows with data | Trivial | **P3** |
| PERF-010 | Low | Performance | Unused eager load on the login path | Trivial | **P3** |
| PERF-011 | Low | Performance | Thread blocked per Hangfire dashboard poll | Low | **P3** |
| PERF-012 | Low | Performance | O(n×m) anti-join in the nightly job | Trivial | **P3** |
| Risk #2 | — | Correctness | Concurrent writes 500 instead of 409 in three places | Low | **P3** |
| Risk #6 | — | Security | Dependency vulnerability status unverified | Low | **P3** |
| Risk #8, #9, #10 | — | Hygiene | Build artifacts committed; client robustness; timezone handling | Low | **P3** |

### Closing note

The engineering foundations here are better than the finding count suggests. The data model is well indexed, soft deletion is applied consistently through a global query filter, the report queries were deliberately written as single round trips, secrets are correctly kept out of configuration, the CORS policy fails closed, error responses do not leak exception detail in production, and the cleaning-logs module — the newest code in the repository — has both correct object-level authorization and unit tests asserting it.

The problem is that those good properties are *local*. Authorization is expressed as attributes on individual actions, so it is present exactly where someone remembered to add it and absent everywhere else; caching invalidation is expressed as three hand-maintained key lists, so it is correct in one controller and incomplete in two; validation is expressed as one filter per DTO, so nine are wired up correctly and the tenth silently does nothing. Fixing the 29 open findings in this report matters, but item 26 of the long-term plan matters more: until authorization and validation are enforced in one place per aggregate rather than repeated per endpoint, this class of finding will keep returning with each new feature.

*End of report.*
