namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using FluentValidation;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Antecipa (422) o formato do código, o domínio fechado dos enums e os tamanhos —
/// simétrico ao domínio (#589). A coerência natureza↔remanejamento, a equivalência
/// RetiraDe⟺origem, os args por regra e a integridade referencial ficam no agregado
/// e no handler.
/// </summary>
public sealed class CriarModalidadeCommandValidator : AbstractValidator<CriarModalidadeCommand>
{
    public CriarModalidadeCommandValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty().WithMessage("Código da modalidade é obrigatório.")
            .Must(CodigoModalidade.EhValido)
            .WithMessage("Código da modalidade deve conter apenas letras maiúsculas, dígitos e "
                + "sublinhado (sem hífen), com no máximo 60 caracteres.");

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
