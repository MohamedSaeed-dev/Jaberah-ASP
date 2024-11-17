using Jaberah.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Jaberah.Validations.TeachersSalaries
{
    [AttributeUsage(AttributeTargets.Method)]
    public class UpsertTeachersSalariesAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            List<ValidationModel> validationContent = new();

            if (context.ActionArguments.ContainsKey("model"))
            {
                UpsertTeachersSalariesDTO dto = (UpsertTeachersSalariesDTO)context.ActionArguments["model"]!;
                if (dto.TeacherId <= 0)
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "المعلم",
                        Message = "المعلم اجباري"
                    });
                }

                if (dto.Salary.HasValue && dto.Salary.Value < 0)
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "الراتب",
                        Message = "الراتب يجب ان يكون اكبر من صفر"
                    });
                }
                else if (dto.DaysAbsence.HasValue && dto.DaysAbsence.Value < 0)
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "ايام الغياب",
                        Message = "ايام الغياب يجب ان تكون اكبر من صفر"
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
                            Key = "المعلم",
                            Message = "المعلم اجباري"
                        },
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
