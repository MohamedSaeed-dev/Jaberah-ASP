using AutoMapper;
using Jaberah.Models.DTOs;
using Jaberah.Models.JaberahModels;

namespace Jaberah.Helpers
{
    public class AutoMapper : Profile
    {
        public AutoMapper()
        {
            CreateMap<Group, AddGroupDTO>()
                .ForMember(x => x.GroupName, y => y.MapFrom(z => z.GroupName))
                .ForMember(x => x.Period, y => y.MapFrom(z => z.Period)).ReverseMap();

            CreateMap<Notification, NotificationsDTO>()
                .ForMember(x => x.Message, y => y.MapFrom(z => z.Message)).ReverseMap();
        }
    }
}
