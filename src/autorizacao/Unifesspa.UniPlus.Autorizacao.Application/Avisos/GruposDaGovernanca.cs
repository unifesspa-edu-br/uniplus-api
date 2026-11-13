namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Os grupos de pessoas que a governança de perfis avisa, cada um definido pela permissão que a pessoa tem.</summary>
public enum GrupoDaGovernanca
{
    /// <summary>Os gestores de acessos que aprovam a adequação funcional; no perfil de governança, os mesmos que mantêm perfis.</summary>
    Gestao = 1,

    /// <summary>Os gestores de acessos que mantêm perfis.</summary>
    Manutencao = 2,

    /// <summary>
    /// Os Representantes do controlador: quem aprova pela proteção de dados, permissão reservada que só o perfil do
    /// Representante reúne. O Encarregado de dados não entra: não recebe aviso de decisão.
    /// </summary>
    RepresentanteDoControlador = 3,

    /// <summary>Quem administra as concessões: os gestores de acessos e os Representantes do controlador.</summary>
    AdministracaoDasConcessoes = 4,
}

/// <summary>
/// Quem recebe o aviso por pertencer a um grupo da governança: os titulares com e-mail na concessão que dá a permissão do
/// grupo. A regra de quem é cada grupo fica aqui, num lugar só.
/// </summary>
public sealed class GruposDaGovernanca
{
    private static readonly Dictionary<GrupoDaGovernanca, string> Codigos = new()
    {
        [GrupoDaGovernanca.Gestao] = CodigoDe(PermissaoAutorizacao.PerfisAprovarFuncional),
        [GrupoDaGovernanca.Manutencao] = CodigoDe(PermissaoAutorizacao.PerfisManter),
        [GrupoDaGovernanca.RepresentanteDoControlador] = CodigoDe(PermissaoAutorizacao.PerfisAprovarProtecaoDeDados),
        [GrupoDaGovernanca.AdministracaoDasConcessoes] = CodigoDe(PermissaoAutorizacao.ConcessoesAdministrar),
    };

    private readonly ITitularesDaPermissao _titulares;

    public GruposDaGovernanca(ITitularesDaPermissao titulares)
    {
        ArgumentNullException.ThrowIfNull(titulares);

        _titulares = titulares;
    }

    public static GrupoDaGovernanca DaEtapa(EtapaDaVersao etapa) =>
        etapa == EtapaDaVersao.GestaoDeAcessos ? GrupoDaGovernanca.Gestao : GrupoDaGovernanca.RepresentanteDoControlador;

    public async Task<List<DestinatarioDoAviso>> ListarAsync(GrupoDaGovernanca grupo, CancellationToken cancellationToken) =>
        await TitularesDoAviso.ListarAsync(_titulares, Codigos[grupo], cancellationToken).ConfigureAwait(false);

    /// <summary>Os titulares dos grupos, na ordem dada; quem está em mais de um grupo aparece uma vez por grupo.</summary>
    public async Task<List<DestinatarioDoAviso>> ListarAsync(IEnumerable<GrupoDaGovernanca> grupos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grupos);

        List<DestinatarioDoAviso> destinatarios = [];
        foreach (GrupoDaGovernanca grupo in grupos)
        {
            destinatarios.AddRange(await ListarAsync(grupo, cancellationToken).ConfigureAwait(false));
        }

        return destinatarios;
    }

    private static string CodigoDe(PermissaoAutorizacao permissao) => CatalogoDePermissoes<PermissaoAutorizacao>.Obter(permissao).Codigo;
}
