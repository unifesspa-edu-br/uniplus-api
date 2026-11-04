namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A divulgação, a taxa, a localidade, a contagem de prazo, o calendário e o identificador legível lidos do envelope.
/// </summary>
internal sealed record BlocosDaPublicacao
{
    public required ConfiguracaoDivulgacao? Divulgacao { get; init; }

    public required ConfiguracaoTaxaInscricao? TaxaInscricao { get; init; }

    public required LocalidadeRegente? Localidade { get; init; }

    public required string? FusoHorario { get; init; }

    public required int? NumeroDeOpcoesDeCurso { get; init; }

    public required ReferenciaRegra? AlgoritmoContagemPrazo { get; init; }

    public required CalendarioDiasUteisCongelado? CalendarioDiasUteis { get; init; }

    public required IdentificadorLegivel? IdentificadorLegivel { get; init; }
}
