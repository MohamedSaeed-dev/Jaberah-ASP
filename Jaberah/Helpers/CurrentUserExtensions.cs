using Jaberah.Models.JaberahModels;
using Jaberah.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Jaberah.Helpers
{
    /// <summary>
    /// وصول موحّد إلى هوية المستدعي التي يضعها <c>VerifyTokenAttribute</c> في
    /// <c>HttpContext.Items["User"]</c>. تُستخدم في النقاط التي يشترك فيها المعلم والمدير
    /// ويجب أن تُحصر بيانات المعلم على نفسه.
    /// </summary>
    public static class CurrentUserExtensions
    {
        public static UserViewModel? CurrentUser(this ControllerBase controller) =>
            controller.HttpContext.Items["User"] as UserViewModel;

        public static bool IsCurrentUserAdmin(this ControllerBase controller) =>
            controller.CurrentUser()?.Role == nameof(Role.ADMIN);

        /// <summary>مدير يتصرف بأي معلم، وغير المدير بنفسه فقط.</summary>
        public static bool CanActOnTeacher(this ControllerBase controller, int teacherId) =>
            controller.IsCurrentUserAdmin() || controller.CurrentUser()?.Id == teacherId;
    }
}
