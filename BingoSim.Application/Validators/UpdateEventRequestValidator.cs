using BingoSim.Application.DTOs;
using FluentValidation;

namespace BingoSim.Application.Validators;

public class UpdateEventRequestValidator : AbstractValidator<UpdateEventRequest>
{
    public UpdateEventRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Event name is required.")
            .MaximumLength(200)
            .WithMessage("Event name cannot exceed 200 characters.");

        RuleFor(x => x.Duration)
            .GreaterThan(TimeSpan.Zero)
            .WithMessage("Duration must be greater than zero.");

        RuleFor(x => x.UnlockPointsRequiredPerRow)
            .GreaterThanOrEqualTo(0)
            .WithMessage("UnlockPointsRequiredPerRow cannot be negative.");

        RuleFor(x => x.Rows)
            .NotNull()
            .WithMessage("Rows cannot be null.");

        RuleForEach(x => x.Rows)
            .SetValidator(new RowDtoValidator());

        RuleFor(x => x)
            .Must(request =>
            {
                if (request.Rows is null) return true;
                var allKeys = request.Rows.SelectMany(r => r.Tiles.Select(t => t.Key)).ToList();
                return allKeys.Distinct(StringComparer.Ordinal).Count() == allKeys.Count;
            })
            .WithMessage("Tile keys must be unique within the event.");
    }
}
