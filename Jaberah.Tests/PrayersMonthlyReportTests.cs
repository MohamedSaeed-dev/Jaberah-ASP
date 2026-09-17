using Jaberah.Controllers;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.ViewModels.Prayers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Jaberah.Tests;

/// <summary>
/// كشف الصلوات الشهري: الطلاب يُرتَّبون تنازليًا حسب عدد الصلوات المؤداة،
/// والترتيب يسبق التصفيح فتحمل الصفحة الأولى أكثرهم صلاةً لا أصغرهم معرّفًا.
/// </summary>
public class PrayersMonthlyReportTests
{
    private static readonly DateOnly MonthStart = new(2026, 8, 1);
    private const int DaysInMonth = 31;

    // الصلوات المزروعة: الفجر ٢، الظهر ٤، العصر ٤، المغرب ٣، العشاء ٤.
    private const int Fajr = 1, Dhuhr = 2, Asr = 3, Maghrib = 4, Isha = 5;

    private static PrayersController ControllerFor(TestDatabase database) =>
        new(database.Db, new MemoryCache(new MemoryCacheOptions()));

    private static QueryMonthlyPrayersReportDTO Query(int pageNumber = 1, int pageSize = 10) => new()
    {
        Date = MonthStart,
        DaysInMonth = DaysInMonth,
        PageNumber = pageNumber,
        PageSize = pageSize,
    };

    private static StudentPrayerAttendance Prayed(int studentId, int prayerId, byte rakats, int dayOfMonth = 1, bool inGroup = false) => new()
    {
        StudentId = studentId,
        PrayerId = prayerId,
        PrayerDate = MonthStart.AddDays(dayOfMonth - 1),
        RakatsCount = rakats,
        IsInGroup = inGroup,
    };

    /// <summary>
    /// أربعة طلاب رُتّبت معرّفاتهم عكس حصيلتهم عمدًا: المتوقَّع ٤ ثم ١ ثم ٣ ثم ٢.
    /// الطالبان ١ و٣ متساويان في عدد الصلوات ويفرّق بينهما عدد الركعات.
    /// </summary>
    private static void SeedMonth(TestDatabase database)
    {
        database.Db.Students.AddRange(
            new Student { Id = 1, Name = "ثلاث صلوات باثنتي عشرة ركعة", PhoneNumber = "710000001" },
            new Student { Id = 2, Name = "صلاة واحدة في الشهر", PhoneNumber = "710000002" },
            new Student { Id = 3, Name = "ثلاث صلوات بست ركعات", PhoneNumber = "710000003" },
            new Student { Id = 4, Name = "خمس صلوات", PhoneNumber = "710000004" });

        database.Db.StudentPrayerAttendances.AddRange(
            // الطالب ٤: خمس صلوات — أكثرهم.
            Prayed(4, Fajr, 2), Prayed(4, Dhuhr, 4), Prayed(4, Asr, 4), Prayed(4, Maghrib, 3), Prayed(4, Isha, 4),

            // الطالب ١: ثلاث صلوات، اثنتا عشرة ركعة.
            Prayed(1, Dhuhr, 4), Prayed(1, Asr, 4), Prayed(1, Isha, 4),

            // الطالب ٣: ثلاث صلوات، ست ركعات — يلي الطالب ١ عند تساوي عدد الصلوات.
            Prayed(3, Fajr, 2), Prayed(3, Maghrib, 3), Prayed(3, Asr, 1),

            // الطالب ٢: صلاة واحدة داخل الشهر، وصلاة غير مؤداة لا تُحتسب.
            Prayed(2, Fajr, 2), Prayed(2, Dhuhr, 0));

        // خمس صلوات للطالب ٢ بعد نهاية الشهر: خارج المدى فلا ترفعه إلى الصدارة.
        database.Db.StudentPrayerAttendances.AddRange(
            Prayed(2, Fajr, 2, dayOfMonth: DaysInMonth + 1),
            Prayed(2, Dhuhr, 4, dayOfMonth: DaysInMonth + 1),
            Prayed(2, Asr, 4, dayOfMonth: DaysInMonth + 1),
            Prayed(2, Maghrib, 3, dayOfMonth: DaysInMonth + 1),
            Prayed(2, Isha, 4, dayOfMonth: DaysInMonth + 1));

        database.Db.SaveChanges();
        database.Db.ChangeTracker.Clear();
    }

