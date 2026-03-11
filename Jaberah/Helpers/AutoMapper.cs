using AutoMapper;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;
using static Jaberah.Models.DTOs.Students;
using static Jaberah.Models.DTOs.Teachers;

namespace Jaberah.Helpers
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            // Group mappings
            CreateMap<AddGroupDTO, Group>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.GroupName))
                .ForMember(dest => dest.TeacherId, opt => opt.MapFrom(src => src.TeacherId))
                .ForMember(dest => dest.Period, opt => opt.MapFrom(src => src.Period));

            CreateMap<Group, AddGroupDTO>()
                .ForMember(dest => dest.GroupName, opt => opt.MapFrom(src => src.Name));

            CreateMap<UpdateGroupDTO, Group>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.GroupName))
                .ForMember(dest => dest.TeacherId, opt => opt.MapFrom(src => src.TeacherId))
                .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));

            // Teacher mappings
            CreateMap<AddTeacherDTO, Teacher>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.TeacherName))
                .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber))
                .ForMember(dest => dest.Groups, opt => opt.Ignore())
                .ForMember(dest => dest.Password, opt => opt.Ignore())
                .ForMember(dest => dest.Role, opt => opt.Ignore())
                .ForMember(dest => dest.FCMToken, opt => opt.Ignore());

            CreateMap<Teacher, AddTeacherDTO>()
                .ForMember(dest => dest.TeacherName, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber))
                .ForMember(dest => dest.GroupsId, opt => opt.MapFrom(src => src.Groups != null ? src.Groups.Select(g => g.Id).ToList() : new List<int>()));

            CreateMap<UpdateTeacherDTO, Teacher>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.TeacherName))
                .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));

            // Student mappings
            CreateMap<AddStudentDTO, Student>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.StudentName))
                .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber))
                .ForMember(dest => dest.Group, opt => opt.Ignore());

            CreateMap<Student, AddStudentDTO>()
                .ForMember(dest => dest.StudentName, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.GroupId, opt => opt.MapFrom(src => src.GroupId));

            CreateMap<UpdateStudentDTO, Student>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.StudentName))
                .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));

            CreateMap<Exam, UpsertMonthlyExamsDTO>()
                .ReverseMap();

            CreateMap<Notification, NotificationsDTO>().ReverseMap();

            CreateMap<Book, UpsertBookDTO>().ReverseMap();
        }
    }
}
