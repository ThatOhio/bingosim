using BingoSim.Application.DTOs;
using FluentValidation;

namespace BingoSim.Application.Validators;

public class ProgressGrantDtoValidator : AbstractValidator<ProgressGrantDto>
{
    public ProgressGrantDtoValidator()
    {
        RuleFor(x => x.DropKey)
            .NotEmpty()
            .WithMessage("Drop key is required.")
            .MaximumLength(200)
            .WithMessage("Drop key cannot exceed 200 characters.");

        When(x => x.UnitsMin.HasValue || x.UnitsMax.HasValue, () =>
        {
            RuleFor(x => x.UnitsMin)
                .NotNull()
                .WithMessage("UnitsMin is required when using variable units.");
            RuleFor(x => x.UnitsMax)
                .NotNull()
                .WithMessage("UnitsMax is required when using variable units.");
            RuleFor(x => x.UnitsMin!.Value)
                .GreaterThanOrEqualTo(1)
                .WithMessage("UnitsMin must be at least 1.")
                .When(x => x.UnitsMin.HasValue);
            RuleFor(x => x.UnitsMax!.Value)
                .GreaterThanOrEqualTo(1)
                .WithMessage("UnitsMax must be at least 1.")
                .When(x => x.UnitsMax.HasValue);
            RuleFor(x => x)
                .Must(x => x.UnitsMin!.Value <= x.UnitsMax!.Value)
                .WithMessage("UnitsMax must be greater than or equal to UnitsMin.")
                .When(x => x.UnitsMin.HasValue && x.UnitsMax.HasValue);
        }).Otherwise(() =>
        {
            RuleFor(x => x.Units)
                .GreaterThanOrEqualTo(1)
                .WithMessage("Units must be at least 1.");
        });
    }
}
