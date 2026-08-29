using Jaberah.Middlewares;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace Jaberah.Tests;

/// <summary>
/// هذه الفلاتر كانت كودًا ميتًا (لم تُطبَّق على أي كنترولر) فمرّ عام كامل بلا تحقق
/// من الدور. الاختبارات هنا تثبّت سلوكها حتى لا تعود صامتة مرة أخرى.
/// </summary>
public class AuthorizationFilterTests
{
    private static ActionExecutingContext ContextFor(UserViewModel? user, params (string Key, string Value)[] headers)
    {
        var httpContext = new DefaultHttpContext();
        foreach (var (key, value) in headers) httpContext.Request.Headers[key] = value;
        if (user is not null) httpContext.Items["User"] = user;

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), controller: null!);
    }

    private static UserViewModel User(int id, Role role) =>
        new() { Id = id, Name = $"user-{id}", PhoneNumber = "700000000", Role = role.ToString() };

    // ---------- IsAdmin ----------

    [Fact]
    public void IsAdmin_admits_an_admin()
    {
        var context = ContextFor(User(1, Role.ADMIN));

        new IsAdminAttribute().OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void IsAdmin_rejects_a_teacher()
    {
        var context = ContextFor(User(2, Role.TEACHER));

        new IsAdminAttribute().OnActionExecuting(context);

        Assert.IsType<ForbidResult>(context.Result);
    }

    [Fact]
    public void IsAdmin_rejects_a_request_with_no_identity()
    {
        // VerifyToken لم يعمل (نُسي على الكنترولر): يجب ألا يُفتَح الباب.
        var context = ContextFor(user: null);

        new IsAdminAttribute().OnActionExecuting(context);

        Assert.IsType<ForbidResult>(context.Result);
    }

    // ---------- VerifyToken ----------

    private static async Task<ActionExecutingContext> RunVerifyToken(TestDatabase database, params (string, string)[] headers)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["TokenKey"] = new string('k', 64) })
            .Build();

        var tokenHelper = new TokenHelper(config, database.Db);
        var context = ContextFor(user: null, headers);

        var nextCalled = false;
        await new VerifyTokenAttribute(tokenHelper).OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        context.HttpContext.Items["next-called"] = nextCalled;
        return context;
    }

    [Theory]
    [InlineData(new object[] { new string[0] })]                       // بلا ترويسة
    [InlineData(new object[] { new[] { "" } })]                        // ترويسة فارغة
    [InlineData(new object[] { new[] { "abc.def.ghi" } })]             // بلا كلمة Bearer
    [InlineData(new object[] { new[] { "Basic abc" } })]               // مخطط آخر
    [InlineData(new object[] { new[] { "Bearer" } })]                  // Bearer بلا توكن
    [InlineData(new object[] { new[] { "Bearer a b c" } })]            // أجزاء زائدة
    public async Task VerifyToken_returns_401_for_a_header_it_cannot_parse(string[] header)
    {
        using var database = new TestDatabase();

        var headers = header.Length == 0
            ? Array.Empty<(string, string)>()
            : [("Authorization", header[0])];

        var context = await RunVerifyToken(database, headers);

        Assert.IsType<UnauthorizedResult>(context.Result);
        Assert.False((bool)context.HttpContext.Items["next-called"]!);
    }

    [Fact]
    public async Task VerifyToken_returns_403_for_a_well_formed_but_invalid_token()
    {
        using var database = new TestDatabase();

        var context = await RunVerifyToken(database, ("Authorization", "Bearer not.a.real.token"));

        Assert.IsType<ForbidResult>(context.Result);
    }

    [Fact]
    public async Task VerifyToken_puts_the_caller_in_HttpContext_Items_for_a_valid_token()
    {
        using var database = new TestDatabase();
        database.SeedTeachersAndStudents();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["TokenKey"] = new string('k', 64) })
            .Build();
        var tokenHelper = new TokenHelper(config, database.Db);
        var token = tokenHelper.GenerateToken("2", "معلم أول", 1);

        var context = await RunVerifyToken(database, ("Authorization", $"Bearer {token}"));

        Assert.Null(context.Result);
        var user = Assert.IsType<UserViewModel>(context.HttpContext.Items["User"]);
        Assert.Equal(2, user.Id);
        Assert.Equal(nameof(Role.TEACHER), user.Role);
    }

    [Fact]
    public async Task VerifyToken_accepts_the_scheme_case_insensitively()
    {
        using var database = new TestDatabase();
        database.SeedTeachersAndStudents();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["TokenKey"] = new string('k', 64) })
            .Build();
        var token = new TokenHelper(config, database.Db).GenerateToken("1", "مدير", 1);

        var context = await RunVerifyToken(database, ("Authorization", $"bearer {token}"));

        Assert.Null(context.Result);
    }

    // ---------- RequireDeployKey ----------

    private static RequireDeployKeyAttribute DeployKeyFilter(string? configured)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DeployKey"] = configured })
            .Build();
        return new RequireDeployKeyAttribute(config);
    }

    [Fact]
    public void RequireDeployKey_fails_closed_when_no_key_is_configured()
    {
        // بلا مفتاح مضبوط يجب أن تُغلق النقطة، لا أن تنفتح للجميع.
        var context = ContextFor(user: null, (RequireDeployKeyAttribute.HeaderName, "anything"));

        DeployKeyFilter(configured: null).OnActionExecuting(context);

        var result = Assert.IsType<StatusCodeResult>(context.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("wrong-key")]
    [InlineData("right-key-with-suffix")]
    public void RequireDeployKey_rejects_a_wrong_or_missing_key(string provided)
    {
        var context = ContextFor(user: null, (RequireDeployKeyAttribute.HeaderName, provided));

        DeployKeyFilter("right-key").OnActionExecuting(context);

        Assert.IsType<UnauthorizedResult>(context.Result);
    }

    [Fact]
    public void RequireDeployKey_rejects_a_request_with_no_header_at_all()
    {
        var context = ContextFor(user: null);

        DeployKeyFilter("right-key").OnActionExecuting(context);

        Assert.IsType<UnauthorizedResult>(context.Result);
    }

    [Fact]
    public void RequireDeployKey_admits_the_configured_key()
    {
        var context = ContextFor(user: null, (RequireDeployKeyAttribute.HeaderName, "right-key"));

        DeployKeyFilter("right-key").OnActionExecuting(context);

        Assert.Null(context.Result);
    }
}
