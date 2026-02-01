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

        RuleFor(x => x.Units)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Units must be at least 1.");
    }
}
