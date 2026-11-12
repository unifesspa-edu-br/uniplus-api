namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

/// <summary>
/// Os papéis de governança que uma mesma pessoa não exerce ao mesmo tempo: o Representante do controlador decide em nome
/// da instituição, e o Encarregado de dados acompanha essas decisões; quem decide não acompanha a si mesmo (ADR-0162). O
/// gestor de acessos acumula com qualquer um dos dois.
/// </summary>
public static class PapeisIncompativeis
{
    /// <summary>O perfil que a pessoa não pode ter vigente para receber <paramref name="perfilId"/>; nulo quando não há.</summary>
    public static Guid? PerfilIncompativelCom(Guid perfilId) =>
        perfilId == PerfisDeSistema.RepresentanteDoControladorId ? PerfisDeSistema.EncarregadoDeDadosId
        : perfilId == PerfisDeSistema.EncarregadoDeDadosId ? PerfisDeSistema.RepresentanteDoControladorId
        : null;
}
