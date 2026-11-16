namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>Projeção de leitura de um procedimento de recurso declarado.</summary>
/// <param name="TipoDeDecisao">Código do tipo de decisão recorrida.</param>
/// <param name="Alvo">O alvo declarado, ou nulo quando o procedimento cobre o tipo de decisão inteiro.</param>
/// <param name="Regra">A referência congelada da regra do catálogo.</param>
public sealed record ProcedimentoDeRecursoDto(string TipoDeDecisao, AlvoDoProcedimentoDeRecursoDto? Alvo, ReferenciaRegraDto Regra);

/// <summary>O alvo de um procedimento de recurso: o item de avaliação ou a área do ENEM; só um dos dois vem preenchido.</summary>
public sealed record AlvoDoProcedimentoDeRecursoDto(Guid? ItemDeAvaliacaoId, string? AreaDoEnem);
