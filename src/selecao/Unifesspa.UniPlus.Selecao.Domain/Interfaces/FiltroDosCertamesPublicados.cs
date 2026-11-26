namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>Filtros aplicados aos certames publicados.</summary>
public sealed record FiltroDosCertamesPublicados(
    SituacaoDoCertame? Situacao = null, string? Modalidade = null, TermoDeBusca? Busca = null);
