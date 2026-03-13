using Jaberah.Models.JaberahModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;
using Version = Jaberah.Models.JaberahModels.Version;

namespace Jaberah.Models.MyDbContext
{
    public class JaberahDBContext : DbContext
    {
        public JaberahDBContext(DbContextOptions<JaberahDBContext> options) : base(options)
        {

        }
        public DbSet<Teacher> Teachers { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<StudentAttendance> StudentAttendances { get; set; }
        public DbSet<TeacherAttendance> TeacherAttendances { get; set; }
        public DbSet<SaveLesson> SaveLessons { get; set; }
        public DbSet<ReviewLesson> ReviewLessons { get; set; }
        public DbSet<Exam> Exams { get; set; }
        public DbSet<TeacherSalary> TeacherSalaries { get; set; }
        public DbSet<MidFinal> MidFinals { get; set; }
        public DbSet<PartialExam> PartialExams { get; set; }
        public DbSet<Version> Versions { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<Prayer> Prayers { get; set; }
        public DbSet<StudentPrayerAttendance> StudentPrayerAttendances { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    var entity = modelBuilder.Entity(entityType.ClrType);

                    entity.HasQueryFilter(
                        ConvertFilterExpression<BaseEntity>(e => e.DeletedAt == null, entityType.ClrType)
                    );

                    entity.Property(nameof(BaseEntity.CreatedAt))
                        .HasDefaultValueSql("GETUTCDATE()");

                    entity.Property(nameof(BaseEntity.UpdatedAt))
                        .HasDefaultValueSql("GETUTCDATE()");
                }
            }

            // PartialExam: keep precision and unique per student/date
            modelBuilder.Entity<PartialExam>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => new { e.StudentId, e.Date })
                      .IsUnique()
                      .HasDatabaseName("UQ_PartialExams_StudentId_ExamDate");

                entity.Property(e => e.Question1).HasColumnType("decimal(3,1)");
                entity.Property(e => e.Question2).HasColumnType("decimal(3,1)");
                entity.Property(e => e.Question3).HasColumnType("decimal(3,1)");
                entity.Property(e => e.Question4).HasColumnType("decimal(3,1)");
                entity.Property(e => e.Question5).HasColumnType("decimal(3,1)");
                entity.Property(e => e.Question6).HasColumnType("decimal(3,1)");
                entity.Property(e => e.Question7).HasColumnType("decimal(3,1)");
                entity.Property(e => e.Question8).HasColumnType("decimal(3,1)");
                entity.Property(e => e.Question9).HasColumnType("decimal(3,1)");
                entity.Property(e => e.Question10).HasColumnType("decimal(3,1)");
                entity.Property(e => e.Performance).HasColumnType("decimal(3,1)");
                entity.Property(e => e.TotalScore).HasColumnType("decimal(4,1)");

                entity.Property(e => e.Tester).HasMaxLength(200);
                entity.Property(e => e.Part).HasMaxLength(200);
                entity.Property(e => e.Rate).HasMaxLength(200);
                entity.Property(e => e.Notes).HasMaxLength(500);

                entity.HasOne(e => e.Student)
                      .WithMany(s => s.PartialExams)
                      .HasForeignKey(e => e.StudentId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Book
            modelBuilder.Entity<Book>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Title).IsRequired().HasMaxLength(250);
                entity.Property(e => e.From).IsRequired().HasMaxLength(100);
                entity.Property(e => e.To).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Date).IsRequired();

