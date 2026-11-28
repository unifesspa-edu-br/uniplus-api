namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>Os campos editáveis da modalidade já normalizados, com os tokens resolvidos para os enums.</summary>
public sealed record CamposResolvidosDaModalidade(
    string? Nome,
    string? Descricao,
    NaturezaLegal NaturezaLegal,
    ComposicaoVagas ComposicaoVagas,
    string? ComposicaoOrigem,
    RegraRemanejamento? RegraRemanejamento,
    RemanejamentoParametros RemanejamentoParametros,
    PredicadoDnf? RegraDeAplicabilidade,
    AcaoQuandoIndeferido? AcaoQuandoIndeferido,
    string? BaseLegal);
