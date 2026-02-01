using BingoSim.Application.DTOs;
using FluentValidation;

namespace BingoSim.Application.Validators;

public class ActivityAttemptDefinitionDtoValidator : AbstractValidator<ActivityAttemptDefinitionDto>
{
    public ActivityAttemptDefinitionDtoValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty()
            .WithMessage("Attempt key is required.")
            .MaximumLength(100)
            .WithMessage("Attempt key cannot exceed 100 characters.");

        RuleFor(x => x.RollScope)
            .Must(r => Enum.IsDefined(typeof(Core.Enums.RollScope), r))
            .WithMessage("RollScope must be 0 (PerPlayer) or 1 (PerGroup).");

        RuleFor(x => x.TimeModel)
            .NotNull()
            .WithMessage("Time model is required.")
            .SetValidator(new AttemptTimeModelDtoValidator());

        RuleFor(x => x.Outcomes)
            .NotNull()
            .WithMessage("Outcomes list cannot be null.")
            .NotEmpty()
            .WithMessage("At least one outcome is required.");

        RuleForEach(x => x.Outcomes)
            .SetValidator(new ActivityOutcomeDefinitionDtoValidator());
    }
}
