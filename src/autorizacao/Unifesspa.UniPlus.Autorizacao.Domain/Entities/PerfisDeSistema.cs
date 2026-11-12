namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

/// <summary>Identificadores fixos dos perfis de sistema, gravados pela carga inicial.</summary>
public static class PerfisDeSistema
{
    public static readonly Guid GestorDeAcessosId = new("019a0000-0000-7000-8000-000000000001");

    public static readonly Guid RepresentanteDoControladorId = new("019a0000-0000-7000-8000-000000000002");

    public static readonly Guid EncarregadoDeDadosId = new("019a0000-0000-7000-8000-000000000003");

    /// <summary>O perfil é um dos perfis de governança, que só a carga inicial cria.</summary>
    public static bool EhDeGovernanca(Guid perfilId) =>
        perfilId == GestorDeAcessosId || perfilId == RepresentanteDoControladorId || perfilId == EncarregadoDeDadosId;
}
