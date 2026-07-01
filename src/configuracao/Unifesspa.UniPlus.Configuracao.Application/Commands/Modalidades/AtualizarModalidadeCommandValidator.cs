namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using FluentValidation;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Antecipa (422) o domínio fechado dos enums e os tamanhos na atualização. O
/// <c>Codigo</c> é imutável e não é campo do comando. A coerência e a integridade
/// referencial ficam no agregado e no handler.
/// </summary>
public sealed class AtualizarModalidadeCommandValidator : AbstractValidator<AtualizarModalidadeCommand>
{
    public AtualizarModalidadeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id da modalidade é obrigatório.");

        RuleFor(x => x.Descricao)
            .MaximumLength(300).WithMessage("Descrição da modalidade deve ter no máximo 300 caracteres.")
            .When(x => x.Descricao is not null);

        RuleFor(x => x.NaturezaLegal)
            .Must(NaturezasLegais.EhValido)
            .WithMessage($"Natureza legal deve ser uma de: {string.Join(", ", NaturezasLegais.TokensCanonicos)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.NaturezaLegal));

        RuleFor(x => x.ComposicaoVagas)
            .Must(ComposicoesVagas.EhValido)
            .WithMessage($"Composição de vagas deve ser uma de: {string.Join(", ", ComposicoesVagas.TokensCanonicos)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.ComposicaoVagas));

        RuleFor(x => x.RegraRemanejamento)
            .Must(RegrasRemanejamento.EhValido)
            .WithMessage($"Regra de remanejamento deve ser uma de: {string.Join(", ", RegrasRemanejamento.TokensCanonicos)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.RegraRemanejamento));

        RuleFor(x => x.AcaoQuandoIndeferido)
            .Must(AcoesQuandoIndeferido.EhValido)
            .WithMessage($"Ação quando indeferido deve ser uma de: {string.Join(", ", AcoesQuandoIndeferido.TokensCanonicos)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.AcaoQuandoIndeferido));

        RuleFor(x => x.ComposicaoOrigem)
            .MaximumLength(60).WithMessage("Código de origem da composição deve ter no máximo 60 caracteres.")
            .When(x => x.ComposicaoOrigem is not null);

        RuleFor(x => x.BaseLegal)
            .MaximumLength(500).WithMessage("Base legal da modalidade deve ter no máximo 500 caracteres.")
            .When(x => x.BaseLegal is not null);
    }
}
