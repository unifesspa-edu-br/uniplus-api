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
/// O que uma operação mudou na janela: as concessões que migraram, foram recusadas, passaram a aguardar a proteção de
/// dados ou foram revogadas pelo fim da janela, o lote que ela criou e a forma do encerramento, se ela encerrou. A
/// revogação pelo fim só conta a que o fim produziu: a feita à mão antes do corte não entra.
/// </summary>
public sealed class MudancasDaJanela
{
    private MudancasDaJanela()
    {
    }

    public IReadOnlyList<MudancaNaLinha> Migradas { get; private init; } = [];

    public IReadOnlyList<MudancaNaLinha> Recusadas { get; private init; } = [];

    public IReadOnlyList<MudancaNaLinha> Aguardando { get; private init; } = [];

    public IReadOnlyList<MudancaNaLinha> RevogadasPeloFim { get; private init; } = [];

    public Guid? NovoLoteId { get; private init; }

    public FormaDoEncerramento? Encerramento { get; private init; }

    public int TotalMigradas { get; private init; }

    public int TotalRevogadas { get; private init; }

    public static MudancasDaJanela Entre(EstadoDaJanelaAntesDaOperacao antes, JanelaDeMigracao janela, ContextoDaMigracao contexto)
    {
        ArgumentNullException.ThrowIfNull(antes);
        ArgumentNullException.ThrowIfNull(janela);
        ArgumentNullException.ThrowIfNull(contexto);

        List<MigracaoDaConcessao> alteradas = [.. janela.Migracoes.Where(m => !antes.Linhas.TryGetValue(m.ConcessaoId, out SituacaoDaMigracao s) || s != m.Situacao)];

        return new MudancasDaJanela
        {
            Migradas = Linhas(alteradas, SituacaoDaMigracao.Migrada, contexto, static _ => true),
            Recusadas = Linhas(alteradas, SituacaoDaMigracao.Recusada, contexto, static _ => true),
            Aguardando = Linhas(alteradas, SituacaoDaMigracao.AguardandoProtecaoDeDados, contexto, static _ => true),
            RevogadasPeloFim = ApanharRevogadasPeloFim(alteradas, antes, janela, contexto),
            NovoLoteId = janela.Lotes.FirstOrDefault(l => !antes.Lotes.Contains(l.Id))?.Id,
            Encerramento = !antes.Encerrada && janela.Encerrada ? janela.Forma : null,
            TotalMigradas = janela.Migracoes.Count(static m => m.Situacao == SituacaoDaMigracao.Migrada),
            TotalRevogadas = contexto.Concessoes.Count(c => c.RevogadaPelaJanelaId == janela.Id),
        };
    }

    private static List<MudancaNaLinha> ApanharRevogadasPeloFim(
        List<MigracaoDaConcessao> alteradas, EstadoDaJanelaAntesDaOperacao antes, JanelaDeMigracao janela, ContextoDaMigracao contexto) =>
        Linhas(alteradas, SituacaoDaMigracao.NaoMigrada, contexto, c => c.RevogadaPelaJanelaId == janela.Id && !antes.Revogadas.Contains(c.Id));

    private static List<MudancaNaLinha> Linhas(
        List<MigracaoDaConcessao> alteradas, SituacaoDaMigracao situacao, ContextoDaMigracao contexto, Func<Concessao, bool> concessaoValida) =>
        [.. alteradas
            .Where(m => m.Situacao == situacao)
            .Select(m => (Linha: m, Concessao: contexto.Concessao(m.ConcessaoId)))
            .Where(x => x.Concessao is not null && concessaoValida(x.Concessao))
            .Select(x => new MudancaNaLinha(x.Concessao!, x.Linha.Id, contexto.ComposicaoDe(x.Concessao!).Acrescimo))];
}
