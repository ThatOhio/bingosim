using BingoSim.Application.DTOs;
using FluentValidation;

namespace BingoSim.Application.Validators;

public class GroupSizeBandDtoValidator : AbstractValidator<GroupSizeBandDto>
{
    public GroupSizeBandDtoValidator()
    {
        RuleFor(x => x.MinSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Min size must be at least 1.");

        RuleFor(x => x.MaxSize)
            .GreaterThanOrEqualTo(x => x.MinSize)
            .WithMessage("Max size cannot be less than min size.");

        RuleFor(x => x.TimeMultiplier)
            .GreaterThan(0)
            .WithMessage("Time multiplier must be greater than zero.");

        RuleFor(x => x.ProbabilityMultiplier)
            .GreaterThan(0)
            .WithMessage("Probability multiplier must be greater than zero.");
    }
}
