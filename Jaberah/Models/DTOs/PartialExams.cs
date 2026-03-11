using System.ComponentModel.DataAnnotations;

namespace Jaberah.Models.DTOs
{
    public class CreatePartialExamDto
    {
        [Required]
        public int StudentId { get; set; }

        [Required]
        public DateOnly ExamDate { get; set; } // Format: "YYYY-MM-DD"

        [Range(0, 1.5)]
        public decimal Question1 { get; set; }

        [Range(0, 1.5)]
        public decimal Question2 { get; set; }

        [Range(0, 1.5)]
        public decimal Question3 { get; set; }

        [Range(0, 1.5)]
        public decimal Question4 { get; set; }

        [Range(0, 1.5)]
        public decimal Question5 { get; set; }

        [Range(0, 1.5)]
        public decimal Question6 { get; set; }

        [Range(0, 1.5)]
        public decimal Question7 { get; set; }

        [Range(0, 1.5)]
        public decimal Question8 { get; set; }

        [Range(0, 1.5)]
        public decimal Question9 { get; set; }

        [Range(0, 1.5)]
        public decimal Question10 { get; set; }

        [Range(0, 5)]
        public decimal Performance { get; set; }

        [MaxLength(200)]
        public string? Tester { get; set; }

        [MaxLength(200)]
        public string? Part { get; set; }
        [MaxLength(200)]
        public string? Rate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public decimal TotalScore { get; set; }
    }

    public class UpdatePartialExamDto
    {
        [Required]
        public int Id { get; set; }

        [Range(0, 1.5)]
        public decimal Question1 { get; set; }

        [Range(0, 1.5)]
        public decimal Question2 { get; set; }

        [Range(0, 1.5)]
        public decimal Question3 { get; set; }

        [Range(0, 1.5)]
        public decimal Question4 { get; set; }

        [Range(0, 1.5)]
        public decimal Question5 { get; set; }

        [Range(0, 1.5)]
        public decimal Question6 { get; set; }

        [Range(0, 1.5)]
        public decimal Question7 { get; set; }

        [Range(0, 1.5)]
        public decimal Question8 { get; set; }

        [Range(0, 1.5)]
        public decimal Question9 { get; set; }

        [Range(0, 1.5)]
        public decimal Question10 { get; set; }

        [Range(0, 5)]
        public decimal Performance { get; set; }

        [MaxLength(200)]
        public string? Tester { get; set; }

        [MaxLength(200)]
        public string? Part { get; set; }
        [MaxLength(200)]
        public string? Rate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public decimal TotalScore { get; set; }
    }

}
