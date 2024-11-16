using Jaberah.Helpers;
using Microsoft.AspNetCore.Mvc.Filters;
using static Jaberah.Models.DTOs.Teachers;

namespace Jaberah.Validations.Teachers
{
    [AttributeUsage(AttributeTargets.Method)]
    public class UpdateTeacherAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            List<ValidationModel> validationContent = new();


            if (context.ActionArguments.ContainsKey("model"))
            {
                UpdateTeacherDTO dto = (UpdateTeacherDTO)context.ActionArguments["model"]!;
                if (dto.TeacherName is not null && !dto.TeacherName.ContainsArabicAndSpaces())
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "اسم المعلم",
                        Message = "اسم المعلم يجب ان يحتوي على احرف عربية فقط"
                    });
                }

                if (dto.PhoneNumber is not null && !dto.PhoneNumber.IsPhoneNumberStartingWith7())
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "رقم الجوال",
                        Message = "رقم الجوال يجب ان يكون رقم جوال صحيح يبدا بـ7"
                    });
                }

                if (dto.NewPassword is null ^ dto.OldPassword is null)
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "كلمة السر",
                        Message = "يجب ارسال كلمة السر الجديدة وكلمة السر القديمة معاً"
                    });
                }
                else if (dto.NewPassword is not null && (dto.NewPassword.Length < 8 && dto.OldPassword!.Length < 8))
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "كلمة السر",
                        Message = "كلمة السر يجب ان تكون اكبر من 8 احرف"
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
            base.OnActionExecuting(context);
        }
    }
}
