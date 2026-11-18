namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>As linhas, as revogações e os envios de decisões da revisão antes de uma operação, para saber depois o que ela mudou.</summary>
public sealed record EstadoDaRevisaoAntesDaOperacao(
    IReadOnlyDictionary<Guid, SituacaoDaMigracao> Linhas, IReadOnlySet<Guid> Revogadas, IReadOnlySet<Guid> Envios, bool Encerrada)
{
    public static EstadoDaRevisaoAntesDaOperacao De(RevisaoDasConcessoesDoPerfil revisao, ContextoDaRevisao contexto)
    {
        ArgumentNullException.ThrowIfNull(revisao);
        ArgumentNullException.ThrowIfNull(contexto);

        return new EstadoDaRevisaoAntesDaOperacao(
            revisao.Migracoes.ToDictionary(static m => m.ConcessaoId, static m => m.Situacao),
            contexto.Concessoes.Where(static c => c.RevogadaEm is not null).Select(static c => c.Id).ToHashSet(),
            revisao.Envios.Select(static l => l.Id).ToHashSet(),
            revisao.Encerrada);
    }
}

/// <summary>A concessão cuja linha mudou de situação, a linha e o que a versão-alvo acrescenta ao que ela dava.</summary>
public sealed record MudancaNaLinha(Concessao Concessao, Guid LinhaId, IReadOnlyList<string> Acrescimo);

/// <summary>
/// O que uma operação mudou na revisão: as concessões que migraram, foram recusadas, passaram a aguardar a proteção de
/// dados ou foram revogadas pelo fim da revisão, o envio de decisões que ela criou e a forma do encerramento, se ela encerrou. A
/// revogação pelo fim só conta a que o fim produziu: a feita à mão antes do encerramento não entra.
/// </summary>
public sealed class MudancasDaRevisaoDasConcessoes
{
    private MudancasDaRevisaoDasConcessoes()
    {
    }

    public MudancasNasLinhas Linhas { get; private init; } = new([], [], [], []);

    public Guid? NovoEnvioId { get; private init; }

    public FormaDoEncerramento? Encerramento { get; private init; }

    public int TotalMigradas { get; private init; }

    public int TotalRevogadas { get; private init; }

    public static MudancasDaRevisaoDasConcessoes Entre(EstadoDaRevisaoAntesDaOperacao antes, RevisaoDasConcessoesDoPerfil revisao, ContextoDaRevisao contexto)
    {
        ArgumentNullException.ThrowIfNull(antes);
        ArgumentNullException.ThrowIfNull(revisao);
        ArgumentNullException.ThrowIfNull(contexto);

        return new MudancasDaRevisaoDasConcessoes
        {
            Linhas = MudancasNasLinhas.Entre(antes, revisao, contexto),
            NovoEnvioId = revisao.Envios.FirstOrDefault(l => !antes.Envios.Contains(l.Id))?.Id,
            Encerramento = !antes.Encerrada && revisao.Encerrada ? revisao.Forma : null,
            TotalMigradas = revisao.Migracoes.Count(static m => m.Situacao == SituacaoDaMigracao.Migrada),
            TotalRevogadas = contexto.Concessoes.Count(c => c.RevogadaPelaRevisaoId == revisao.Id),
        };
    }
}
