namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed record ModalidadeNoCadastro
{
    public required Guid Id { get; init; }
    public required string Codigo { get; init; }
    public required string Nome { get; init; }
    public string? Descricao { get; init; }
    public required NaturezaLegalModalidade? NaturezaLegal { get; init; }
    public required ComposicaoVagasModalidade? ComposicaoVagas { get; init; }
    public string? ComposicaoOrigemCodigo { get; init; }
    public RegraRemanejamentoModalidade? RegraRemanejamento { get; init; }
    public string? RemanejamentoDestino { get; init; }
    public string? RemanejamentoPar { get; init; }
    public string? RemanejamentoFallback { get; init; }
    public required PredicadoDnf RegraDeAplicabilidade { get; init; }
    public string? AcaoQuandoIndeferido { get; init; }
    public required string BaseLegal { get; init; }
}
