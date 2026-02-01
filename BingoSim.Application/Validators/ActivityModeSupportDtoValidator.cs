using BingoSim.Application.DTOs;
using FluentValidation;

namespace BingoSim.Application.Validators;

public class ActivityModeSupportDtoValidator : AbstractValidator<ActivityModeSupportDto>
{
    public ActivityModeSupportDtoValidator()
    {
        RuleFor(x => x.MinGroupSize)
            .GreaterThanOrEqualTo(1)
            .When(x => x.MinGroupSize.HasValue)
            .WithMessage("Min group size must be at least 1.");

        RuleFor(x => x.MaxGroupSize)
            .GreaterThanOrEqualTo(1)
            .When(x => x.MaxGroupSize.HasValue)
            .WithMessage("Max group size must be at least 1.");

        RuleFor(x => x.MaxGroupSize)
            .GreaterThanOrEqualTo(x => x.MinGroupSize ?? 0)
            .When(x => x.MinGroupSize.HasValue && x.MaxGroupSize.HasValue)
            .WithMessage("Max group size cannot be less than min group size.");
    }
}
