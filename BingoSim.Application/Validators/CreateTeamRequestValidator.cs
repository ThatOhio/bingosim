using BingoSim.Application.DTOs;
using BingoSim.Application.StrategyKeys;
using FluentValidation;

namespace BingoSim.Application.Validators;

public class CreateTeamRequestValidator : AbstractValidator<CreateTeamRequest>
{
    public CreateTeamRequestValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty()
            .WithMessage("Team must belong to an event.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Team name is required.")
            .MaximumLength(200)
            .WithMessage("Team name cannot exceed 200 characters.");

        RuleFor(x => x.PlayerProfileIds)
            .NotNull()
            .WithMessage("Player list cannot be null.");

        RuleFor(x => x.PlayerProfileIds)
            .Must(ids => ids is null || ids.Distinct().Count() == ids!.Count)
            .WithMessage("A player cannot be assigned more than once to the same team.");

        RuleFor(x => x.StrategyKey)
            .NotEmpty()
            .WithMessage("Strategy is required.")
            .Must(StrategyCatalog.IsSupported)
            .WithMessage("Strategy must be one of the supported keys: " + string.Join(", ", StrategyCatalog.GetSupportedKeys()));

        RuleFor(x => x.ParamsJson)
            .MaximumLength(8000)
            .When(x => !string.IsNullOrEmpty(x.ParamsJson));
    }
}
