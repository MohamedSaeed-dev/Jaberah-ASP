using Jaberah.Helpers;
using Jaberah.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Jaberah.Validations.CleaningLogs
{
    [AttributeUsage(AttributeTargets.Method)]
    public class UpdateCleaningTaskAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            List<ValidationModel> validationContent = new();

            if (context.ActionArguments.TryGetValue("model", out var argument) && argument is UpdateCleaningTaskDTO dto)
            {
                if (dto.NameAr is not null)
                {
                    if (string.IsNullOrWhiteSpace(dto.NameAr) || !dto.NameAr.ContainsArabic())
                    {
                        validationContent.Add(new ValidationModel
                        {
                            Key = "اسم المهمة",
                            Message = "اسم المهمة يجب ان يحتوي على احرف عربية فقط"
                        });
                    }
                    else if (dto.NameAr.Trim().Length > 100)
                    {
                        validationContent.Add(new ValidationModel
                        {
                            Key = "اسم المهمة",
                            Message = "اسم المهمة يجب ان لا يتجاوز 100 حرف"
                        });
                    }
                }

                if (!string.IsNullOrWhiteSpace(dto.NameEn) && dto.NameEn.Trim().Length > 100)
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "الاسم بالانجليزية",
                        Message = "الاسم بالانجليزية يجب ان لا يتجاوز 100 حرف"
                    });
                }
            }
            else
                validationContent.Add(new ValidationModel
                {
                    Key = "بيانات المهمة",
                    Message = "بيانات المهمة اجبارية"
                });

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
