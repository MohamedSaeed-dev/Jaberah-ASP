using Jaberah.Models.JaberahModels;
using Microsoft.EntityFrameworkCore;

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
        public DbSet<TeachersAttendances> TeacherAttendances { get; set; }
        public DbSet<TeachersAttendancesRow> TeacherAttendanceRows { get; set; }
        public DbSet<FollowStudentInMonth> FollowStudentsInMonth { get; set; }
        public DbSet<FollowStudentInMonthRow> FollowStudentInMonthRows { get; set; }
        public DbSet<Exam> Exams { get; set; }
        public DbSet<TeachersSalaries> TeacherSalaries { get; set; }
        public DbSet<TeachersSalariesRow> TeacherSalariesRows { get; set; }
        public DbSet<WithTeacherFriend> WithTeacherFriends { get; set; }
        public DbSet<Surah> Surahs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Exam
            modelBuilder.Entity<Exam>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.FollowStudentInMonth)
                      .WithOne(f => f.Exams)
                      .HasForeignKey<Exam>(e => e.FollowStudentInMonthId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // FollowStudentInMonth
            modelBuilder.Entity<FollowStudentInMonth>(entity =>
            {
                entity.HasKey(f => f.Id);
                entity.Property(f => f.Date).IsRequired();

                entity.HasOne(f => f.Student)
                      .WithMany(s => s.FollowStudentInMonth)
                      .HasForeignKey(f => f.StudentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(f => f.FollowStudentInMonthRows)
                      .WithOne(row => row.FollowStudentInMonth)
                      .HasForeignKey(row => row.FollowStudentInMonthId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // FollowStudentInMonthRow
            modelBuilder.Entity<FollowStudentInMonthRow>(entity =>
            {
                entity.HasKey(row => row.Id);

                entity.HasOne(row => row.WithTeacher)
                      .WithMany()
                      .HasForeignKey(row => row.WithTeacherId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(row => row.WithFriend)
                      .WithMany()
                      .HasForeignKey(row => row.WithFriendId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(row => row.FollowStudentInMonth)
                      .WithMany(f => f.FollowStudentInMonthRows)
                      .HasForeignKey(row => row.FollowStudentInMonthId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // WithTeacherFriend
            modelBuilder.Entity<WithTeacherFriend>(entity =>
            {
                entity.HasKey(wtf => wtf.Id);

                entity.HasOne(wtf => wtf.From)
                      .WithMany()
                      .HasForeignKey(wtf => wtf.FromId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(wtf => wtf.To)
                      .WithMany()
                      .HasForeignKey(wtf => wtf.ToId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Surah
            modelBuilder.Entity<Surah>(entity =>
            {
                entity.HasKey(s => s.Id);
            });

            // Group
            modelBuilder.Entity<Group>(entity =>
            {
                entity.HasKey(g => g.Id);
                entity.Property(g => g.GroupName).IsRequired().HasMaxLength(50);

                entity.HasOne(g => g.Teacher)
                      .WithMany(t => t.Groups)
                      .HasForeignKey(g => g.TeacherId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasMany(g => g.Students)
                      .WithOne(s => s.Group)
                      .HasForeignKey(s => s.GroupId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Notification
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(n => n.Id);
                entity.Property(n => n.Message).IsRequired();
                entity.Property(n => n.CreatedAt).IsRequired();
            });

            // Student
            modelBuilder.Entity<Student>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.StudentName).IsRequired().HasMaxLength(100);
                entity.Property(s => s.PhoneNumber).HasMaxLength(20);
            });

            // Teacher
            modelBuilder.Entity<Teacher>(entity =>
            {
                entity.HasKey(t => t.Id);
                entity.Property(t => t.TeacherName).IsRequired().HasMaxLength(100);
                entity.Property(t => t.PhoneNumber).HasMaxLength(20);
                entity.Property(t => t.Password).IsRequired().HasMaxLength(200);
            });

            // TeachersAttendances
            modelBuilder.Entity<TeachersAttendances>(entity =>
            {
                entity.HasKey(ta => ta.Id);
                entity.Property(ta => ta.Date).IsRequired();

                entity.HasMany(ta => ta.TeachersAttendancesRows)
                      .WithOne(row => row.TeachersAttendances)
                      .HasForeignKey(row => row.TeacherAttendanceId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // TeachersAttendancesRow
            modelBuilder.Entity<TeachersAttendancesRow>(entity =>
            {
                entity.HasKey(row => row.Id);

                entity.HasOne(row => row.Teacher)
                      .WithMany(t => t.TeachersAttendancesRow)
                      .HasForeignKey(row => row.TeacherId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // TeachersSalaries
            modelBuilder.Entity<TeachersSalaries>(entity =>
            {
                entity.HasKey(ts => ts.Id);
                entity.Property(ts => ts.Date).IsRequired();

                entity.HasMany(ts => ts.TeachersSalariesRows)
                      .WithOne(row => row.TeachersSalaries)
                      .HasForeignKey(row => row.TeachersSalariesId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // TeachersSalariesRow
            modelBuilder.Entity<TeachersSalariesRow>(entity =>
            {
                entity.HasKey(row => row.Id);

                entity.HasOne(row => row.Teacher)
                      .WithMany(t => t.TeachersSalariesRow)
                      .HasForeignKey(row => row.TeacherId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }


    }

}
