using Jaberah.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Jaberah.Validations.CleaningLogs
{
    [AttributeUsage(AttributeTargets.Method)]
    public class UpsertDailyCleaningLogAttribute : ActionFilterAttribute
    {
        private const int MaxLogsPerRequest = 100;
        private const int MaxNotesLength = 500;

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            List<ValidationModel> validationContent = new();

            if (context.ActionArguments.TryGetValue("model", out var argument) && argument is UpsertDailyCleaningLogDTO dto)
            {
                if (dto.Date == default)
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "التاريخ",
                        Message = "ادخل تاريخ صحيح"
                    });
                }

                if (dto.Logs.Count == 0)
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "المهمات",
                        Message = "اختر مهمة واحدة على الاقل"
                    });
                }
                else if (dto.Logs.Count > MaxLogsPerRequest)
                {
                    validationContent.Add(new ValidationModel
                    {
                        Key = "المهمات",
                        Message = $"عدد المهمات يجب ان لا يتجاوز {MaxLogsPerRequest} في الطلب الواحد"
                    });
                }
                else
                {
                    if (dto.Logs.Any(x => x.CleaningTaskId <= 0))
                    {
                        validationContent.Add(new ValidationModel
                        {
                            Key = "المهمة",
                            Message = "ادخل id صحيح للمهمة"
                        });
                    }

                    if (dto.Logs.Any(x => x.StudentId.HasValue && x.StudentId.Value <= 0))
                    {
                        validationContent.Add(new ValidationModel
                        {
                            Key = "الطالب",
                            Message = "ادخل id صحيح للطالب"
                        });
                    }

                    if (dto.Logs.Select(x => x.CleaningTaskId).Distinct().Count() != dto.Logs.Count)
                    {
                        validationContent.Add(new ValidationModel
                        {
                            Key = "المهمات",
                            Message = "لا يمكن تكرار نفس المهمة في نفس الطلب"
                        });
                    }

                    if (dto.Logs.Any(x => !string.IsNullOrEmpty(x.Notes) && x.Notes.Length > MaxNotesLength))
                    {
                        validationContent.Add(new ValidationModel
                        {
                            Key = "الملاحظات",
                            Message = $"الملاحظات يجب ان لا تتجاوز {MaxNotesLength} حرف"
                        });
                    }
                }
            }
            else
                validationContent.Add(new ValidationModel
                {
                    Key = "بيانات كشف النظافة",
                    Message = "بيانات كشف النظافة اجبارية"
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
