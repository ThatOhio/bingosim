using BingoSim.Application.DTOs;
using FluentValidation;

namespace BingoSim.Application.Validators;

public class TileActivityRuleDtoValidator : AbstractValidator<TileActivityRuleDto>
{
    public TileActivityRuleDtoValidator()
    {
        RuleFor(x => x.ActivityDefinitionId)
            .NotEmpty()
            .WithMessage("Activity definition is required.");

        RuleFor(x => x.AcceptedDropKeys)
            .NotNull()
            .WithMessage("AcceptedDropKeys cannot be null.");

        RuleFor(x => x.Requirements)
            .NotNull()
            .WithMessage("Requirements cannot be null.");

        RuleForEach(x => x.Requirements)
            .SetValidator(new CapabilityDtoValidator());

        RuleFor(x => x.Modifiers)
            .NotNull()
            .WithMessage("Modifiers cannot be null.");

        RuleForEach(x => x.Modifiers)
            .SetValidator(new ActivityModifierRuleDtoValidator());
    }
}