                entity.HasOne(e => e.Group)
                      .WithMany(g => g.Books)
                      .HasForeignKey(e => e.GroupId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.GroupId, e.Date }).HasDatabaseName("IX_Books_GroupId_Date");
            });

            // Exam
            modelBuilder.Entity<Exam>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.PaperExam).HasDefaultValue(0);
                entity.Property(e => e.OralExam).HasDefaultValue(0);

                entity.HasOne(e => e.Student)
                      .WithMany(f => f.Exams)
                      .HasForeignKey(e => e.StudentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.StudentId, e.Date }).HasDatabaseName("IX_Exams_StudentId_Date");
            });

            // Version
            modelBuilder.Entity<Version>(ver =>
            {
                ver.HasKey(e => e.Id);
                ver.Property(e => e.LatestVersion).IsRequired().HasMaxLength(50);
                ver.Property(e => e.MinRequiredVersion).IsRequired().HasMaxLength(50);
                ver.Property(e => e.URL).IsRequired().HasMaxLength(500);
            });

            // MidFinal
            modelBuilder.Entity<MidFinal>(x =>
            {
                x.HasKey(a => a.Id);

                x.Property(a => a.FromDate).IsRequired();
                x.Property(a => a.ToDate).IsRequired();
                x.Property(a => a.Grade).IsRequired();

                x.HasOne(a => a.Student)
                 .WithMany(a => a.MidFinals)
                 .HasForeignKey(a => a.StudentId)
                 .OnDelete(DeleteBehavior.Cascade);

                x.HasIndex(a => new { a.StudentId, a.FromDate, a.ToDate }).HasDatabaseName("IX_MidFinals_Student_Period");
            });

            // Save Lessons
            modelBuilder.Entity<SaveLesson>(entity =>
            {
                entity.HasKey(f => f.Id);
                entity.Property(f => f.Date).IsRequired();
                entity.Property(f => f.SurahFrom).IsRequired().HasMaxLength(200);
                entity.Property(f => f.SurahTo).IsRequired().HasMaxLength(200);
                entity.Property(f => f.VerseFrom).IsRequired();
                entity.Property(f => f.VerseTo).IsRequired();
                entity.Property(f => f.Rate).IsRequired().HasMaxLength(50);
                entity.Property(f => f.Notes).HasMaxLength(500);

                entity.HasOne(f => f.Student)
                      .WithMany(s => s.SaveLessons)
                      .HasForeignKey(f => f.StudentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(f => new { f.StudentId, f.Date }).HasDatabaseName("IX_SaveLessons_StudentId_Date");
            });

            // Review Lessons
            modelBuilder.Entity<ReviewLesson>(entity =>
            {
                entity.HasKey(f => f.Id);
                entity.Property(f => f.Date).IsRequired();
                entity.Property(f => f.SurahFrom).IsRequired().HasMaxLength(200);
                entity.Property(f => f.SurahTo).IsRequired().HasMaxLength(200);
                entity.Property(f => f.VerseFrom).IsRequired();
                entity.Property(f => f.VerseTo).IsRequired();
                entity.Property(f => f.Rate).IsRequired().HasMaxLength(50);
                entity.Property(f => f.Notes).HasMaxLength(500);

                entity.HasOne(f => f.Student)
                      .WithMany(s => s.ReviewLessons)
                      .HasForeignKey(f => f.StudentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(f => new { f.StudentId, f.Date }).HasDatabaseName("IX_ReviewLessons_StudentId_Date");
            });

            // Group
            modelBuilder.Entity<Group>(entity =>
            {
                entity.HasKey(g => g.Id);
                entity.Property(g => g.Name).IsRequired().HasMaxLength(100);

                entity.HasOne(g => g.Teacher)
                      .WithMany(t => t.Groups)
                      .HasForeignKey(g => g.TeacherId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasMany(g => g.Students)
                      .WithOne(s => s.Group)
                      .HasForeignKey(s => s.GroupId)
                      .OnDelete(DeleteBehavior.SetNull);

                // group name should be unique
                entity.HasIndex(g => g.Name).IsUnique().HasDatabaseName("UQ_Groups_Name");
            });

            // Notification
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(n => n.Id);
                entity.Property(n => n.Title).IsRequired().HasMaxLength(250);
                entity.Property(n => n.Body).HasMaxLength(2000);
                entity.Property(n => n.CreatedAt).IsRequired();

                entity.HasIndex(n => n.CreatedAt).HasDatabaseName("IX_Notifications_CreatedAt");
            });

            // Student
            modelBuilder.Entity<Student>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Name).IsRequired().HasMaxLength(150);
                entity.Property(s => s.PhoneNumber).IsRequired().HasMaxLength(30);
                entity.Property(s => s.SchoolClass).HasMaxLength(100);
                entity.Property(s => s.SchoolLevel).HasMaxLength(100);
                entity.Property(s => s.StudyLevel).HasMaxLength(100);
                entity.Property(s => s.Notes).HasMaxLength(1000);

                entity.HasIndex(s => s.PhoneNumber).IsUnique().HasDatabaseName("UQ_Students_PhoneNumber");
                entity.HasIndex(s => s.Name).IsUnique().HasDatabaseName("UQ_Students_Name");
                entity.HasIndex(s => s.GroupId).HasDatabaseName("IX_Students_GroupId");

                entity.HasOne(s => s.Group)
                      .WithMany(g => g.Students)
                      .HasForeignKey(s => s.GroupId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Teacher
            modelBuilder.Entity<Teacher>(entity =>
            {
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Name).IsRequired().HasMaxLength(150);
                entity.Property(t => t.PhoneNumber).IsRequired().HasMaxLength(30);
                entity.Property(t => t.Password).IsRequired().HasMaxLength(200);
                entity.Property(t => t.FCMToken).HasMaxLength(500);

                entity.HasIndex(t => t.Name).IsUnique().HasDatabaseName("UQ_Teachers_Name");
                entity.HasIndex(t => t.Role).HasDatabaseName("IX_Teachers_Role");
            });

            // TeachersAttendances
            modelBuilder.Entity<TeacherAttendance>()
                .HasIndex(x => new { x.TeacherId, x.GroupId, x.Date })
                .IsUnique();

            modelBuilder.Entity<TeacherAttendance>()
                .HasOne(x => x.Teacher)
                .WithMany(t => t.Attendances)
                .HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TeacherAttendance>()
                .HasOne(x => x.Group)
                .WithMany(g => g.TeacherAttendances)
                .HasForeignKey(x => x.GroupId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TeacherAttendance>()
                .Property(x => x.Status)
                .HasConversion<int>();

            // TeachersSalaries
            modelBuilder.Entity<TeacherSalary>(builder =>
            {
                modelBuilder.Entity<TeacherSalary>()
                    .ToTable("TeacherSalaries", table =>
                    {
                        table.HasCheckConstraint(
                            "CK_TeacherSalary_Month_Range",
                            "[Month] >= 1 AND [Month] <= 12"
                        );

                        table.HasCheckConstraint(
                            "CK_TeacherSalary_Salary_Positive",
                            "[Salary] >= 0"
                        );
                    });
                builder.HasKey(x => x.Id);

                // Relationship
                builder.HasOne(x => x.Teacher)
                    .WithMany(t => t.Salaries)
                    .HasForeignKey(x => x.TeacherId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Prevent duplicate salary per teacher per month
                builder.HasIndex(x => new { x.TeacherId, x.GroupId, x.Year, x.Month })
                    .IsUnique();

                // Optional index for payroll batch queries
                builder.HasIndex(x => new { x.Year, x.Month });

                // Required fields
                builder.Property(x => x.Year)
                    .IsRequired();

                builder.Property(x => x.Month)
                    .IsRequired();

                builder.Property(x => x.Salary)
                    .IsRequired();

                builder.Property(x => x.IsPaid)
                    .HasDefaultValue(false);

                builder.Property(x => x.PaidAt)
                    .IsRequired(false);
            });

            // StudentAttendances
            modelBuilder.Entity<StudentAttendance>(entity =>
            {
                entity.HasKey(sa => sa.Id);
                entity.Property(sa => sa.Date).IsRequired();

                entity.HasOne(sa => sa.Student)
                      .WithMany(s => s.StudentAttendances)
                      .HasForeignKey(sa => sa.StudentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(sa => new { sa.StudentId, sa.Date }).IsUnique().HasDatabaseName("UQ_StudentAttendance_StudentId_Date");
            });

            modelBuilder.Entity<Prayer>(builder =>
            {
                builder.ToTable("Prayers");

                builder.HasKey(x => x.Id);

                builder.Property(x => x.Id)
                    .ValueGeneratedNever();

                builder.Property(x => x.NameAr)
                    .IsRequired()
                    .HasMaxLength(50);

                builder.Property(x => x.NameEn)
                    .IsRequired()
                    .HasMaxLength(50);

                builder.Property(x => x.DefaultRakats)
                    .IsRequired();

                builder.Property(x => x.DisplayOrder)
                    .IsRequired();

                builder.HasData(
                    new Prayer { Id = 1, NameAr = "الفجر", NameEn = "Fajr", DefaultRakats = 2, DisplayOrder = 1 },
                    new Prayer { Id = 2, NameAr = "الظهر", NameEn = "Dhuhr", DefaultRakats = 4, DisplayOrder = 2 },
                    new Prayer { Id = 3, NameAr = "العصر", NameEn = "Asr", DefaultRakats = 4, DisplayOrder = 3 },
                    new Prayer { Id = 4, NameAr = "المغرب", NameEn = "Maghrib", DefaultRakats = 3, DisplayOrder = 4 },
                    new Prayer { Id = 5, NameAr = "العشاء", NameEn = "Isha", DefaultRakats = 4, DisplayOrder = 5 }
                );

            });

            modelBuilder.Entity<StudentPrayerAttendance>(builder =>
            {
                builder.ToTable("StudentPrayerAttendances");

                builder.HasKey(x => x.Id);

                builder.Property(x => x.PrayerDate)
                    .HasColumnType("date")
                    .IsRequired();

                builder.Property(x => x.RakatsCount)
                    .IsRequired();

                builder.Property(x => x.IsInGroup)
                    .IsRequired();

                builder.Property(x => x.CreatedAt)
                    .HasDefaultValueSql("SYSDATETIME()");

                builder.HasIndex(x => new { x.PrayerDate, x.StudentId, x.PrayerId })
                    .IsUnique();

                builder.HasOne(x => x.Student)
                    .WithMany(x => x.Attendances)
                    .HasForeignKey(x => x.StudentId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(x => x.Prayer)
                    .WithMany(x => x.Attendances)
                    .HasForeignKey(x => x.PrayerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Get the current DateTime
            var currentDateTime = GetCurrentDateTime();

            // Iterate over the entries in the ChangeTracker
            foreach (var entry in ChangeTracker.Entries())
            {
                // Check if the entry is of type BaseEntity or a derived class
                if (entry.Entity is BaseEntity baseEntity)
                {
                    // Handle newly added entities (Added state)
                    if (entry.State == EntityState.Added)
                    {
                        baseEntity.CreatedAt = currentDateTime;
                        baseEntity.UpdatedAt = currentDateTime;
                    }
                    // Handle modified entities (Modified state)
                    else if (entry.State == EntityState.Modified)
                    {
                        baseEntity.UpdatedAt = currentDateTime;
                    }
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }




        // Helper method to get the current DateTime
        public static DateTime GetCurrentDateTime()
        {
            DateTime currentDateTime = DateTime.UtcNow.AddHours(3);

            return currentDateTime;
        }


        // Soft delete method
        public void SoftDelete<TEntity>(TEntity entity) where TEntity : BaseEntity
        {
            var entry = Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                Set<TEntity>().Attach(entity);
            }

            // Mark the entity as deleted by setting DeletedAt to current date
            entity.DeletedAt = GetCurrentDateTime();

            // Mark the entity as modified (so the DeletedAt is saved)
            entry.State = EntityState.Modified;
        }

        // Restore soft-deleted entity by setting DeletedAt to null
        public void RestoreEntity<TEntity>(TEntity entity) where TEntity : BaseEntity
        {
            var entry = Entry(entity);
            entity.DeletedAt = null;
            entry.State = EntityState.Modified;
        }

        private static LambdaExpression ConvertFilterExpression<T>(Expression<Func<T, bool>> filter, Type entityType)
        {
            var parameter = Expression.Parameter(entityType);
            var body = ReplacingExpressionVisitor.Replace(filter.Parameters.Single(), parameter, filter.Body);
            return Expression.Lambda(body, parameter);
        }
    }

}
