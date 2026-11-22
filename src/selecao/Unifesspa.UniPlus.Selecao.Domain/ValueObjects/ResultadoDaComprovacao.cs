namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Enums;

/// <summary>Consequência aplicável de uma regra de comprovação.</summary>
public sealed record ConsequenciaAplicavel(Guid RegraDeComprovacaoId, FormaDeComprovacao FormaDaRegra, string Consequencia, string? OcorrenciaId = null);

/// <summary>Item que orienta como cumprir um grupo cuja consequência está pendente.</summary>
public sealed record PendenciaDeOrientacao(Guid RegraDeComprovacaoId, string? OcorrenciaId = null);

/// <summary>Situação de uma exigência para um registro de grupo do formulário.</summary>
public sealed record SituacaoDaExigenciaPorOcorrencia(Guid DocumentoExigidoId, string OcorrenciaId, StatusResolucaoExigencia Situacao);

/// <summary>Situação de uma regra para um registro de grupo do formulário.</summary>
public sealed record SituacaoDaRegraPorOcorrencia(Guid RegraDeComprovacaoId, string OcorrenciaId, SituacaoDaComprovacao Situacao);

/// <summary>Resultado da avaliação das regras e suas consequências por ocorrência.</summary>
public sealed record ResultadoDaComprovacao(
    IReadOnlyDictionary<Guid, SituacaoDaComprovacao> SituacoesPorRegra,
    IReadOnlyDictionary<Guid, StatusResolucaoExigencia> SituacoesPorExigencia,
    IReadOnlyList<ConsequenciaAplicavel> ConsequenciasVigentes,
    IReadOnlyList<PendenciaDeOrientacao> PendenciasDeOrientacao,
    IReadOnlyList<SituacaoDaExigenciaPorOcorrencia> SituacoesDeExigenciaPorOcorrencia,
    IReadOnlyList<SituacaoDaRegraPorOcorrencia> SituacoesDeRegraPorOcorrencia,
    IReadOnlySet<Guid> RegrasComConsequenciaAdiada);
