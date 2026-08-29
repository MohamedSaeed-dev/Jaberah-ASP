using Jaberah.Helpers;
using Jaberah.Models.JaberahModels;
using Jaberah.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jaberah.Tests;

/// <summary>
/// الحصر بالهوية على النقاط التي يصلها المعلم والمدير معًا
/// (ملف المعلم، حلقاته، حضوره).
/// </summary>
public class CurrentUserExtensionsTests
{
    private sealed class ProbeController : ControllerBase;

    private static ProbeController ControllerFor(UserViewModel? user)
    {
        var httpContext = new DefaultHttpContext();
        if (user is not null) httpContext.Items["User"] = user;

        return new ProbeController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    private static UserViewModel User(int id, Role role) =>
        new() { Id = id, Name = $"user-{id}", PhoneNumber = "700000000", Role = role.ToString() };

    [Fact]
    public void A_teacher_may_act_on_themself()
        => Assert.True(ControllerFor(User(2, Role.TEACHER)).CanActOnTeacher(2));

    [Fact]
    public void A_teacher_may_not_act_on_another_teacher()
        => Assert.False(ControllerFor(User(2, Role.TEACHER)).CanActOnTeacher(3));

    [Fact]
    public void An_admin_may_act_on_any_teacher()
    {
        var controller = ControllerFor(User(1, Role.ADMIN));

        Assert.True(controller.CanActOnTeacher(1));
        Assert.True(controller.CanActOnTeacher(2));
        Assert.True(controller.CanActOnTeacher(3));
    }

    [Fact]
    public void With_no_identity_nothing_is_permitted()
    {
        var controller = ControllerFor(user: null);

        Assert.False(controller.IsCurrentUserAdmin());
        Assert.False(controller.CanActOnTeacher(1));
        Assert.Null(controller.CurrentUser());
    }
}
