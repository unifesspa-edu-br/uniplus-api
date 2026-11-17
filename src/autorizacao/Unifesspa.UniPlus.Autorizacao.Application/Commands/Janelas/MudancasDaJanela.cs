namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>As linhas, as revogações e os lotes da janela antes de uma operação, para saber depois o que ela mudou.</summary>
public sealed record EstadoDaJanelaAntesDaOperacao(
    IReadOnlyDictionary<Guid, SituacaoDaMigracao> Linhas, IReadOnlySet<Guid> Revogadas, IReadOnlySet<Guid> Lotes, bool Encerrada)
{
    public static EstadoDaJanelaAntesDaOperacao De(JanelaDeMigracao janela, ContextoDaMigracao contexto)
    {
        ArgumentNullException.ThrowIfNull(janela);
        ArgumentNullException.ThrowIfNull(contexto);

        return new EstadoDaJanelaAntesDaOperacao(
            janela.Migracoes.ToDictionary(static m => m.ConcessaoId, static m => m.Situacao),
            contexto.Concessoes.Where(static c => c.RevogadaEm is not null).Select(static c => c.Id).ToHashSet(),
            janela.Lotes.Select(static l => l.Id).ToHashSet(),
            janela.Encerrada);
    }
}

/// <summary>A concessão cuja linha mudou de situação, a linha e o que a versão-alvo acrescenta ao que ela dava.</summary>
public sealed record MudancaNaLinha(Concessao Concessao, Guid LinhaId, IReadOnlyList<string> Acrescimo);

/// <summary>
/// O que uma operação mudou na janela: as mudanças nas linhas, o lote que ela criou, a forma do encerramento, se ela
/// encerrou, e os totais.
/// </summary>
public sealed class MudancasDaJanela
{
    private MudancasDaJanela()
    {
    }

    public MudancasNasLinhas Linhas { get; private init; } = new([], [], [], []);

    public Guid? NovoLoteId { get; private init; }

    public FormaDoEncerramento? Encerramento { get; private init; }

    public int TotalMigradas { get; private init; }

    public int TotalRevogadas { get; private init; }

    public static MudancasDaJanela Entre(EstadoDaJanelaAntesDaOperacao antes, JanelaDeMigracao janela, ContextoDaMigracao contexto)
    {
        ArgumentNullException.ThrowIfNull(antes);
        ArgumentNullException.ThrowIfNull(janela);
        ArgumentNullException.ThrowIfNull(contexto);

        return new MudancasDaJanela
        {
            Linhas = MudancasNasLinhas.Entre(antes, janela, contexto),
            NovoLoteId = janela.Lotes.FirstOrDefault(l => !antes.Lotes.Contains(l.Id))?.Id,
            Encerramento = !antes.Encerrada && janela.Encerrada ? janela.Forma : null,
            TotalMigradas = janela.Migracoes.Count(static m => m.Situacao == SituacaoDaMigracao.Migrada),
            TotalRevogadas = contexto.Concessoes.Count(c => c.RevogadaPelaJanelaId == janela.Id),
        };
    }
}
