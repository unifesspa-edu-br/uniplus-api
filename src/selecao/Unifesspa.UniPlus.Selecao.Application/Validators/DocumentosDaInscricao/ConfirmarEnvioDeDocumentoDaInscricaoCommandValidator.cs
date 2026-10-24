namespace Unifesspa.UniPlus.Selecao.Application.Validators.DocumentosDaInscricao;

using Commands.DocumentosDaInscricao;

using FluentValidation;

public sealed class ConfirmarEnvioDeDocumentoDaInscricaoCommandValidator : AbstractValidator<ConfirmarEnvioDeDocumentoDaInscricaoCommand>
{
    public const int ETagMaxLength = 256;

    public ConfirmarEnvioDeDocumentoDaInscricaoCommandValidator()
    {
        RuleFor(x => x.ETag)
            .MaximumLength(ETagMaxLength)
            .WithMessage($"O ETag deve ter no máximo {ETagMaxLength} caracteres.");
    }
}
