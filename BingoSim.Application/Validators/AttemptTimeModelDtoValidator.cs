using BingoSim.Application.DTOs;
using FluentValidation;

namespace BingoSim.Application.Validators;

public class AttemptTimeModelDtoValidator : AbstractValidator<AttemptTimeModelDto>
{
    public AttemptTimeModelDtoValidator()
    {
        RuleFor(x => x.BaselineTimeSeconds)
            .GreaterThan(0)
            .WithMessage("Baseline time must be greater than zero.");

        RuleFor(x => x.Distribution)
            .Must(d => Enum.IsDefined(typeof(Core.Enums.TimeDistribution), d))
            .WithMessage("Distribution must be a valid TimeDistribution value (0=Uniform, 1=NormalApprox, 2=Custom).");

        RuleFor(x => x.VarianceSeconds)
            .GreaterThanOrEqualTo(0)
            .When(x => x.VarianceSeconds.HasValue)
            .WithMessage("Variance cannot be negative.");
    }
}
