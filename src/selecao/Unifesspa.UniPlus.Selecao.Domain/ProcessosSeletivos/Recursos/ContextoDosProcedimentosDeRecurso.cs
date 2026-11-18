namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que a conferência dos procedimentos precisa saber do lugar em que a janela de recurso está: os tipos de decisão
/// que o lugar declara produzir, os itens de avaliação da etapa e se o processo tem etapa de nota do ENEM.
/// </summary>
/// <param name="TiposDeDecisaoDoLugar">Os tipos que a fase ou etapa produtora declara; nulo quando a declaração dos tipos do lugar foi recusada.</param>
/// <param name="ItensDeAvaliacao">As identidades dos itens de avaliação da etapa produtora; vazio quando o produtor é uma fase.</param>
/// <param name="ProcessoTemNotaDoEnem">Se o processo importa a nota do ENEM, o que torna a área do ENEM um alvo possível.</param>
public sealed record ContextoDosProcedimentosDeRecurso(
    IReadOnlySet<TipoMotivoDeDecisao>? TiposDeDecisaoDoLugar,
    IReadOnlySet<Guid> ItensDeAvaliacao,
    bool ProcessoTemNotaDoEnem);
