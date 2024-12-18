using Jaberah.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using static Jaberah.Models.DTOs.Students;

namespace Jaberah.Validations.Students
{
    [AttributeUsage(AttributeTargets.Method)]
    public class AddStudentAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            List<ValidationModel> validationContent = new();


            if (context.ActionArguments.ContainsKey("model"))
            {
                AddStudentDTO dto = (AddStudentDTO)context.ActionArguments["model"]!;
                if (dto.StudentName is null)
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "اسم الطالب",
                        Message = "اسم الطالب اجباري"
                    });
                }
                else if (!dto.StudentName.ContainsArabicAndSpaces())
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "اسم الطالب",
                        Message = "اسم الطالب يجب ان يحتوي على احرف عربية فقط"
                    });
                }

                if (dto.PhoneNumber is null)
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "رقم ولي الامر",
                        Message = "رقم ولي الامر اجباري"
                    });
                }
                else if (!dto.PhoneNumber.IsPhoneNumberStartingWith7())
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "رقم ولي الامر",
                        Message = "رقم ولي الامر يجب ان يكون رقم جوال صحيح يبدا بـ7"
                    });
                }

            }
            else
                validationContent.AddRange
                (
                    new List<ValidationModel>()
                    {
                        new()
                        {
                            Key = "اسم الطالب",
                            Message = "اسم الطالب اجباري"
                        },
                        new ()
                        {
                            Key = "رقم ولي الامر",
                            Message = "رقم ولي الامر اجباري"
                        }
                    }
                );
            if (validationContent.Count > 0)
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Result = new JsonResult(new
                {
                    validationContent,
                });
            }
            base.OnActionExecuting(context);
        }
    }
}
