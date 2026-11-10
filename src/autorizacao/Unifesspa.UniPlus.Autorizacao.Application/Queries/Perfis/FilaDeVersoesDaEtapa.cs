namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// A fila de aprovação de uma etapa: as versões pendentes no prazo cuja etapa atual é a pedida, da mais antiga para a mais
/// nova. A etapa de cada versão se calcula pelo catálogo atual e nunca se grava.
/// </summary>
public sealed class FilaDeVersoesDaEtapa
{
    private readonly IPerfilRepository _perfis;
    private readonly CatalogoDaAplicacao _catalogo;
    private readonly TimeProvider _clock;

    public FilaDeVersoesDaEtapa(IPerfilRepository perfis, CatalogoDaAplicacao catalogo, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(clock);

        _perfis = perfis;
        _catalogo = catalogo;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ItemDaFilaDeVersoesDto>> MontarAsync(EtapaDaVersao etapa, Identidade? leitor, CancellationToken cancellationToken)
    {
        IReadOnlyList<Perfil> comPendente = await _perfis.ListarComPendenteNoPrazoAsync(_clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        return
        [.. comPendente
            .Select(p => (Perfil: p, Versao: VersaoMapping.PendenteDe(p, _catalogo, leitor)))
            .Where(x => x.Versao?.EtapaAtual == etapa.ToCodigo())
            .Select(static x => new ItemDaFilaDeVersoesDto(x.Perfil.Id, x.Perfil.Nome, x.Versao!, x.Versao!.PropostaPorQuemLe))
            .OrderBy(static i => i.Versao.CriadaEm)];
    }
}
