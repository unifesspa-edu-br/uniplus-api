namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

/// <summary>Uma permissão que um perfil de governança reúne.</summary>
public sealed record PermissaoDeGovernanca(Guid PerfilId, string Codigo);

/// <summary>
/// A composição esperada dos perfis de governança, a única que o código declara: a carga inicial a grava e a conferência
/// da troca de catálogo a compara com a gravada. Os perfis de governança não têm versão, e a composição deles só muda por
/// PR com migration.
/// </summary>
public static class ComposicaoDaGovernanca
{
    /// <summary>As permissões de cada perfil de governança, na ordem da carga.</summary>
    public static IReadOnlyList<PermissaoDeGovernanca> Esperada { get; } =
    [
        new(PerfisDeSistema.GestorDeAcessosId, "autorizacao:concessoes:administrar"),
        new(PerfisDeSistema.GestorDeAcessosId, "autorizacao:pedidos:aprovar-funcional"),
        new(PerfisDeSistema.GestorDeAcessosId, "autorizacao:perfis:aprovar-funcional"),
        new(PerfisDeSistema.GestorDeAcessosId, "autorizacao:perfis:manter"),
        new(PerfisDeSistema.GestorDeAcessosId, "autorizacao:permissoes:consultar-catalogo"),
        new(PerfisDeSistema.GestorDeAcessosId, "autorizacao:revisoes:rever-protecao-de-dados"),
        new(PerfisDeSistema.GestorDeAcessosId, "autorizacao:termos:publicar"),
        new(PerfisDeSistema.RepresentanteDoControladorId, "autorizacao:concessoes:administrar"),
        new(PerfisDeSistema.RepresentanteDoControladorId, "autorizacao:pedidos:aprovar-protecao-de-dados"),
        new(PerfisDeSistema.RepresentanteDoControladorId, "autorizacao:perfis:aprovar-protecao-de-dados"),
        new(PerfisDeSistema.RepresentanteDoControladorId, "autorizacao:permissoes:consultar-catalogo"),
        new(PerfisDeSistema.RepresentanteDoControladorId, "autorizacao:revisoes:conduzir"),
        new(PerfisDeSistema.RepresentanteDoControladorId, "configuracao:protecao-do-catalogo:aprovar"),
    ];

    /// <summary>Os códigos que o perfil de governança deve reunir.</summary>
    public static IReadOnlyList<string> CodigosDe(Guid perfilId) =>
        [.. Esperada.Where(p => p.PerfilId == perfilId).Select(static p => p.Codigo)];
}
