using BingoSim.Application.DTOs;
using FluentValidation;

namespace BingoSim.Application.Validators;

public class UpdatePlayerProfileRequestValidator : AbstractValidator<UpdatePlayerProfileRequest>
{
    public UpdatePlayerProfileRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Player name is required.")
            .MaximumLength(100)
            .WithMessage("Player name cannot exceed 100 characters.");

        RuleFor(x => x.SkillTimeMultiplier)
            .GreaterThan(0)
            .WithMessage("Skill time multiplier must be greater than zero.")
            .LessThanOrEqualTo(10)
            .WithMessage("Skill time multiplier cannot exceed 10.");

        RuleFor(x => x.Capabilities)
            .NotNull()
            .WithMessage("Capabilities list cannot be null.");

        RuleForEach(x => x.Capabilities)
            .SetValidator(new CapabilityDtoValidator());

        RuleFor(x => x.WeeklySchedule)
            .NotNull()
            .WithMessage("Weekly schedule is required.")
            .SetValidator(new WeeklyScheduleDtoValidator());
    }
}
