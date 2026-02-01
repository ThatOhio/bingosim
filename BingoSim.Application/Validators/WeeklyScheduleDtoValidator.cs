using BingoSim.Application.DTOs;
using FluentValidation;

namespace BingoSim.Application.Validators;

public class WeeklyScheduleDtoValidator : AbstractValidator<WeeklyScheduleDto>
{
    public WeeklyScheduleDtoValidator()
    {
        RuleFor(x => x.Sessions)
            .NotNull()
            .WithMessage("Sessions list cannot be null.");

        RuleForEach(x => x.Sessions)
            .SetValidator(new ScheduledSessionDtoValidator());
    }
}
