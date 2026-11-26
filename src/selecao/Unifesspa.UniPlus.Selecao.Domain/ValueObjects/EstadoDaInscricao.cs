namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed record EstadoDaInscricao
{
    private EstadoDaInscricao(SituacaoInscricao situacao, string? numeroInscricao, CancelamentoDaInscricao? cancelamento)
    {
        Situacao = situacao;
        NumeroInscricao = numeroInscricao;
        Cancelamento = cancelamento;
    }

    public SituacaoInscricao Situacao { get; }
    public string? NumeroInscricao { get; }
    public CancelamentoDaInscricao? Cancelamento { get; }

    public static EstadoDaInscricao Inicial => new(SituacaoInscricao.Rascunho, null, null);

    public static EstadoDaInscricao Reidratar(SituacaoInscricao situacao, string? numeroInscricao, CancelamentoDaInscricao? cancelamento) =>
        new(situacao, numeroInscricao, cancelamento);

    internal EstadoDaInscricao AtribuirNumeroSeNecessario(string numeroInscricao) =>
        new(Situacao, NumeroInscricao ?? numeroInscricao, Cancelamento);

    internal EstadoDaInscricao SubmeterAposConferencia(string numeroInscricao) =>
        new(SituacaoInscricao.Enviada, NumeroInscricao ?? numeroInscricao, Cancelamento);

    internal EstadoDaInscricao CancelarAposConferencia(CancelamentoDaInscricao cancelamento) =>
        new(SituacaoInscricao.Cancelada, NumeroInscricao, cancelamento);
}
