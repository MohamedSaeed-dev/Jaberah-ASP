using Jaberah.Helpers;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Jaberah.Validations.Groups
{
    [AttributeUsage(AttributeTargets.Method)]
    public class AddGroupValidationAttribute : ActionFilterAttribute
    {

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            List<ValidationModel> validationContent = new();


            if (context.ActionArguments.ContainsKey("model"))
            {
                AddGroupDTO dto = (AddGroupDTO)context.ActionArguments["model"]!;

                if (string.IsNullOrWhiteSpace(dto.GroupName))
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "اسم الحلقة",
                        Message = "اسم الحلقة اجباري"
                    });
                }
                else if (!dto.GroupName.ContainsArabic())
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "اسم الحلقة",
                        Message = "اسم الحلقة يجب ان يحتوي على احرف عربية فقط"
                    });
                }

                if (!Enum.IsDefined(typeof(Period), dto.Period))
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "الفترة",
                        Message = $"الفترة يجب ان تكون 0 (مسائية) او 1 (صباحية)"
                    });
                }
            }
            else
                validationContent.AddRange
                (
                    new List<ValidationModel>()
                    {
                        new ()
                        {
                            Key = "اسم الحلقة",
                            Message = "اسم الحلقة اجباري"
                        },
                        new ()
                        {
                            Key = "الفترة",
                            Message = "الفترة اجبارية"
                        }
                    }
                );
            if (validationContent.Count > 0)
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Result = new JsonResult(new
                {
                    Data = validationContent,
                    Code = 400
                });
            }
        }
    }
}
