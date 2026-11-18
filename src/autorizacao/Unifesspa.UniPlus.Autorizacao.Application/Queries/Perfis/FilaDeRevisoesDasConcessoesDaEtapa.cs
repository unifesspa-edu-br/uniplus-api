namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// A fila de revisões de uma etapa: as revisões abertas e dentro do prazo em que quem lê tem concessão a decidir, da de
/// prazo mais curto para a de prazo mais longo. O que quem lê pode decidir sai da própria revisão, pelas regras que ela
/// aplica na gravação. Lê sem rastrear nem travar e não traz o nome de nenhum beneficiário.
/// </summary>
public sealed class FilaDeRevisoesDasConcessoesDaEtapa
{
    private readonly IRevisaoDasConcessoesRepository _revisoes;
    private readonly IPerfilRepository _perfis;
    private readonly TimeProvider _clock;

    public FilaDeRevisoesDasConcessoesDaEtapa(IRevisaoDasConcessoesRepository revisoes, IPerfilRepository perfis, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(revisoes);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(clock);

        _revisoes = revisoes;
        _perfis = perfis;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ItemDaListaDeRevisoesDto>> MontarAsync(
        EtapaDaVersao etapa, Identidade leitor, CancellationToken cancellationToken)
    {
        DateTimeOffset agora = _clock.GetUtcNow();
        IReadOnlyList<RevisaoDasConcessoesDoPerfil> revisoes = await _revisoes.ListarAbertasParaLeituraAsync(agora, cancellationToken).ConfigureAwait(false);

        return revisoes.Count == 0 ? [] : await ItensAsync(revisoes, EtapaDaMigracaoDe(etapa), (leitor, agora), cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<ItemDaListaDeRevisoesDto>> ItensAsync(
        IReadOnlyList<RevisaoDasConcessoesDoPerfil> revisoes, EtapaDaMigracao etapa, (Identidade Leitor, DateTimeOffset Agora) quando, CancellationToken cancellationToken)
    {
        List<Guid> perfilIds = [.. revisoes.Select(static j => j.PerfilId)];
        ILookup<Guid, Concessao> concessoes = (await _revisoes.ListarConcessoesAtivasDosPerfisAsync(perfilIds, cancellationToken).ConfigureAwait(false))
            .ToLookup(static c => c.PerfilId);
        Dictionary<Guid, Perfil> perfis = (await _perfis.ListarPorIdsAsync(perfilIds, cancellationToken).ConfigureAwait(false))
            .ToDictionary(static p => p.Id);

        return
        [.. revisoes
            .Select(j => ItemDe(j, perfis[j.PerfilId], j.QuantidadeADecidir(etapa, quando.Leitor, [.. concessoes[j.PerfilId]], quando.Agora)))
            .OfType<ItemDaListaDeRevisoesDto>()
            .OrderBy(static i => i.TerminaEm)];
    }

    private static EtapaDaMigracao EtapaDaMigracaoDe(EtapaDaVersao etapa) =>
        etapa == EtapaDaVersao.GestaoDeAcessos ? EtapaDaMigracao.GestaoDeAcessos : EtapaDaMigracao.ProtecaoDeDados;

    private static ItemDaListaDeRevisoesDto? ItemDe(RevisaoDasConcessoesDoPerfil revisao, Perfil perfil, int? quantidade) =>
        quantidade is { } aDecidir
            ? new ItemDaListaDeRevisoesDto(
                perfil.Id,
                perfil.Nome,
                perfil.Versoes.First(v => v.Id == revisao.VersaoAlvoId).Numero,
                revisao.TerminaEm,
                revisao.EnvioDaGestao is not null,
                aDecidir)
            : null;
}
