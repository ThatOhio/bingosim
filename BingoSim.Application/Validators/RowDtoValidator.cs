using BingoSim.Application.DTOs;
using FluentValidation;

namespace BingoSim.Application.Validators;

public class RowDtoValidator : AbstractValidator<RowDto>
{
    public RowDtoValidator()
    {
        RuleFor(x => x.Index)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Row index cannot be negative.");

        RuleFor(x => x.Tiles)
            .NotNull()
            .WithMessage("Tiles cannot be null.")
            .Must(tiles => tiles?.Count == 4)
            .WithMessage("Row must contain exactly 4 tiles.");

        When(x => x.Tiles is { Count: 4 }, () =>
        {
            RuleFor(x => x.Tiles)
                .Must(tiles =>
                {
                    var points = tiles!.Select(t => t.Points).OrderBy(p => p).ToList();
                    return points.SequenceEqual([1, 2, 3, 4]);
                })
                .WithMessage("Row must contain exactly one tile per point value 1, 2, 3, 4.");
        });

        RuleForEach(x => x.Tiles)
            .SetValidator(new TileDtoValidator());
    }
}
