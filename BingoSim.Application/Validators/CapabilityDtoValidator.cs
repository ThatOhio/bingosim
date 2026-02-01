using BingoSim.Application.DTOs;
using FluentValidation;

namespace BingoSim.Application.Validators;

public class CapabilityDtoValidator : AbstractValidator<CapabilityDto>
{
    public CapabilityDtoValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty()
            .WithMessage("Capability key is required.")
            .MaximumLength(100)
            .WithMessage("Capability key cannot exceed 100 characters.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Capability name is required.")
            .MaximumLength(200)
            .WithMessage("Capability name cannot exceed 200 characters.");
    }
}
