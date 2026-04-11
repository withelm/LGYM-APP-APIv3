using LgymApi.Application.Repositories;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Plan;

public sealed partial class PlanService : IPlanService
{
    private readonly IUserRepository _userRepository;
    private readonly IPlanRepository _planRepository;
    private readonly IPlanDayRepository _planDayRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PlanService(IUserRepository userRepository, IPlanRepository planRepository, IPlanDayRepository planDayRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _planRepository = planRepository;
        _planDayRepository = planDayRepository;
        _unitOfWork = unitOfWork;
    }
}
