using BingoSim.Application.DTOs;
using FluentValidation;

namespace BingoSim.Application.Validators;

public class TileDtoValidator : AbstractValidator<TileDto>
{
    public TileDtoValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty()
            .WithMessage("Tile key is required.")
            .MaximumLength(100)
            .WithMessage("Tile key cannot exceed 100 characters.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Tile name is required.")
            .MaximumLength(200)
            .WithMessage("Tile name cannot exceed 200 characters.");

        RuleFor(x => x.Points)
            .InclusiveBetween(1, 4)
            .WithMessage("Points must be 1, 2, 3, or 4.");

        RuleFor(x => x.RequiredCount)
            .GreaterThanOrEqualTo(1)
            .WithMessage("RequiredCount must be at least 1.");

        RuleFor(x => x.AllowedActivities)
            .NotNull()
            .WithMessage("AllowedActivities cannot be null.")
            .NotEmpty()
            .WithMessage("At least one TileActivityRule is required.");

        RuleForEach(x => x.AllowedActivities)
            .SetValidator(new TileActivityRuleDtoValidator());
    }
}
