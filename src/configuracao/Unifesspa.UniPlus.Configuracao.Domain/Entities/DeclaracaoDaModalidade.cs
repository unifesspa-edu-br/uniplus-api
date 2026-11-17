namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Os campos editáveis de uma modalidade como chegam de quem a cadastra, antes de qualquer normalização:
/// os enums como tokens textuais, e a origem e os parâmetros de remanejamento como códigos soltos.
/// </summary>
internal sealed record DeclaracaoDaModalidade(
    string? Descricao,
    string? NaturezaLegalToken,
    string? ComposicaoVagasToken,
    string? ComposicaoOrigem,
    string? RegraRemanejamentoToken,
    string? RemanejamentoDestino,
    string? RemanejamentoPar,
    string? RemanejamentoFallback,
    PredicadoDnf? RegraDeAplicabilidade,
    string? AcaoQuandoIndeferidoToken,
    string? BaseLegal);
