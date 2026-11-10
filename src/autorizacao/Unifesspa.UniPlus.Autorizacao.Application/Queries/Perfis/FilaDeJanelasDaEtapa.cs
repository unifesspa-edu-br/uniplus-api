namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// A fila de janelas de uma etapa: as janelas abertas e dentro do prazo em que quem lê tem concessão a decidir, da de
/// prazo mais curto para a de prazo mais longo. O que quem lê pode decidir sai da própria janela, pelas regras que ela
/// aplica na gravação. Lê sem rastrear nem travar e não traz o nome de nenhum beneficiário.
/// </summary>
public sealed class FilaDeJanelasDaEtapa
{
    private readonly IJanelaDeMigracaoRepository _janelas;
    private readonly IPerfilRepository _perfis;
    private readonly TimeProvider _clock;

    public FilaDeJanelasDaEtapa(IJanelaDeMigracaoRepository janelas, IPerfilRepository perfis, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(janelas);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(clock);

        _janelas = janelas;
        _perfis = perfis;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ItemDaFilaDeJanelasDto>> MontarAsync(
        EtapaDaVersao etapa, Identidade leitor, CancellationToken cancellationToken)
    {
        DateTimeOffset agora = _clock.GetUtcNow();
        IReadOnlyList<JanelaDeMigracao> janelas = await _janelas.ListarAbertasParaLeituraAsync(agora, cancellationToken).ConfigureAwait(false);

        return janelas.Count == 0 ? [] : await ItensAsync(janelas, EtapaDaMigracaoDe(etapa), (leitor, agora), cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<ItemDaFilaDeJanelasDto>> ItensAsync(
        IReadOnlyList<JanelaDeMigracao> janelas, EtapaDaMigracao etapa, (Identidade Leitor, DateTimeOffset Agora) quando, CancellationToken cancellationToken)
    {
        List<Guid> perfilIds = [.. janelas.Select(static j => j.PerfilId)];
        ILookup<Guid, Concessao> concessoes = (await _janelas.ListarConcessoesAtivasDosPerfisAsync(perfilIds, cancellationToken).ConfigureAwait(false))
            .ToLookup(static c => c.PerfilId);
        Dictionary<Guid, Perfil> perfis = (await _perfis.ListarPorIdsAsync(perfilIds, cancellationToken).ConfigureAwait(false))
            .ToDictionary(static p => p.Id);

        return
        [.. janelas
            .Select(j => ItemDe(j, perfis[j.PerfilId], j.QuantidadeADecidir(etapa, quando.Leitor, [.. concessoes[j.PerfilId]], quando.Agora)))
            .OfType<ItemDaFilaDeJanelasDto>()
            .OrderBy(static i => i.Prazo)];
    }

    private static EtapaDaMigracao EtapaDaMigracaoDe(EtapaDaVersao etapa) =>
        etapa == EtapaDaVersao.GestaoDeAcessos ? EtapaDaMigracao.GestaoDeAcessos : EtapaDaMigracao.ProtecaoDeDados;

    private static ItemDaFilaDeJanelasDto? ItemDe(JanelaDeMigracao janela, Perfil perfil, int? quantidade) =>
        quantidade is { } aDecidir
            ? new ItemDaFilaDeJanelasDto(
                perfil.Id,
                perfil.Nome,
                perfil.Versoes.First(v => v.Id == janela.VersaoAlvoId).Numero,
                janela.Prazo,
                janela.LoteDaGestao is not null,
                aDecidir)
            : null;
}
