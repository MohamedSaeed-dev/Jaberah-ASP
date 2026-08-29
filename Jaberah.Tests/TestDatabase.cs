using Jaberah.Models.JaberahModels;
using Jaberah.Models.MyDbContext;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Jaberah.Tests;

/// <summary>
/// قاعدة SQLite في الذاكرة لكل اختبار: نفس تعيينات <see cref="JaberahDBContext"/>
/// الحقيقية (المرشّحات العامة، الفهارس الفريدة، البيانات الأولية) بلا خادم.
/// الاتصال يبقى مفتوحًا لأن قاعدة SQLite في الذاكرة تزول بإغلاق آخر اتصال.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public JaberahDBContext Db { get; }

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<JaberahDBContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new JaberahDBContext(options);
        Db.Database.EnsureCreated();
    }

    /// <summary>مدير، ومعلمان لكل واحد حلقة وطالب.</summary>
    public void SeedTeachersAndStudents()
    {
        Db.Teachers.AddRange(
            new Teacher { Id = 1, Name = "مدير النظام", PhoneNumber = "700000001", Password = "x", Role = Role.ADMIN },
            new Teacher { Id = 2, Name = "معلم أول", PhoneNumber = "700000002", Password = "x", Role = Role.TEACHER },
            new Teacher { Id = 3, Name = "معلم ثانٍ", PhoneNumber = "700000003", Password = "x", Role = Role.TEACHER });

        Db.Groups.AddRange(
            new Group { Id = 1, Name = "حلقة أ", TeacherId = 2, Period = Period.MORNING },
            new Group { Id = 2, Name = "حلقة ب", TeacherId = 3, Period = Period.EVENING });

        Db.Students.AddRange(
            new Student { Id = 1, Name = "طالب حلقة أ", PhoneNumber = "710000001", GroupId = 1 },
            new Student { Id = 2, Name = "طالب حلقة ب", PhoneNumber = "710000002", GroupId = 2 });

        Db.SaveChanges();
        Db.ChangeTracker.Clear();
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
