using BingoSim.Application.DTOs;
using FluentValidation;

namespace BingoSim.Application.Validators;

public class ScheduledSessionDtoValidator : AbstractValidator<ScheduledSessionDto>
{
    public ScheduledSessionDtoValidator()
    {
        RuleFor(x => x.DayOfWeek)
            .IsInEnum()
            .WithMessage("Day of week must be a valid day.");

        RuleFor(x => x.DurationMinutes)
            .GreaterThan(0)
            .WithMessage("Duration must be greater than zero.")
            .LessThanOrEqualTo(1440)
            .WithMessage("Duration cannot exceed 24 hours (1440 minutes).");
    }
}
