using BingoSim.Application.DTOs;
using FluentValidation;

namespace BingoSim.Application.Validators;

public class ActivityOutcomeDefinitionDtoValidator : AbstractValidator<ActivityOutcomeDefinitionDto>
{
    public ActivityOutcomeDefinitionDtoValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty()
            .WithMessage("Outcome key is required.")
            .MaximumLength(100)
            .WithMessage("Outcome key cannot exceed 100 characters.");

        RuleFor(x => x.WeightNumerator)
            .GreaterThan(0)
            .WithMessage("Weight numerator must be greater than zero.");

        RuleFor(x => x.WeightDenominator)
            .GreaterThan(0)
            .WithMessage("Weight denominator must be greater than zero.");

        RuleFor(x => x.Grants)
            .NotNull()
            .WithMessage("Grants list cannot be null.");

        RuleForEach(x => x.Grants)
            .SetValidator(new ProgressGrantDtoValidator());
    }
}
