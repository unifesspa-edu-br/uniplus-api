namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;

using FluentValidation;

/// <summary>
/// Antecipa (422) os tamanhos na atualização. O <c>Codigo</c> é imutável e não é
/// campo do comando. A fase típica é orientativa (não validada contra o cadastro de
/// fases), apenas limitada em tamanho.
/// </summary>
public sealed class AtualizarTipoBancaCommandValidator : AbstractValidator<AtualizarTipoBancaCommand>
{
    public AtualizarTipoBancaCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id do tipo de banca é obrigatório.");

        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Nome do tipo de banca é obrigatório.")
            .MaximumLength(200).WithMessage("Nome do tipo de banca deve ter no máximo 200 caracteres.");

        RuleFor(x => x.FaseTipica)
            .MaximumLength(60).WithMessage("Fase típica do tipo de banca deve ter no máximo 60 caracteres.")
            .When(x => x.FaseTipica is not null);

        RuleFor(x => x.Descricao)
            .MaximumLength(300).WithMessage("Descrição do tipo de banca deve ter no máximo 300 caracteres.")
            .When(x => x.Descricao is not null);
    }
}
