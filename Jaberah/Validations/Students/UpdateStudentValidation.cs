using Jaberah.Helpers;
using Microsoft.AspNetCore.Mvc.Filters;
using static Jaberah.Models.DTOs.Students;

namespace Jaberah.Validations.Students
{
    [AttributeUsage(AttributeTargets.Method)]
    public class UpdateStudentAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            List<ValidationModel> validationContent = new();


            if (context.ActionArguments.ContainsKey("model"))
            {
                UpdateStudentDTO dto = (UpdateStudentDTO)context.ActionArguments["model"]!;
                if (dto.StudentName is not null && !dto.StudentName.ContainsArabicAndSpaces())
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "اسم الطالب",
                        Message = "اسم الطالب يجب ان يحتوي على احرف عربية فقط"
                    });
                }

                if (dto.PhoneNumber is not null && !dto.PhoneNumber.IsPhoneNumberStartingWith7())
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "رقم ولي الامر",
                        Message = "رقم ولي الامر يجب ان يكون رقم جوال صحيح يبدا بـ7"
                    });
                }

                if (dto.GroupId.HasValue && dto.GroupId <= 0)
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "الحلقة",
                        Message = "رقم الحلقة يجب ان يكون اكبر من صفر"
                    });
                }

            }
            base.OnActionExecuting(context);
        }
    }
}
