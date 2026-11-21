namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Projeção de leitura de um procedimento de recurso declarado.</summary>
/// <param name="TipoDeDecisao">O tipo de decisão recorrida, com o código e o rótulo que a tela mostra.</param>
/// <param name="Alvo">O alvo declarado, ou nulo quando o procedimento cobre o tipo de decisão inteiro.</param>
/// <param name="Regra">A referência congelada da regra do catálogo.</param>
/// <param name="Sugestao">O código do procedimento que a plataforma sugere pela forma de avaliar; calculado na leitura, nunca congelado.</param>
public sealed record ProcedimentoDeRecursoDto(
    CodigoComRotulo<TipoMotivoDeDecisao> TipoDeDecisao,
    AlvoDoProcedimentoDeRecursoDto? Alvo,
    ReferenciaRegraDto Regra,
    string? Sugestao);

/// <summary>O alvo de um procedimento de recurso: o item de avaliação ou a área do ENEM; só um dos dois vem preenchido.</summary>
public sealed record AlvoDoProcedimentoDeRecursoDto(Guid? ItemDeAvaliacaoId, string? AreaDoEnem);
