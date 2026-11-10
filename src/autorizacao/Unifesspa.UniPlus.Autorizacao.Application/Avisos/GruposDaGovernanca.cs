namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Os grupos de pessoas que a governança de perfis avisa, cada um definido pela permissão que a pessoa tem.</summary>
public enum GrupoDaGovernanca
{
    /// <summary>Quem aprova pela gestão de acessos; no perfil de governança, os mesmos gestores que mantêm perfis.</summary>
    Gestao = 1,

    /// <summary>Quem mantém perfis.</summary>
    Manutencao = 2,

    /// <summary>Quem aprova pela proteção de dados.</summary>
    ProtecaoDeDados = 3,
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
        [GrupoDaGovernanca.ProtecaoDeDados] = CodigoDe(PermissaoAutorizacao.PerfisAprovarProtecaoDeDados),
    };

    private readonly ITitularesDaPermissao _titulares;

    public GruposDaGovernanca(ITitularesDaPermissao titulares)
    {
        ArgumentNullException.ThrowIfNull(titulares);

        _titulares = titulares;
    }

    public static GrupoDaGovernanca DaEtapa(EtapaDaVersao etapa) =>
        etapa == EtapaDaVersao.GestaoDeAcessos ? GrupoDaGovernanca.Gestao : GrupoDaGovernanca.ProtecaoDeDados;

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
