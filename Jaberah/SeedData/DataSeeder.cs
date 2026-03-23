using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Jaberah.Models.MyDbContext;

/// <summary>
/// Seeds Teachers → Groups → Students from JSON files while preserving original IDs.
/// JSON files must be placed in a "SeedData" folder next to the executable, or you
/// can adjust <see cref="SeedDataPath"/> to any absolute path.
///
/// Call from Program.cs (after app.Build()):
///   await DataSeeder.SeedAsync(app.Services);
/// </summary>
public static class DataSeeder
{
    // ── Adjust this path if your JSON files live elsewhere ──────────────────
    private static string SeedDataPath =>
        Path.Combine(AppContext.BaseDirectory, "SeedData");

    private static object? N(object? value) => value;

    // ────────────────────────────────────────────────────────────────────────

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JaberahDBContext>();

        // Nothing to do if Teachers are already present
        //if (await context.Teachers.AnyAsync())
        //    return;

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            await SeedTeachersAsync(context);
            await SeedGroupsAsync(context);
            await SeedStudentsAsync(context);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ── Teachers ─────────────────────────────────────────────────────────────

    private static async Task SeedTeachersAsync(JaberahDBContext context)
    {
        var teachers = LoadJson<TeacherSeed>("Teachers.json");
        if (teachers.Count == 0) return;

        await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.Teachers ON");

        foreach (var t in teachers)
        {
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO dbo.Teachers
                    (Id, Name, PhoneNumber, [Password], Role,
                     FCMToken, LastLogin, CreatedAt, DeletedAt, UpdatedAt)
                VALUES
                    ({0}, {1}, {2}, {3}, {4},
                     {5}, {6}, {7}, {8}, {9})",
                t.Id, t.TeacherName, t.PhoneNumber, t.Password, t.Role,
t.FCMToken, N(t.LastLogin), N(t.CreatedAt), N(t.DeletedAt), N(t.UpdatedAt));
        }

        await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.Teachers OFF");
        Console.WriteLine($"[Seeder] ✔ Inserted {teachers.Count} teachers.");
    }

    // ── Groups ────────────────────────────────────────────────────────────────

    private static async Task SeedGroupsAsync(JaberahDBContext context)
    {
        var groups = LoadJson<GroupSeed>("Groups.json");
        if (groups.Count == 0) return;

        await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.Groups ON");

        foreach (var g in groups)
        {
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO dbo.Groups
                    (Id, Name, Period, TeacherId, CreatedAt, DeletedAt, UpdatedAt)
                VALUES
                    ({0}, {1}, {2}, {3}, {4}, {5}, {6})",
               g.Id, g.GroupName, g.Period, g.TeacherId,
N(g.CreatedAt), N(g.DeletedAt), N(g.UpdatedAt));
        }

        await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.Groups OFF");
        Console.WriteLine($"[Seeder] ✔ Inserted {groups.Count} groups.");
    }

    // ── Students ──────────────────────────────────────────────────────────────

    private static async Task SeedStudentsAsync(JaberahDBContext context)
    {
        var students = LoadJson<StudentSeed>("Students.json");
        if (students.Count == 0) return;

        await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.Students ON");

        foreach (var s in students)
        {
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO dbo.Students
                    (Id, Name, PhoneNumber, SchoolClass, MemoRate, SchoolLevel,
                     Notes, GroupId, StudyLevel, CreatedAt, DeletedAt, UpdatedAt)
                VALUES
                    ({0}, {1}, {2}, {3}, {4}, {5},
                     {6}, {7}, {8}, {9}, {10}, {11})",
               s.Id, s.StudentName, s.PhoneNumber, s.SchoolClass, s.MemoRate, s.SchoolLevel,
s.Notes,
N(s.GroupId),
N(s.StudyLevel),
N(s.CreatedAt),
N(s.DeletedAt),
N(s.UpdatedAt));
        }

        await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.Students OFF");
        Console.WriteLine($"[Seeder] ✔ Inserted {students.Count} students.");
    }

    // ── JSON helper ──────────────────────────────────────────────────────────

    // ── JSON helper ──────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new SpaceDateTimeConverter() }  // ← added
    };

    private static List<T> LoadJson<T>(string fileName)
    {
        var path = Path.Combine(SeedDataPath, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"[Seeder] Seed file not found: {path}");

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<T>>(json, _jsonOptions) ?? []; // ← uses _jsonOptions
    }

    // ── Custom converter: handles "yyyy-MM-dd HH:mm:ss.fffffff" ─────────────

    private sealed class SpaceDateTimeConverter : System.Text.Json.Serialization.JsonConverter<DateTime?>
    {
        private static readonly string[] _formats =
        [
            "yyyy-MM-dd HH:mm:ss.fffffff",
        "yyyy-MM-dd HH:mm:ss.ffffff",
        "yyyy-MM-dd HH:mm:ss.fffff",
        "yyyy-MM-dd HH:mm:ss.ffff",
        "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-dd HH:mm:ss.ff",
        "yyyy-MM-dd HH:mm:ss.f",
        "yyyy-MM-dd HH:mm:ss",
    ];

        public override bool HandleNull => true;

        public override DateTime? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            var raw = reader.GetString();
            if (string.IsNullOrWhiteSpace(raw)) return null;

            if (DateTime.TryParseExact(raw, _formats,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt))
                return dt;

            return DateTime.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
            => writer.WriteStringValue(value?.ToString("yyyy-MM-dd HH:mm:ss.fffffff"));
    }

    // ── Seed DTOs (mirrors JSON shape) ───────────────────────────────────────

    private sealed record TeacherSeed(
    int Id, string TeacherName, string PhoneNumber, string Password, int Role,
    string? FCMToken,
    DateTime? LastLogin,
    DateTime? CreatedAt,   // ← was DateTime
    DateTime? DeletedAt,
    DateTime? UpdatedAt);  // ← was DateTime

    private sealed record GroupSeed(
        int Id, string GroupName, int Period, int TeacherId,
        DateTime? CreatedAt,   // ← was DateTime
        DateTime? DeletedAt,
        DateTime? UpdatedAt);  // ← was DateTime

    private sealed record StudentSeed(
        int Id, string StudentName, string PhoneNumber,
        string? SchoolClass, int MemoRate, string? SchoolLevel,
        string? Notes, int? GroupId, string? StudyLevel,
        DateTime? CreatedAt,   // ← was DateTime
        DateTime? DeletedAt,
        DateTime? UpdatedAt);  // ← was DateTime
}
