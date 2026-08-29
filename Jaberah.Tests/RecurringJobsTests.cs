using Hangfire;
using Hangfire.InMemory;
using Hangfire.Storage;
using Jaberah.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace Jaberah.Tests;

/// <summary>
/// انحدار: تسجيل المهمة الدورية كان يستعمل <c>RecurringJob</c> الساكن الذي يقرأ
/// <c>JobStorage.Current</c>. و<c>AddHangfire</c> لا يضبط ذلك المتغيّر، فكان النداء يعمل
/// بالصدفة لأن <c>app.UseHangfireDashboard()</c> مكررًا كان يسبقه ويُحَلّ التخزين نيابةً
/// عنه. حذف ذلك النداء — وهو ثغرة: لوحة بلا فلتر صلاحيات قبل المصادقة — أسقط الإقلاع.
///
/// هذه الاختبارات تُشغّل <see cref="RecurringJobs.Register"/> على حاوية لم يُحَلّ منها
/// التخزين قط، فتفشل لو عاد أحد إلى الـ API الساكن.
/// </summary>
public class RecurringJobsTests
{
    private sealed class FakeAttendanceJobService : IAttendanceJobService
    {
        public Task MarkAbsentTeachersAsync() => Task.CompletedTask;
    }

    /// <summary>حاوية كما يبنيها Program.cs، بتخزين في الذاكرة بدل SQL Server.</summary>
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddHangfire(config => config.UseInMemoryStorage());
        services.AddScoped<IAttendanceJobService, FakeAttendanceJobService>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Register_does_not_need_the_static_JobStorage_to_be_initialized()
    {
        using var provider = BuildProvider();

        // لا شيء هنا يُحَلّ التخزين قبل التسجيل — وهو بالضبط ما كسر الإقلاع.
        var exception = Record.Exception(() => RecurringJobs.Register(provider));

        Assert.Null(exception);
    }

    [Fact]
    public void Register_adds_the_absent_teachers_job()
    {
        using var provider = BuildProvider();

        RecurringJobs.Register(provider);

        var storage = provider.GetRequiredService<JobStorage>();
        using var connection = storage.GetConnection();
        var job = connection.GetRecurringJobs()
            .SingleOrDefault(j => j.Id == RecurringJobs.MarkAbsentTeachersId);

        Assert.NotNull(job);
        Assert.Equal("59 23 * * *", job!.Cron);
    }

    [Fact]
    public void Register_is_idempotent_across_restarts()
    {
        using var provider = BuildProvider();

        RecurringJobs.Register(provider);
        RecurringJobs.Register(provider);

        var storage = provider.GetRequiredService<JobStorage>();
        using var connection = storage.GetConnection();

        Assert.Single(connection.GetRecurringJobs(), j => j.Id == RecurringJobs.MarkAbsentTeachersId);
    }

    [Fact]
    public void The_configured_time_zone_resolves_on_this_platform()
    {
        // معرّف ويندوز؛ .NET يترجمه إلى IANA على لينكس. لو فشلت الترجمة على منصّة
        // النشر لسقط الإقلاع هنا لا في وقت تنفيذ المهمة.
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");

        Assert.NotNull(timeZone);
    }
}
