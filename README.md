# Jaberah API

[العربية](README.ar.md)

Backend for the Jaberah Mosque Circles app (حلقات مسجد جابرة). It manages
teachers, their circles (حلقات) and students, and records daily memorisation
and revision follow-up, prayers, exams, teacher attendance and salaries, and
the daily cleaning roster — then turns all of that into monthly and semester
reports for the administrator.

The only consumer of this API is the mobile app in
[Jaberah-Flutter](https://github.com/MohamedSaeed-dev/Jaberah-Flutter).
There is no web frontend and no other client.

## Roles

Two roles, stored in the `Role` column of the `Teachers` table. There is no
separate users table — an administrator is a teacher with a different role.

| Role | Value | Scope |
|---|---|---|
| `ADMIN` | 1 | Circles, students, teachers, salaries, partial exams, reports, trash, logs |
| `TEACHER` | 2 | Their own circles: daily follow-up, prayers, cleaning roster, own attendance, own salary, own reports |

A few endpoints are reached by both and carry a teacher id in the route
(`GET /api/teachers/{id}/groups`, `PUT /api/teachers/{id}`, and teacher
attendance). Those are scoped by identity rather than by role: an admin can
act on any teacher, anyone else only on themselves.

## Stack

ASP.NET Core 9 · EF Core 9 on SQL Server · AutoMapper · Hangfire · Serilog ·
Firebase Admin SDK for push · Dropbox for APK hosting · xUnit for tests.

## Layout

```
Jaberah/
  Controllers/        One per domain: Auth, Students, Groups, Teachers, Prayers,
                      FollowStudents, Exams, Reports, CleaningLogs, ...
  Models/
    JaberahModels/    EF entities (Student, Group, Teacher, CleaningLog, ...)
    DTOs/             Request bodies
    ViewModels/       Response shapes
    MyDbContext/      JaberahDBContext — every mapping and index in one file
  Middlewares/        VerifyToken, IsAdmin, RequireDeployKey, request logging
  Validations/        Input validation as action filters (see below)
  Helpers/            AutoMapper, Dropbox, Firebase, PagedList, exception handler
  Jobs/               The recurring Hangfire job and its dashboard filter
  SeedData/           Optional JSON seeding (disabled by default)
Jaberah.Tests/        xUnit against SQLite in memory
```

## Conventions that catch people out

**Deletes are soft.** Domain entities inherit `BaseEntity` (`Id`, `CreatedAt`,
`UpdatedAt`, `DeletedAt`), and `OnModelCreating` walks every type that inherits
it and attaches a `HasQueryFilter` hiding deleted rows. So `_db.Students` never
returns deleted students. To reach them — as the trash screen does — use
`.IgnoreQueryFilters()`. To delete, call `_db.SoftDelete(entity)`, not `Remove`.

The exceptions are the two reference tables, `Prayers` and `CleaningTasks`:
they do not inherit `BaseEntity`, they are seeded with `HasData` in the same
file, and they are never deleted — deactivation is an `IsActive` flag.

**Timestamps are Riyadh time, not UTC.** `JaberahDBContext.GetCurrentDateTime()`
returns `DateTime.UtcNow.AddHours(3)`, and that is what stamps `CreatedAt` and
`UpdatedAt` automatically in `SaveChangesAsync`. Read every timestamp in the
database as local time (+3).

**Validation is not DataAnnotations.** Each operation has its own attribute
under `Validations/` (`[AddStudent]`, `[UpdateTeacher]`, and so on) that
inspects the DTO and returns 400 with a `validationContent` array of
`{key, message}` pairs in Arabic, which the app displays verbatim. Follow the
same pattern for new endpoints rather than annotating DTOs.

**Two layers of authentication.** The `FallbackPolicy` in `Program.cs` requires
a valid JWT on every endpoint not marked `[AllowAnonymous]`. On top of that,
`[ServiceFilter(typeof(VerifyTokenAttribute))]` loads the teacher from the
database and puts them in `HttpContext.Items["User"]`, which `[IsAdmin]` and
the `CurrentUserExtensions` helpers depend on. A controller that needs to know
who is calling needs both.

**`Migrations/` is gitignored** and nothing is applied at startup, so a first
local run means generating and applying your own migration.

## Running locally

You need the .NET 9 SDK and SQL Server (LocalDB is enough).

`appsettings.json` is not in the repository. Create it under `Jaberah/`:

```json
{
  "ConnectionStrings": { "DB": "Server=(localdb)\\MSSQLLocalDB;Database=Jaberah;Trusted_Connection=True;" },
  "TokenKey": "a long random signing key",
  "DeployKey": "the APK publish key",
  "Cors": { "AllowedOrigins": [] },
  "FCM": { "ServiceAccountFilePath": "path to the Firebase service account file" },
  "Dropbox": { "clientId": "...", "clientSecret": "...", "refreshToken": "..." }
}
```

The Firebase service account file is gitignored too and is not part of the
deploy package — it is placed on the server by hand. Note that
`GoogleCredential.FromFile` only reads and parses the file at startup; it never
checks it against Google. A revoked or corrupted key starts up perfectly well
and then fails on the first push notification with
`invalid_grant: Invalid JWT Signature`. Transfer the file in binary mode — an
ASCII transfer mangles the newlines in `private_key` and produces exactly the
same error.

Then:

```bash
dotnet restore
dotnet tool restore              # dotnet-ef is a local tool under .config
dotnet ef migrations add Init -p Jaberah
dotnet ef database update -p Jaberah
dotnet run --project Jaberah
```

Swagger is served at the site **root** (`http://localhost:5291/`) because
`RoutePrefix` is set to an empty string. In Development it also answers on
`/swagger`, because the UI is registered twice in `Program.cs` — once inside
the `IsDevelopment` branch and once after it.

`SeedData/DataSeeder.cs` fills the database from the JSON files in `SeedData/`.
Its call in `Program.cs` is commented out; enable it only when you need it.

## Tests

```bash
dotnet test
```

`Jaberah.Tests` runs the real `JaberahDBContext` against SQLite in memory —
same mappings, indexes and query filters — covering the authorisation filters,
the cleaning roster rules, identity scoping, and Hangfire job registration.

## Scheduled work

Hangfire runs `MarkAbsentTeachersAsync` at 23:59 Riyadh time: it marks teachers
who never checked in as absent, and skips Fridays. Registration lives in
`Jobs/RecurringJobs.cs` and goes through `IRecurringJobManager` from DI — do not
use the static `RecurringJob` API here. It reads `JobStorage.Current`, which
`AddHangfire` does not set, and it will throw during startup.

The dashboard is at `/hangfire`, admin only.

## Logs

`Middlewares/RequestResponseLoggingMiddleware` writes only requests whose
response was **not** 2xx to `Logs/error-requests.log`, after redacting
sensitive fields (passwords, tokens, the deploy key) and skipping binary and
oversized payloads.

Read it through `GET /api/Logs` as an admin; `DELETE /api/Logs` clears it.

## Deployment

GitHub Actions to MonsterASP.NET, two separate servers:

| Branch | Target |
|---|---|
| `master` | Server 1 |
| `main-v2` | Server 2 — current production |

The order is build → test → publish → deploy. Tests run before publish on
purpose, so a failing test stops everything before the deploy package is even
produced.

The workflow also runs on pull requests, but both deploy steps are gated on
`github.ref` and therefore only fire on the two branches above.

### The APK deploy key

`PUT /api/versions` receives the APK from the Flutter release pipeline, uploads
it to Dropbox, and makes that link the official update URL for every user. The
pipeline has no JWT — it is not a user — so the endpoint is guarded by an
`X-Deploy-Key` header compared against the `DeployKey` setting using SHA-256
digests and a constant-time comparison.

The same value has to exist in two places: `DeployKey` in the server
configuration, and `DEPLOY_KEY` in the GitHub Secrets of the Flutter
repository. If it is not configured on the server the endpoint rejects every
upload with 503 — it fails closed, not open.

## Known rough edges

- `builder.Host.UseSerilog()` is called but never configured, so every `ILogger`
  call in the application goes nowhere — including the unhandled-exception
  logging in `GlobalException.cs`. The request log above is the only file that
  is actually written.
- The timestamp inside the request log adds three hours twice, so it runs six
  hours fast and is then labelled `Z` as if it were UTC.
- `AutoMapper 13.0.1` carries a security advisory
  ([GHSA-rvv3-g6hj-g44x](https://github.com/advisories/GHSA-rvv3-g6hj-g44x));
  moving to 14 is a deliberate breaking change.
- Nothing in CI actually starts the application, so startup failures only show
  up after a deploy.
