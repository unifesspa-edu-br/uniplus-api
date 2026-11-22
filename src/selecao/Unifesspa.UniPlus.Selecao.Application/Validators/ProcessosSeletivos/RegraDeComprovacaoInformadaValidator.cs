namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using Domain.Enums;

using FluentValidation;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Confere os dados informados para cada regra de comprovação.</summary>
public sealed class RegraDeComprovacaoInformadaValidator : AbstractValidator<RegraDeComprovacaoInformada>
{
    public RegraDeComprovacaoInformadaValidator()
    {
        RegrasDoDocumento();
        RegrasDosItens();
        RegrasDaDistincao();
        RegrasDaConsequencia();
        RegrasDasBasesLegaisDoGrupo();
    }

    private static FormaDeComprovacao? FormaDe(RegraDeComprovacaoInformada regra) =>
        CatalogoDe<FormaDeComprovacao>.TentarObter(regra.Forma, out FormaDeComprovacao forma) ? forma : null;

    private void RegrasDoDocumento()
    {
        RuleFor(x => x.Documento!)
            .SetValidator(new ItemDocumentoExigidoInputValidator())
            .When(static x => FormaDe(x) == FormaDeComprovacao.Documento && x.Documento is not null);
    }

    private void RegrasDosItens()
    {
        RuleForEach(x => x.Itens)
            .SetValidator(this)
            .When(static x => x.Itens is { Count: > 0 });
    }

    private void RegrasDaDistincao()
    {
        RuleFor(x => x.Cardinalidade!.Distincao!)
            .SetValidator(new DistincaoForaDaRegraDeDocumentoValidator())
            .When(static x => FormaDe(x) is FormaDeComprovacao.TodosOsItens or FormaDeComprovacao.QuantidadeMinimaDeItens && x.Cardinalidade?.Distincao is not null);

    }

    private void RegrasDaConsequencia() =>
        RuleFor(x => x.Indeferimento!.Consequencia)
            .Null()
            .When(static x => FormaDe(x) == FormaDeComprovacao.Documento && x.Indeferimento is not null)
            .WithMessage("A consequência própria só é permitida no grupo que exige uma quantidade mínima de itens.");

    private void RegrasDasBasesLegaisDoGrupo() =>
        RuleFor(x => x.Indeferimento!.BasesLegais)
            .Must(static basesLegais => basesLegais is null or { Count: 0 })
            .When(static x => FormaDe(x) == FormaDeComprovacao.Documento && x.Indeferimento is not null)
            .WithMessage("A base legal própria só é permitida no grupo que exige uma quantidade mínima de itens e declara consequência.");

}

/// <summary>Confere que grupos não declaram distinção de apresentações.</summary>
public sealed class DistincaoForaDaRegraDeDocumentoValidator : AbstractValidator<DistincaoDasApresentacoesInput>
{
    public DistincaoForaDaRegraDeDocumentoValidator()
    {
        RuleFor(x => x.CriterioDeDistincao)
            .Null()
            .WithMessage("O critério de distinção só é permitido em regra de documento.");
        RuleFor(x => x.DataReferencia)
            .Null()
            .WithMessage("A data de referência só é permitida em regra de documento.");
        RuleFor(x => x.OcorrenciasEsperadas)
            .Must(static ocorrencias => ocorrencias is null or { Count: 0 })
            .WithMessage("As ocorrências esperadas só são permitidas em regra de documento.");
    }
}
