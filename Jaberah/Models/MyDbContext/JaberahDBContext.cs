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
            // Teachers
            modelBuilder.Entity<Teacher>()
                .HasKey(t => t.Id);
            modelBuilder.Entity<Teacher>()
                .HasIndex(t => new { t.Role, t.TeacherName });

            // Teacher-Group Relationship (1:M)
            modelBuilder.Entity<Group>()
                .HasOne(g => g.Teacher)
                .WithMany(t => t.Groups)
                .HasForeignKey(g => g.TeacherId)
                .OnDelete(DeleteBehavior.SetNull);

            // Students
            modelBuilder.Entity<Student>()
                .HasKey(s => s.Id);
            modelBuilder.Entity<Student>()
                .HasIndex(s => new { s.StudentName, s.GroupId });

            // Student-Group Relationship (M:1)
            modelBuilder.Entity<Student>()
                .HasOne(s => s.Group)
                .WithMany(g => g.Students)
                .HasForeignKey(s => s.GroupId)
                .OnDelete(DeleteBehavior.SetNull);

            // TeachersAttendance (1:M)
            modelBuilder.Entity<TeachersAttendances>()
                .HasKey(ta => ta.Id);
            modelBuilder.Entity<TeachersAttendances>()
                .HasMany(ta => ta.TeachersAttendancesRows)
                .WithOne(tr => tr.TeachersAttendances)
                .HasForeignKey(ta => ta.TeacherAttendanceId)
                .OnDelete(DeleteBehavior.Cascade);

            // FollowStudentInMonth (M:1)
            modelBuilder.Entity<FollowStudentInMonth>()
                .HasOne(fsm => fsm.Student)
                .WithMany(s => s.FollowStudentInMonth)
                .HasForeignKey(fsm => fsm.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Exams
            modelBuilder.Entity<Exam>()
                .HasKey(e => e.Id);
            modelBuilder.Entity<Exam>()
                .HasOne(e => e.FollowStudentInMonth)
                .WithOne(fsm => fsm.Exams)
                .HasForeignKey<FollowStudentInMonth>()
                .OnDelete(DeleteBehavior.Cascade);

            // TeacherSalaries
            modelBuilder.Entity<TeachersSalaries>()
                .HasKey(ts => ts.Id);
            modelBuilder.Entity<TeachersSalaries>()
                .HasIndex(ts => ts.Date);

            // TeacherSalaryRow
            modelBuilder.Entity<TeachersSalariesRow>()
                .HasKey(tsr => tsr.Id);
            modelBuilder.Entity<TeachersSalariesRow>()
                .HasOne(tsr => tsr.Teacher)
                .WithMany(t => t.TeachersSalariesRow)
                .HasForeignKey(tsr => tsr.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<TeachersSalariesRow>()
                .HasOne(tsr => tsr.TeachersSalaries)
                .WithMany(ts => ts.TeachersSalariesRows)
                .HasForeignKey(tsr => tsr.TeachersSalariesId)
                .OnDelete(DeleteBehavior.Cascade);




            modelBuilder.Entity<FollowStudentInMonthRow>()
            .HasOne(fsmr => fsmr.WithTeacher)
            .WithMany()
            .HasForeignKey(fsmr => fsmr.WithTeacherId)
            .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<FollowStudentInMonthRow>()
                .HasOne(fsmr => fsmr.WithFriend)
                .WithMany()
                .HasForeignKey(fsmr => fsmr.WithFriendId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<WithTeacherFriend>()
                .HasOne(wtf => wtf.From)
                .WithMany()
                .HasForeignKey(wtf => wtf.FromId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<WithTeacherFriend>()
                .HasOne(wtf => wtf.To)
                .WithMany()
                .HasForeignKey(wtf => wtf.ToId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Surah>()
                .HasKey(s => s.Id);



        }
    }
}
