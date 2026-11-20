namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
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
            perfil.Id, perfil.Nome, perfil.Descricao, true, null, ValoresCanonicos.De(ClassificacaoDeProtecao.Interno), Esperada(SituacaoDoAcessoDoPerfil.EmVigor), true, null, null));
    }

    [Fact(DisplayName = "O perfil com composição em vigor e sem proposta está em vigor e não tem pendente")]
    public void ToListadoDto_ComVigenteSemPendente_IndicaSoAVigente()
    {
        PerfilListadoDto dto = PerfilMapping.ToListadoDto(CriarPerfil("teste:itens:ler"), Catalogo);

        dto.SituacaoDoAcesso.Should().Be(Esperada(SituacaoDoAcessoDoPerfil.EmVigor));
        dto.TemVersaoPendente.Should().BeFalse();
    }

    [Fact(DisplayName = "O perfil só com proposta pendente está sem versão em vigor e tem pendente")]
    public void ToListadoDto_SoComPendente_IndicaSoAPendente()
    {
        Perfil perfil = CenarioDaGovernancaDaVersao.PendenteVerificada(["teste:itens:ler"]);

        PerfilListadoDto dto = PerfilMapping.ToListadoDto(perfil, Catalogo);

        dto.SituacaoDoAcesso.Should().Be(Esperada(SituacaoDoAcessoDoPerfil.SemVersaoEmVigor));
        dto.TemVersaoPendente.Should().BeTrue();
    }

    [Fact(DisplayName = "O perfil suspenso é listado como suspenso, com ou sem versão em vigor")]
    public void ToListadoDto_PerfilSuspenso_ListaComoSuspenso()
    {
        Perfil comVigente = CriarPerfil("teste:itens:ler");
        Perfil semVigente = CenarioDaGovernancaDaVersao.PendenteVerificada(["teste:itens:ler"]);
        SuspenderPelaGestao(comVigente);
        SuspenderPelaGestao(semVigente);

        PerfilMapping.ToListadoDto(comVigente, Catalogo).SituacaoDoAcesso.Should().Be(Esperada(SituacaoDoAcessoDoPerfil.Suspenso));
        PerfilMapping.ToListadoDto(semVigente, Catalogo).SituacaoDoAcesso.Should().Be(Esperada(SituacaoDoAcessoDoPerfil.Suspenso));
    }

    [Fact(DisplayName = "O perfil de governança é listado em vigor, sem suspensão nem desativação")]
    public void ToListadoDto_PerfilDeGovernanca_ListaEmVigor()
    {
        Perfil perfil = PerfisDeTeste.DeGovernanca("Gestão de acessos", "Governa os acessos.", ["teste:itens:ler"], new DoublesDePerfis.CatalogoDeTeste());

        PerfilListadoDto dto = PerfilMapping.ToListadoDto(perfil, Catalogo);

        dto.SituacaoDoAcesso.Should().Be(Esperada(SituacaoDoAcessoDoPerfil.EmVigor));
        dto.Suspensao.Should().BeNull();
        dto.DesativadoDesde.Should().BeNull();
    }

    [Fact(DisplayName = "O perfil só desativado é listado em vigor e o desativado e suspenso, como suspenso, ambos com a desativação")]
    public void ToListadoDto_PerfilDesativado_DesativacaoNaoMudaASituacaoDoAcesso()
    {
        Perfil soDesativado = CriarPerfil("teste:itens:ler");
        Perfil desativadoESuspenso = CriarPerfil("teste:itens:ler");
        PerfisDeTeste.Desativar(soDesativado, new DoublesDePerfis.CatalogoDeTeste());
        PerfisDeTeste.Desativar(desativadoESuspenso, new DoublesDePerfis.CatalogoDeTeste());
        SuspenderPelaGestao(desativadoESuspenso);

        PerfilListadoDto emVigor = PerfilMapping.ToListadoDto(soDesativado, Catalogo);
        PerfilListadoDto suspenso = PerfilMapping.ToListadoDto(desativadoESuspenso, Catalogo);

        emVigor.SituacaoDoAcesso.Should().Be(Esperada(SituacaoDoAcessoDoPerfil.EmVigor));
        emVigor.DesativadoDesde.Should().Be(PerfisDeTeste.Instante);
        suspenso.SituacaoDoAcesso.Should().Be(Esperada(SituacaoDoAcessoDoPerfil.Suspenso));
        suspenso.DesativadoDesde.Should().Be(PerfisDeTeste.Instante);
        suspenso.Ativo.Should().BeFalse();
    }

    [Fact(DisplayName = "A listagem e o detalhe devolvem a mesma situação do acesso nas três situações")]
    public void ListagemEDetalhe_MesmoPerfil_MesmaSituacaoDoAcesso()
    {
        Perfil suspenso = CriarPerfil("teste:itens:ler");
        SuspenderPelaGestao(suspenso);
        Perfil[] perfis = [suspenso, CriarPerfil("teste:itens:ler"), CenarioDaGovernancaDaVersao.PendenteVerificada(["teste:itens:ler"])];

        CodigoComRotulo<SituacaoDoAcessoDoPerfil>[] naListagem = [.. perfis.Select(p => PerfilMapping.ToListadoDto(p, Catalogo).SituacaoDoAcesso)];
        CodigoComRotulo<SituacaoDoAcessoDoPerfil>[] noDetalhe = [.. perfis.Select(p => PerfilMapping.ToDto(p, Catalogo).SituacaoDoAcesso)];

        naListagem.Should().Equal(noDetalhe);
        naListagem.Should().Equal(
            Esperada(SituacaoDoAcessoDoPerfil.Suspenso), Esperada(SituacaoDoAcessoDoPerfil.EmVigor), Esperada(SituacaoDoAcessoDoPerfil.SemVersaoEmVigor));
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

    private static CodigoComRotulo<SituacaoDoAcessoDoPerfil> Esperada(SituacaoDoAcessoDoPerfil valor) => CodigoComRotulo<SituacaoDoAcessoDoPerfil>.De(valor);
}
