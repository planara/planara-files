using FluentValidation;

namespace Planara.Files.Validators;

public class GuidValidator : AbstractValidator<Guid>
{
    public GuidValidator()
    {
        RuleFor(x => x)
            .NotEmpty()
            .WithMessage("ID является обязательным.");
    }
}