    private static PrayersMonthlyReportDTO ReportOf(IActionResult result) =>
        Assert.IsType<PrayersMonthlyReportDTO>(Assert.IsType<OkObjectResult>(result).Value);

    [Fact]
    public async Task Students_are_ordered_by_the_most_prayers_prayed()
    {
        using var database = new TestDatabase();
        SeedMonth(database);
        var controller = ControllerFor(database);

        var report = ReportOf(await controller.GetMonthlyPrayersReport(Query()));

        Assert.Equal([4, 1, 3, 2], report.Students.Select(s => s.StudentId));
        Assert.Equal([5, 3, 3, 1], report.Students.Select(s => s.TotalPrayed));
    }

    [Fact]
    public async Task Equal_prayer_counts_are_broken_by_the_rakats_prayed()
    {
        using var database = new TestDatabase();
        SeedMonth(database);
        var controller = ControllerFor(database);

        var report = ReportOf(await controller.GetMonthlyPrayersReport(Query()));

        var tied = report.Students.Where(s => s.TotalPrayed == 3).ToList();
        Assert.Equal([1, 3], tied.Select(s => s.StudentId));
        Assert.Equal([12, 6], tied.Select(s => s.TotalPrayedRakats));
    }

    [Fact]
    public async Task The_first_page_holds_the_top_students_not_the_smallest_ids()
    {
        using var database = new TestDatabase();
        SeedMonth(database);
        var controller = ControllerFor(database);

        var first = ReportOf(await controller.GetMonthlyPrayersReport(Query(pageNumber: 1, pageSize: 2)));
        var second = ReportOf(await controller.GetMonthlyPrayersReport(Query(pageNumber: 2, pageSize: 2)));

        // الترتيب كان يجري بعد الاقتطاع، فكانت الصفحة الأولى ١ و٢ لا ٤ و١.
        Assert.Equal([4, 1], first.Students.Select(s => s.StudentId));
        Assert.Equal([3, 2], second.Students.Select(s => s.StudentId));
    }

    [Fact]
    public async Task Prayers_outside_the_month_are_left_out_of_the_tally()
    {
        using var database = new TestDatabase();
        SeedMonth(database);
        var controller = ControllerFor(database);

        var report = ReportOf(await controller.GetMonthlyPrayersReport(Query()));

        var lastStudent = report.Students.Last();
        Assert.Equal(2, lastStudent.StudentId);
        Assert.Equal(1, lastStudent.TotalPrayed);
        Assert.Equal(2, lastStudent.TotalPrayedRakats);
    }

    [Fact]
    public async Task A_group_filter_keeps_the_same_descending_order()
    {
        using var database = new TestDatabase();
        SeedMonth(database);
        database.Db.Teachers.Add(new Teacher { Id = 1, Name = "معلم", PhoneNumber = "700000001", Password = "x", Role = Role.TEACHER });
        database.Db.Groups.Add(new Group { Id = 1, Name = "حلقة أ", TeacherId = 1, Period = Period.MORNING });
        database.Db.SaveChanges();

        foreach (var student in database.Db.Students.Where(s => s.Id == 1 || s.Id == 4))
            student.GroupId = 1;
        database.Db.SaveChanges();
        database.Db.ChangeTracker.Clear();

        var controller = ControllerFor(database);
        var query = Query();
        query.GroupId = 1;

        var report = ReportOf(await controller.GetMonthlyPrayersReport(query));

        Assert.Equal([4, 1], report.Students.Select(s => s.StudentId));
        Assert.Equal("حلقة أ", report.Students[0].GroupName);
    }

    [Fact]
    public async Task A_bad_date_is_rejected()
    {
        using var database = new TestDatabase();
        var controller = ControllerFor(database);

        var result = await controller.GetMonthlyPrayersReport(new QueryMonthlyPrayersReportDTO
        {
            Date = default,
            DaysInMonth = DaysInMonth,
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
