using Jaberah.Controllers;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Jaberah.Tests;

/// <summary>
/// قواعد كشف النظافة: مهمة واحدة لطالب واحد في اليوم على مستوى المسجد،
/// وحصر المعلم في حلقاته. تُستدعى الأفعال مباشرةً بهوية مركّبة في
/// <c>HttpContext.Items["User"]</c> كما يفعل <c>VerifyTokenAttribute</c>.
/// </summary>
public class CleaningLogRulesTests
{
    private static readonly DateOnly Today = new(2026, 8, 29);

    private static CleaningLogsController ControllerFor(TestDatabase database, int userId, Role role)
    {
        var controller = new CleaningLogsController(database.Db, new MemoryCache(new MemoryCacheOptions()));
        var httpContext = new DefaultHttpContext();
        httpContext.Items["User"] = new UserViewModel
        {
            Id = userId,
            Name = $"user-{userId}",
            PhoneNumber = "700000000",
            Role = role.ToString(),
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static UpsertDailyCleaningLogDTO Assign(int taskId, int? studentId, bool completed = false) => new()
    {
        Date = Today,
        Logs = [new CleaningLogUpdateDTO { CleaningTaskId = taskId, StudentId = studentId, IsCompleted = completed }],
    };

    private static string MessageOf(IActionResult result) =>
        result switch
        {
            ObjectResult objectResult => objectResult.Value?.GetType().GetProperty("message")?
                .GetValue(objectResult.Value)?.ToString() ?? string.Empty,
            _ => string.Empty,
        };

    [Fact]
    public async Task The_four_default_tasks_are_seeded()
    {
        using var database = new TestDatabase();
        var controller = ControllerFor(database, 2, Role.TEACHER);

        var result = Assert.IsType<OkObjectResult>(await controller.GetTasks());
        var tasks = Assert.IsAssignableFrom<System.Collections.IEnumerable>(result.Value).Cast<object>().ToList();

        Assert.Equal(4, tasks.Count);
        Assert.Equal(
            ["الممر", "الدرج", "الصف", "الصالة"],
            tasks.Select(t => t.GetType().GetProperty("NameAr")!.GetValue(t)!.ToString()));
    }

    [Fact]
    public async Task A_teacher_can_assign_a_student_from_their_own_group()
    {
        using var database = new TestDatabase();
        database.SeedTeachersAndStudents();
        var controller = ControllerFor(database, 2, Role.TEACHER);

        var result = await controller.UpsertDaily(Assign(taskId: 1, studentId: 1));

        Assert.IsType<OkObjectResult>(result);
        Assert.Single(database.Db.CleaningLogs.Where(l => l.Date == Today));
    }

    [Fact]
    public async Task A_teacher_cannot_assign_a_student_from_another_group()
    {
        using var database = new TestDatabase();
        database.SeedTeachersAndStudents();
        var controller = ControllerFor(database, 2, Role.TEACHER);

        // الطالب 2 في حلقة المعلم 3
        var result = await controller.UpsertDaily(Assign(taskId: 1, studentId: 2));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("لا يمكنك اسناد مهمة لطالب من حلقة اخرى", MessageOf(result));
        Assert.Empty(database.Db.CleaningLogs.Where(l => l.Date == Today));
    }

    [Fact]
    public async Task A_teacher_cannot_take_over_a_task_another_group_already_holds()
    {
        using var database = new TestDatabase();
        database.SeedTeachersAndStudents();

        await ControllerFor(database, 3, Role.TEACHER).UpsertDaily(Assign(taskId: 1, studentId: 2));
        database.Db.ChangeTracker.Clear();

        var result = await ControllerFor(database, 2, Role.TEACHER).UpsertDaily(Assign(taskId: 1, studentId: 1));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("المهمة مسندة لحلقة اخرى", MessageOf(result));
    }

    [Fact]
    public async Task One_student_may_hold_more_than_one_task_in_a_day()
    {
        using var database = new TestDatabase();
        database.SeedTeachersAndStudents();
        var controller = ControllerFor(database, 2, Role.TEACHER);

        var result = await controller.UpsertDaily(new UpsertDailyCleaningLogDTO
        {
            Date = Today,
            Logs =
            [
                new CleaningLogUpdateDTO { CleaningTaskId = 1, StudentId = 1 },
                new CleaningLogUpdateDTO { CleaningTaskId = 2, StudentId = 1 },
            ],
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(2, database.Db.CleaningLogs.Count(l => l.Date == Today && l.StudentId == 1));
    }

    [Fact]
    public async Task A_null_student_clears_the_assignment()
    {
        using var database = new TestDatabase();
        database.SeedTeachersAndStudents();

        await ControllerFor(database, 2, Role.TEACHER).UpsertDaily(Assign(taskId: 1, studentId: 1));
        database.Db.ChangeTracker.Clear();

        var result = await ControllerFor(database, 2, Role.TEACHER).UpsertDaily(Assign(taskId: 1, studentId: null));

        Assert.IsType<OkObjectResult>(result);
        Assert.Empty(database.Db.CleaningLogs.Where(l => l.Date == Today));
    }

    [Fact]
    public async Task A_cleared_task_can_be_reassigned_the_same_day()
    {
        // الفهرس الفريد مُرشَّح بـ DeletedAt IS NULL، فالحذف الناعم يجب أن يحرّر المهمة.
        using var database = new TestDatabase();
        database.SeedTeachersAndStudents();

        await ControllerFor(database, 2, Role.TEACHER).UpsertDaily(Assign(taskId: 1, studentId: 1));
        database.Db.ChangeTracker.Clear();
        await ControllerFor(database, 2, Role.TEACHER).UpsertDaily(Assign(taskId: 1, studentId: null));
        database.Db.ChangeTracker.Clear();

        var result = await ControllerFor(database, 2, Role.TEACHER).UpsertDaily(Assign(taskId: 1, studentId: 1));

        Assert.IsType<OkObjectResult>(result);
        Assert.Single(database.Db.CleaningLogs.Where(l => l.Date == Today));
    }

    [Fact]
    public async Task An_inactive_task_cannot_be_assigned()
    {
        using var database = new TestDatabase();
        database.SeedTeachersAndStudents();

        var task = database.Db.CleaningTasks.First(t => t.Id == 1);
        task.IsActive = false;
        database.Db.SaveChanges();
        database.Db.ChangeTracker.Clear();

        var result = await ControllerFor(database, 2, Role.TEACHER).UpsertDaily(Assign(taskId: 1, studentId: 1));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("توجد مهمة غير موجودة او معطلة", MessageOf(result));
    }

    [Fact]
    public async Task An_unknown_student_is_rejected()
    {
        using var database = new TestDatabase();
        database.SeedTeachersAndStudents();

        var result = await ControllerFor(database, 1, Role.ADMIN).UpsertDaily(Assign(taskId: 1, studentId: 999));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("لايوجد طالب", MessageOf(result));
    }

    [Fact]
    public async Task Assignable_students_are_the_callers_own_even_for_an_admin()
    {
        // شاشة كشف النظافة شاشة معلم، فنطاقها حلقات المستدعي مهما كان دوره —
        // كان استثناء المدير هنا يُظهر كل طلاب النظام.
        using var database = new TestDatabase();
        database.SeedTeachersAndStudents();

        var result = Assert.IsType<OkObjectResult>(
            await ControllerFor(database, 1, Role.ADMIN)
                .GetAssignableStudents(new QueryAssignableStudentsDTO { Date = Today }));

        var payload = result.Value!;
        var data = (System.Collections.IEnumerable)payload.GetType().GetProperty("Data")!.GetValue(payload)!;

        // المدير (id=1) لا يملك حلقات، فلا طلاب قابلين للإسناد.
        Assert.Empty(data.Cast<object>());
    }

    [Fact]
    public async Task An_admin_may_assign_across_groups()
    {
        using var database = new TestDatabase();
        database.SeedTeachersAndStudents();

        var result = await ControllerFor(database, 1, Role.ADMIN).UpsertDaily(new UpsertDailyCleaningLogDTO
        {
            Date = Today,
            Logs =
            [
                new CleaningLogUpdateDTO { CleaningTaskId = 1, StudentId = 1 },
                new CleaningLogUpdateDTO { CleaningTaskId = 2, StudentId = 2 },
            ],
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(2, database.Db.CleaningLogs.Count(l => l.Date == Today));
    }
}
