using AutoMapper;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using static Jaberah.Models.DTOs.Students;
using static Jaberah.Models.DTOs.Teachers;

namespace Jaberah.Helpers
{
    public class AutoMapper : Profile
    {
        public AutoMapper()
        {
            CreateMap<Group, AddGroupDTO>().ReverseMap();

            CreateMap<Teacher, AddTeacherDTO>()
                .ForMember(x => x.GroupsId, y => y.Ignore())
                .ReverseMap();

            CreateMap<Student, AddStudentDTO>()
                .ReverseMap();

            CreateMap<Exam, UpsertMonthlyExamsDTO>()
                .ReverseMap();

            CreateMap<Notification, NotificationsDTO>().ReverseMap();

            CreateMap<Book, UpsertBookDTO>().ReverseMap();
        }
    }
}
