# Jaberah API

الخادم الذي يقف خلف تطبيق **حلقات مسجد جابرة**. يدير المعلمين وحلقاتهم وطلابهم،
ويسجّل متابعة الحفظ والمراجعة، والصلوات، والاختبارات، وحضور المعلمين ورواتبهم،
وكشف النظافة اليومي — ويُخرج من ذلك تقارير شهرية وفصلية للمدير.

المستهلك الوحيد لهذه الواجهة هو تطبيق الجوال في مستودع
[Jaberah-Flutter](https://github.com/MohamedSaeed-dev/Jaberah-Flutter).
لا توجد واجهة ويب، ولا عملاء آخرون.

## الأدوار

دوران اثنان فقط، في عمود `Role` من جدول `Teachers`. لا يوجد جدول مستخدمين منفصل —
المدير معلم بدور مختلف.

| الدور | القيمة | النطاق |
|---|---|---|
| `ADMIN` | 1 | الحلقات، الطلاب، المعلمون، الرواتب، الاختبارات الجزئية، التقارير، سلة المحذوفات، السجلات |
| `TEACHER` | 2 | حلقاته هو: المتابعة اليومية، الصلوات، كشف النظافة، حضوره، راتبه، تقاريره |

بعض النقاط يصلها الاثنان وتحمل معرّف المعلم في المسار
(`GET /api/teachers/{id}/groups`، `PUT /api/teachers/{id}`، وحضور المعلم).
هذه محصورة بالهوية لا بالدور: المدير يصل لأي معلم، وغيره لنفسه فقط.

## التقنيات

ASP.NET Core 9 · EF Core 9 على SQL Server · AutoMapper · Hangfire · Serilog ·
Firebase Admin SDK للإشعارات · Dropbox لاستضافة ملف الـ APK · xUnit للاختبارات.

## تنظيم المشروع

```
Jaberah/
  Controllers/        نقطة لكل مجال: Auth, Students, Groups, Teachers, Prayers,
                      FollowStudents, Exams, Reports, CleaningLogs, ...
  Models/
    JaberahModels/    كيانات EF (Student, Group, Teacher, CleaningLog, ...)
    DTOs/             أجسام الطلبات
    ViewModels/       أشكال الردود
    MyDbContext/      JaberahDBContext — كل التعيينات والفهارس في مكان واحد
  Middlewares/        VerifyToken, IsAdmin, RequireDeployKey, تسجيل الطلبات
  Validations/        التحقق من المدخلات كـ action filters (انظر أدناه)
  Helpers/            AutoMapper, Dropbox, Firebase, PagedList, معالج الاستثناءات
  Jobs/               مهمة Hangfire الدورية وفلتر لوحتها
  SeedData/           بذور اختيارية من ملفات JSON (معطَّلة افتراضيًا)
Jaberah.Tests/        xUnit على SQLite في الذاكرة
```

## أعراف تُربك القادم الجديد

**الحذف ناعم.** كيانات المجال ترث `BaseEntity` (`Id`, `CreatedAt`, `UpdatedAt`,
`DeletedAt`)، و`OnModelCreating` يمرّ على كل ما يرثه ويركّب عليه `HasQueryFilter`
يُخفي المحذوف. فـ `_db.Students` **لا** ترجع المحذوفين إطلاقًا؛ للوصول إليهم — كما في
سلة المحذوفات — استعمل `.IgnoreQueryFilters()`. وللحذف استعمل `_db.SoftDelete(entity)`
لا `Remove`.

يشذّ عن ذلك جدولا المرجع `Prayers` و`CleaningTasks`: لا يرثان `BaseEntity`، ويُبذران
بـ `HasData` في نفس الملف، ولا يُحذفان — التعطيل فيهما براية `IsActive`.

**الوقت بتوقيت الرياض لا UTC.** `JaberahDBContext.GetCurrentDateTime()` يرجع
`DateTime.UtcNow.AddHours(3)`، وهو ما يُختم به `CreatedAt`/`UpdatedAt` تلقائيًا في
`SaveChangesAsync`. تعامل مع كل ختم زمني في القاعدة على أنه توقيت محلي (+3).

**التحقق من المدخلات ليس DataAnnotations.** كل عملية لها attribute خاص في
`Validations/` (مثل `[AddStudent]` و`[UpdateTeacher]`) يفحص الـ DTO ويرجع 400
مع `validationContent` — قائمة `{key, message}` بالعربية يعرضها التطبيق كما هي.
عند إضافة نقطة جديدة اتبع النمط نفسه بدل وضع سمات على الـ DTO.

**طبقتا مصادقة.** `FallbackPolicy` في `Program.cs` تفرض توكن JWT صالحًا على كل
نقطة لم تُعلَّم بـ `[AllowAnonymous]`. وفوقها `[ServiceFilter(typeof(VerifyTokenAttribute))]`
الذي يحمّل المعلم من القاعدة ويضعه في `HttpContext.Items["User"]`، وعليه يعتمد
`[IsAdmin]` وامتدادات `CurrentUserExtensions`. الكنترولر الذي يحتاج معرفة
المستدعي يحتاج الاثنين معًا.

**مجلد `Migrations/` مستثنى من Git**، ولا يُطبَّق شيء تلقائيًا عند الإقلاع. أول
تشغيل محلي يتطلب توليد migration وتطبيقه بنفسك.

## التشغيل محليًا

المتطلبات: .NET SDK 9، و SQL Server (LocalDB يكفي).

`appsettings.json` غير مرفوع (مستثنى من Git). أنشئه في مجلد `Jaberah/` بهذا الشكل:

```json
{
  "ConnectionStrings": { "DB": "Server=(localdb)\\MSSQLLocalDB;Database=Jaberah;Trusted_Connection=True;" },
  "TokenKey": "مفتاح توقيع طويل عشوائي",
  "DeployKey": "مفتاح نشر الـ APK",
  "Cors": { "AllowedOrigins": [] },
  "FCM": { "ServiceAccountFilePath": "المسار إلى ملف حساب خدمة Firebase" },
  "Dropbox": { "clientId": "...", "clientSecret": "...", "refreshToken": "..." }
}
```

ملف حساب خدمة Firebase مستثنى من Git أيضًا ولا يدخل حزمة النشر — يوضع على الخادم
يدويًا. وانتبه: `GoogleCredential.FromFile` يقرأ الملف عند الإقلاع ولا يتحقق منه
لدى Google، فمفتاح منتهٍ أو تالف يُقلع بنجاح ثم يفشل عند أول إرسال إشعار برسالة
`invalid_grant: Invalid JWT Signature`. وانقل الملف بوضع binary — النقل النصي يفسد
أسطر `private_key` ويعطي الخطأ نفسه.

ثم:

```bash
dotnet restore
dotnet tool restore              # dotnet-ef مثبَّت كأداة محلية في .config
dotnet ef migrations add Init -p Jaberah
dotnet ef database update -p Jaberah
dotnet run --project Jaberah
```

واجهة Swagger على **جذر** الموقع (`http://localhost:5291/`) لأن `RoutePrefix` مضبوط
على نص فارغ. وفي وضع التطوير تعمل على `/swagger` أيضًا، لأن الواجهة مسجَّلة مرتين
في `Program.cs` — مرة داخل شرط `IsDevelopment` وأخرى بعده.

`SeedData/DataSeeder.cs` يملأ القاعدة من ملفات JSON في `SeedData/`. نداؤه معطَّل
بتعليق في `Program.cs` — فعّله عند الحاجة فقط.

## الاختبارات

```bash
dotnet test
```

`Jaberah.Tests` يشغّل `JaberahDBContext` الحقيقي على SQLite في الذاكرة — بنفس
التعيينات والفهارس والمرشّحات — ويغطّي فلاتر الصلاحيات، وقواعد كشف النظافة،
وحصر الهوية، وتسجيل مهام Hangfire.

## المهام المجدولة

Hangfire يشغّل `MarkAbsentTeachersAsync` عند 23:59 بتوقيت الرياض: يعلّم المعلمين
الذين لم يسجّلوا حضورًا غائبين، ويتخطّى الجمعة. التسجيل في `Jobs/RecurringJobs.cs`
عبر `IRecurringJobManager` من الحقن — لا تستعمل `RecurringJob` الساكن هنا، فهو
يعتمد على `JobStorage.Current` الذي لا يضبطه `AddHangfire` وسيرمي عند الإقلاع.

اللوحة على `/hangfire` للمدير فقط.

## السجلات

`Middlewares/RequestResponseLoggingMiddleware` يكتب الطلبات ذات الرد **غير 2xx**
فقط إلى `Logs/error-requests.log`، بعد تنقية الحقول الحساسة (كلمات المرور،
التوكنات، مفتاح النشر) وتخطّي الحمولات الثنائية والكبيرة.

يُقرأ عبر `GET /api/Logs` بحساب مدير، و`DELETE /api/Logs` يُفرغه.

## النشر

GitHub Actions ← MonsterASP.NET، وخادمان منفصلان:

| الفرع | الوجهة |
|---|---|
| `master` | Server 1 |
| `main-v2` | Server 2 — وهو الإنتاج الحالي |

الترتيب: بناء ← اختبار ← تحزيم (`publish`) ← رفع إلى الخادم. الاختبار قبل التحزيم
عمدًا، فاختبار فاشل يوقف كل شيء قبل أن تُبنى حزمة النشر أصلًا.

الخط يعمل على طلبات الدمج أيضًا، لكن خطوتي النشر محصورتان بـ `github.ref` فلا تعملان
إلا على الفرعين أعلاه.

### مفتاح نشر الـ APK

`PUT /api/versions` يستقبل ملف الـ APK من خط نشر تطبيق الفلاتر، يرفعه إلى Dropbox،
ويجعل رابطه رابط التحديث الرسمي لكل المستخدمين. هذه النقطة لا تملك توكن JWT (الخط
ليس مستخدمًا)، فتحرسها ترويسة `X-Deploy-Key` تُقارَن بقيمة `DeployKey` من الإعدادات
عبر تجزئة SHA-256 ومقارنة ثابتة الزمن.

القيمة نفسها يجب أن توجد في مكانين: `DeployKey` في إعدادات الخادم، و `DEPLOY_KEY`
في GitHub Secrets لمستودع الفلاتر. وإن لم تُضبط على الخادم ترفض النقطة كل رفع
بـ 503 — تفشل مغلقة لا مفتوحة.

## أمور معروفة لم تُعالج بعد

- `builder.Host.UseSerilog()` مستدعى بلا تهيئة، فكل نداء `ILogger` في التطبيق يذهب
  إلى العدم — بما فيه تسجيل الاستثناءات غير المعالَجة في `GlobalException.cs`.
  الملف الوحيد الذي يُكتب فعلًا هو سجل الطلبات أعلاه.
- الختم الزمني داخل سجل الطلبات يضيف ثلاث ساعات مرتين، فيتقدّم ست ساعات ويُعلَّم
  `Z` كأنه UTC.
- `AutoMapper 13.0.1` عليه تنبيه أمني ([GHSA-rvv3-g6hj-g44x](https://github.com/advisories/GHSA-rvv3-g6hj-g44x))،
  والترقية إلى 14 كسر متعمَّد في الواجهة.
- لا شيء في الـ CI يُقلع التطبيق فعليًا، فأعطال الإقلاع لا تظهر إلا بعد النشر.
