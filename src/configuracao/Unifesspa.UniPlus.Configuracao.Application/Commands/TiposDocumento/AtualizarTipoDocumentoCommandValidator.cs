namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;

using FluentValidation;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public sealed class AtualizarTipoDocumentoCommandValidator
    : AbstractValidator<AtualizarTipoDocumentoCommand>
{
    public AtualizarTipoDocumentoCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id do tipo de documento é obrigatório.");

        RuleFor(x => x.Codigo)
            .NotEmpty().WithMessage("Código do tipo de documento é obrigatório.")
            .MaximumLength(60).WithMessage("Código do tipo de documento deve ter no máximo 60 caracteres.");

        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Nome do tipo de documento é obrigatório.")
            .MaximumLength(200).WithMessage("Nome do tipo de documento deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Categoria)
            .Must(CategoriaDocumentos.EhValido)
            .WithMessage($"Categoria do tipo de documento deve ser uma de: {string.Join(", ", CategoriaDocumentos.TokensCanonicos)}.");

        RuleFor(x => x.Descricao)
            .MaximumLength(1000).WithMessage("Descrição do tipo de documento deve ter no máximo 1000 caracteres.")
            .When(x => x.Descricao is not null);

        RuleFor(x => x.FormatosAceitos)
            .MaximumLength(200).WithMessage("Formatos aceitos devem ter no máximo 200 caracteres.")
            .When(x => x.FormatosAceitos is not null);

        RuleFor(x => x.TamanhoMaximoMb)
            .GreaterThan(0).WithMessage("Tamanho máximo em MB, quando informado, deve ser positivo.")
            .When(x => x.TamanhoMaximoMb.HasValue);

        RuleFor(x => x.TipoEquivalente)
            .MaximumLength(60).WithMessage("Tipo equivalente deve ter no máximo 60 caracteres.")
            .When(x => x.TipoEquivalente is not null);

        RuleFor(x => x)
            .Must(NaoSerEquivalenteASiMesmo)
            .WithMessage("Um tipo de documento não pode declarar-se equivalente a si mesmo.")
            .When(x => !string.IsNullOrWhiteSpace(x.TipoEquivalente));
    }

    private static bool NaoSerEquivalenteASiMesmo(AtualizarTipoDocumentoCommand command) =>
        !string.Equals(command.TipoEquivalente?.Trim(), command.Codigo?.Trim(), StringComparison.Ordinal);
}
