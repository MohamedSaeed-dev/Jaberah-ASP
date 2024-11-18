using AutoMapper;
using Jaberah.Models.MyDbContext;
using Microsoft.AspNetCore.Mvc;

namespace Jaberah.Controllers
{
    [Route("api/follow-student-in-month")]
    [ApiController]
    public class FollowStudentInMonthController : ControllerBase
    {
        private readonly JaberahDBContext _db;
        private readonly IMapper _mapper;
        public FollowStudentInMonthController(JaberahDBContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }
    }
}
