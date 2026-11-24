namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;

/// <summary>
/// A mesma pessoa não exerce ao mesmo tempo o papel de Representante do controlador e o de Gestor de proteção de dados, nas duas
/// ordens; o gestor de acessos acumula com qualquer um dos dois, e o perfil comum não participa da incompatibilidade.
/// </summary>
public sealed class PapeisIncompativeisTests
{
    [Fact(DisplayName = "Representante do controlador e Gestor de proteção de dados são incompatíveis nas duas ordens")]
    public void RepresentanteEGestorDeProtecaoDeDados_IncompativeisNasDuasOrdens()
    {
        PapeisIncompativeis.PerfilIncompativelCom(PerfisDeSistema.RepresentanteDoControladorId).Should().Be(PerfisDeSistema.GestorDeProtecaoDeDadosId);
        PapeisIncompativeis.PerfilIncompativelCom(PerfisDeSistema.GestorDeProtecaoDeDadosId).Should().Be(PerfisDeSistema.RepresentanteDoControladorId);
    }

    [Fact(DisplayName = "O gestor de acessos e o perfil comum não têm papel incompatível")]
    public void GestorEPerfilComum_SemIncompatibilidade()
    {
        PapeisIncompativeis.PerfilIncompativelCom(PerfisDeSistema.GestorDeAcessosId).Should().BeNull();
        PapeisIncompativeis.PerfilIncompativelCom(Guid.CreateVersion7()).Should().BeNull();
    }

    [Fact(DisplayName = "Os três perfis de sistema são de governança, e nenhum outro é")]
    public void PerfisDeGovernanca_SaoOsTresDeSistema()
    {
        new[] { PerfisDeSistema.GestorDeAcessosId, PerfisDeSistema.RepresentanteDoControladorId, PerfisDeSistema.GestorDeProtecaoDeDadosId }
            .Should().OnlyContain(static id => PerfisDeSistema.EhDeGovernanca(id));
        PerfisDeSistema.EhDeGovernanca(Guid.CreateVersion7()).Should().BeFalse();
    }
}
