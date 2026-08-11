namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;

using FluentValidation;

/// <summary>Antecipa as invariantes de fronteira; o agregado repete a defesa.</summary>
public sealed class CriarTipoEtapaCommandValidator : AbstractValidator<CriarTipoEtapaCommand>
{
    private const char CaractereNulo = (char)0;

    public CriarTipoEtapaCommandValidator()
    {
        RuleFor(command => command.Codigo)
            .NotEmpty().WithMessage("Código do tipo de etapa é obrigatório.")
            .MaximumLength(64).WithMessage("Código do tipo de etapa deve ter no máximo 64 caracteres.")
            .Must(static codigo => codigo is null || !codigo.Contains(CaractereNulo))
            .WithMessage("Código do tipo de etapa não pode conter o caractere nulo (U+0000).");
        RuleFor(command => command.Nome)
            .NotEmpty().WithMessage("Nome do tipo de etapa é obrigatório.")
            .MaximumLength(200).WithMessage("Nome do tipo de etapa deve ter no máximo 200 caracteres.")
            .Must(static nome => nome is null || !nome.Contains(CaractereNulo))
            .WithMessage("Nome do tipo de etapa não pode conter o caractere nulo (U+0000).");
        RuleFor(command => command.Descricao)
            .MaximumLength(1000).WithMessage("Descrição do tipo de etapa deve ter no máximo 1000 caracteres.")
            .Must(static descricao => descricao is null || !descricao.Contains(CaractereNulo))
            .WithMessage("Descrição do tipo de etapa não pode conter o caractere nulo (U+0000).")
            .When(command => command.Descricao is not null);
    }
}
