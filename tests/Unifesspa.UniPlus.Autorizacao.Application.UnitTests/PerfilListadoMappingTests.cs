namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CatalogoDoMapeamento;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.PerfilMappingTests;

public sealed class PerfilListadoMappingTests
{
    [Fact(DisplayName = "A listagem do perfil com versão em vigor e pendente expõe todos os campos")]
    public void ToListadoDto_PerfilComVigenteEPendente_ExpoeTodosOsCampos()
    {
        Perfil perfil = PerfilComVigenteEPendente();

        PerfilListadoDto dto = PerfilMapping.ToListadoDto(perfil, Catalogo);

        dto.Should().BeEquivalentTo(new PerfilListadoDto(
            perfil.Id, perfil.Nome, perfil.Descricao, true, null, ValoresCanonicos.De(ClassificacaoDeProtecao.Interno), true, true, null, null));
    }

    [Fact(DisplayName = "O perfil com composição em vigor e sem proposta tem versão vigente e não tem pendente")]
    public void ToListadoDto_ComVigenteSemPendente_IndicaSoAVigente()
    {
        PerfilListadoDto dto = PerfilMapping.ToListadoDto(CriarPerfil("teste:itens:ler"), Catalogo);

        dto.TemVersaoVigente.Should().BeTrue();
        dto.TemVersaoPendente.Should().BeFalse();
    }

    [Fact(DisplayName = "O perfil só com proposta pendente tem versão pendente e não tem vigente")]
    public void ToListadoDto_SoComPendente_IndicaSoAPendente()
    {
        Perfil perfil = CenarioDaGovernancaDaVersao.PendenteVerificada(["teste:itens:ler"]);

        PerfilListadoDto dto = PerfilMapping.ToListadoDto(perfil, Catalogo);

        dto.TemVersaoVigente.Should().BeFalse();
        dto.TemVersaoPendente.Should().BeTrue();
    }

    [Fact(DisplayName = "O perfil de governança expõe o tipo de governança")]
    public void ToListadoDto_PerfilDeGovernanca_ExpoeOTipo()
    {
        Perfil perfil = PerfisDeTeste.DeGovernanca("Gestão de acessos", "Governa os acessos.", ["teste:itens:ler"], new DoublesDePerfis.CatalogoDeTeste());

        PerfilMapping.ToListadoDto(perfil, Catalogo).Governanca.Should().Be(TipoDeGovernanca.GestorDeAcessos.ToCodigo());
    }

    [Fact(DisplayName = "O perfil desativado expõe o instante da desativação")]
    public void ToListadoDto_PerfilDesativado_ExpoeODesativadoDesde()
    {
        Perfil perfil = CriarPerfil("teste:itens:ler");
        PerfisDeTeste.Desativar(perfil, new DoublesDePerfis.CatalogoDeTeste());

        PerfilMapping.ToListadoDto(perfil, Catalogo).DesativadoDesde.Should().Be(PerfisDeTeste.Instante);
    }

    [Fact(DisplayName = "O perfil suspenso expõe o papel de quem suspendeu e as etapas exigidas")]
    public void ToListadoDto_PerfilSuspenso_ExpoeASuspensao()
    {
        Perfil perfil = CriarPerfil("teste:itens:ler");
        SuspenderPelaGestao(perfil);

        SuspensaoDoPerfilDto? suspensao = PerfilMapping.ToListadoDto(perfil, Catalogo).Suspensao;

        suspensao!.PapelDeQuemSuspendeu.Should().Be(EtapaDaVersao.GestaoDeAcessos.ToCodigo());
        suspensao.EtapasExigidasAgora.Should().Equal(EtapaDaVersaoCodigo.GestaoDeAcessos);
    }
}
