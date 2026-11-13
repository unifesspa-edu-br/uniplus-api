namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Todos os eventos de manifestação técnica de um item, de todos os ciclos, com a situação de cada um derivada contra o
/// ciclo corrente do item: vigente, substituída ou encerrada (ADR-0163).
/// </summary>
public sealed class HistoricoDasManifestacoes
{
    private readonly HashSet<Guid> _substituidas;

    private HistoricoDasManifestacoes(IReadOnlyList<ManifestacaoDaProtecaoDoCatalogo> eventos, CicloDaAprovacao cicloCorrente)
    {
        Eventos = eventos;
        CicloCorrente = cicloCorrente;
        _substituidas = [.. eventos.Select(static e => e.SubstituiId).OfType<Guid>()];
    }

    /// <summary>Os eventos, do ciclo mais antigo ao mais recente e, no ciclo, do mais antigo ao mais recente.</summary>
    public IReadOnlyList<ManifestacaoDaProtecaoDoCatalogo> Eventos { get; }

    public CicloDaAprovacao CicloCorrente { get; }

    /// <summary>Os eventos já gravados do item, contra o ciclo em que o item está.</summary>
    public static HistoricoDasManifestacoes De(IReadOnlyList<ManifestacaoDaProtecaoDoCatalogo> eventos, CicloDaAprovacao cicloCorrente)
    {
        ArgumentNullException.ThrowIfNull(eventos);

        return new HistoricoDasManifestacoes(
            [.. eventos.OrderBy(static e => e.Ciclo.Numero).ThenBy(static e => e.Instante).ThenBy(static e => e.Id)], cicloCorrente);
    }

    /// <summary>A situação do evento: substituída tem precedência; senão, vigente no ciclo corrente e encerrada nos demais.</summary>
    public SituacaoDaManifestacao SituacaoDe(ManifestacaoDaProtecaoDoCatalogo evento)
    {
        ArgumentNullException.ThrowIfNull(evento);

        if (_substituidas.Contains(evento.Id))
        {
            return SituacaoDaManifestacao.Substituida;
        }

        return evento.Ciclo == CicloCorrente ? SituacaoDaManifestacao.Vigente : SituacaoDaManifestacao.Encerrada;
    }
}
