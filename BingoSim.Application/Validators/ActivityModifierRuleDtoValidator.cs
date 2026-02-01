using BingoSim.Application.DTOs;
using FluentValidation;

namespace BingoSim.Application.Validators;

public class ActivityModifierRuleDtoValidator : AbstractValidator<ActivityModifierRuleDto>
{
    public ActivityModifierRuleDtoValidator()
    {
        RuleFor(x => x.Capability)
            .NotNull()
            .WithMessage("Capability is required.")
            .SetValidator(new CapabilityDtoValidator());

        RuleFor(x => x)
            .Must(x => x.TimeMultiplier.HasValue || x.ProbabilityMultiplier.HasValue)
            .WithMessage("At least one of TimeMultiplier or ProbabilityMultiplier must be set.");

        When(x => x.TimeMultiplier.HasValue, () =>
        {
            RuleFor(x => x.TimeMultiplier!.Value)
                .GreaterThan(0)
                .WithMessage("Time multiplier must be greater than zero.");
        });

        When(x => x.ProbabilityMultiplier.HasValue, () =>
        {
            RuleFor(x => x.ProbabilityMultiplier!.Value)
                .GreaterThan(0)
                .WithMessage("Probability multiplier must be greater than zero.");
        });
    }
}
