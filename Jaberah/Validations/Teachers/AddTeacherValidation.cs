using Jaberah.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using static Jaberah.Models.DTOs.Teachers;

namespace Jaberah.Validations.Teachers
{
    [AttributeUsage(AttributeTargets.Method)]
    public class AddTeacherAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            List<ValidationModel> validationContent = new();


            if (context.ActionArguments.ContainsKey("model"))
            {
                AddTeacherDTO dto = (AddTeacherDTO)context.ActionArguments["model"]!;
                if (dto.TeacherName is null)
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "اسم المعلم",
                        Message = "اسم المعلم اجباري"
                    });
                }
                else if (!dto.TeacherName.ContainsArabicAndSpaces())
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "اسم المعلم",
                        Message = "اسم المعلم يجب ان يحتوي على احرف عربية فقط"
                    });
                }

                if (dto.PhoneNumber is null)
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "رقم الجوال",
                        Message = "رقم الجوال اجباري"
                    });
                }
                else if (!dto.PhoneNumber.IsPhoneNumberStartingWith7())
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "رقم الجوال",
                        Message = "رقم الجوال يجب ان يكون رقم جوال صحيح يبدا بـ7"
                    });
                }

                if (dto.GroupsId is not null && dto.GroupsId.Any(x => x <= 0))
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "الحلقات",
                        Message = "رقم الحلقة يجب ان يكون اكبر من صفر"
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
                            Key = "اسم المعلم",
                            Message = "اسم المعلم اجباري"
                        },
                        new ()
                        {
                            Key = "رقم الجوال",
                            Message = "رقم الجوال اجباري"
                        },
                        new ()
                        {
                            Key = "الحلقات",
                            Message = "الحلقات اجبارية"
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
