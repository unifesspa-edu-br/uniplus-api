namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>Uma permissão que um perfil de governança reúne.</summary>
public sealed record PermissaoDeGovernanca(Guid PerfilId, CodigoDePermissao Codigo);

/// <summary>
/// A composição esperada dos perfis de governança, a única que o código declara: a carga inicial a grava e a conferência
/// da troca de catálogo a compara com a gravada. Os perfis de governança não têm versão, e a composição deles só muda por
/// PR que altera o modelo e gera a migration inicial de novo.
/// </summary>
public static class ComposicaoDaGovernanca
{
    private const string ModuloDaAutorizacao = "autorizacao";

    /// <summary>As permissões de cada perfil de governança, na ordem da carga.</summary>
    public static IReadOnlyList<PermissaoDeGovernanca> Esperada { get; } =
    [
        Reune(PerfisDeSistema.GestorDeAcessosId, "autorizacao:concessoes:administrar"),
        Reune(PerfisDeSistema.GestorDeAcessosId, "autorizacao:pedidos:aprovar-funcional"),
        Reune(PerfisDeSistema.GestorDeAcessosId, "autorizacao:perfis:aprovar-funcional"),
        Reune(PerfisDeSistema.GestorDeAcessosId, "autorizacao:perfis:manter"),
        Reune(PerfisDeSistema.GestorDeAcessosId, "autorizacao:permissoes:consultar-catalogo"),
        Reune(PerfisDeSistema.GestorDeAcessosId, "autorizacao:revisoes:rever-protecao-de-dados"),
        Reune(PerfisDeSistema.GestorDeAcessosId, "autorizacao:termos:publicar"),
        Reune(PerfisDeSistema.RepresentanteDoControladorId, "autorizacao:concessoes:administrar"),
        Reune(PerfisDeSistema.RepresentanteDoControladorId, "autorizacao:pedidos:aprovar-protecao-de-dados"),
        Reune(PerfisDeSistema.RepresentanteDoControladorId, "autorizacao:perfis:aprovar-protecao-de-dados"),
        Reune(PerfisDeSistema.RepresentanteDoControladorId, "autorizacao:permissoes:consultar-catalogo"),
        Reune(PerfisDeSistema.RepresentanteDoControladorId, "autorizacao:revisoes:conduzir"),
        Reune(PerfisDeSistema.RepresentanteDoControladorId, "configuracao:protecao-do-catalogo:aprovar"),
        Reune(PerfisDeSistema.GestorDeProtecaoDeDadosId, "autorizacao:concessoes:consultar"),
        Reune(PerfisDeSistema.GestorDeProtecaoDeDadosId, "autorizacao:pedidos:consultar"),
        Reune(PerfisDeSistema.GestorDeProtecaoDeDadosId, "autorizacao:perfis:consultar-versoes"),
        Reune(PerfisDeSistema.GestorDeProtecaoDeDadosId, "autorizacao:permissoes:consultar-catalogo"),
        Reune(PerfisDeSistema.GestorDeProtecaoDeDadosId, "autorizacao:providencias:solicitar"),
        Reune(PerfisDeSistema.GestorDeProtecaoDeDadosId, "autorizacao:revisoes:consultar"),
        Reune(PerfisDeSistema.GestorDeProtecaoDeDadosId, "configuracao:protecao-do-catalogo:consultar"),
        Reune(PerfisDeSistema.GestorDeProtecaoDeDadosId, "configuracao:protecao-do-catalogo:manifestar"),
    ];

    private static PermissaoDeGovernanca Reune(Guid perfilId, string codigo) => new(perfilId, CodigoDePermissao.De(codigo));

    /// <summary>
    /// Se um perfil de governança pode reunir a permissão do catálogo: qualquer uma da autorização e, de outro módulo, só
    /// a reservada à governança. Uma permissão comum de outro módulo chegaria a todos os titulares do papel sem passar pela
    /// aprovação que os perfis comuns exigem.
    /// </summary>
    public static bool Admite(PermissaoDoCatalogo permissao)
    {
        ArgumentNullException.ThrowIfNull(permissao);

        return permissao.Codigo.Modulo == ModuloDaAutorizacao || permissao.Reservada;
    }

    /// <summary>Os códigos que o perfil de governança deve reunir.</summary>
    public static IReadOnlyList<CodigoDePermissao> CodigosDe(Guid perfilId) =>
        [.. Esperada.Where(p => p.PerfilId == perfilId).Select(static p => p.Codigo)];
}
