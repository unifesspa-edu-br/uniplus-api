namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Os campos editáveis de uma modalidade como chegam de quem a cadastra, antes de qualquer normalização:
/// os enums como códigos textuais, e a origem e os parâmetros de remanejamento como códigos soltos.
/// </summary>
public sealed record DeclaracaoDaModalidade(
    string? Nome,
    string? Descricao,
    string? CodigoDaNaturezaLegal,
    string? CodigoDaComposicaoDeVagas,
    string? ComposicaoOrigem,
    string? CodigoDaRegraDeRemanejamento,
    string? RemanejamentoDestino,
    string? RemanejamentoPar,
    string? RemanejamentoFallback,
    PredicadoDnf? RegraDeAplicabilidade,
    string? CodigoDaAcaoQuandoIndeferido,
    string? BaseLegal);
