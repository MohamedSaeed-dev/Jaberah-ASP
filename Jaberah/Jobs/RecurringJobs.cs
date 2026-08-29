using Hangfire;

namespace Jaberah.Jobs
{
    public static class RecurringJobs
    {
        public const string MarkAbsentTeachersId = "mark-absent-teachers";

        /// <summary>
        /// تسجيل المهام الدورية عبر <see cref="IRecurringJobManager"/> من الحقن، لا عبر
        /// <c>RecurringJob</c> الساكن.
        ///
        /// الـ API الساكن يقرأ <c>JobStorage.Current</c>، و<c>AddHangfire</c> لا يضبطه عند
        /// التسجيل — يُضبط كأثر جانبي أول مرة يُحَلّ فيها التخزين من الحقن. فكان النداء
        /// الساكن يعمل بالصدفة لأن <c>app.UseHangfireDashboard()</c> مكررًا كان يسبقه
        /// ويُحَلّ التخزين نيابةً عنه؛ ولمّا حُذف ذلك النداء (كان يفتح /hangfire بلا فلتر
        /// صلاحيات وقبل المصادقة) سقط الإقلاع بـ «JobStorage instance has not been
        /// initialized yet». الحلّ ألّا نعتمد على الحالة العامة الساكنة أصلًا.
        /// </summary>
        public static void Register(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

            manager.AddOrUpdate<IAttendanceJobService>(
                MarkAbsentTeachersId,
                job => job.MarkAbsentTeachersAsync(),
                "59 23 * * *", // 23:59 كل يوم
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time")
                });
        }
    }
}
