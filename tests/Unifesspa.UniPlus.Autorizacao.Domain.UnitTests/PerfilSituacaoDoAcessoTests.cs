namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

public sealed class PerfilSituacaoDoAcessoTests
{
    private const string Consultar = "selecao:processos:consultar";
    private const string Descricao = "Descrição válida.";

    private readonly CatalogoMutavelDeTeste _catalogo = new();

    [Fact(DisplayName = "Perfil comum suspenso com versão em vigor está suspenso")]
    public void PerfilSuspensoComVersaoEmVigor_EstaSuspenso()
    {
        Perfil perfil = ComVersaoEmVigor();
        Suspender(perfil);

        perfil.SituacaoDoAcesso.Should().Be(SituacaoDoAcessoDoPerfil.Suspenso);
        perfil.TemComposicaoEmVigor.Should().BeTrue();
    }

    [Fact(DisplayName = "Perfil comum suspenso sem versão em vigor está suspenso")]
    public void PerfilSuspensoSemVersaoEmVigor_EstaSuspenso()
    {
        Perfil perfil = SemVersaoEmVigor();
        Suspender(perfil);

        perfil.SituacaoDoAcesso.Should().Be(SituacaoDoAcessoDoPerfil.Suspenso);
        perfil.TemComposicaoEmVigor.Should().BeFalse();
    }

    [Fact(DisplayName = "Perfil comum com versão em vigor e sem suspensão está em vigor")]
    public void PerfilComumComVersaoEmVigor_EstaEmVigor() =>
        ComVersaoEmVigor().SituacaoDoAcesso.Should().Be(SituacaoDoAcessoDoPerfil.EmVigor);

    [Fact(DisplayName = "Perfil de governança está em vigor, sem versão")]
    public void PerfilDeGovernanca_EstaEmVigor()
    {
        Perfil perfil = PerfisDeTeste.DeGovernanca("Gestor de acessos", Descricao, [Consultar], _catalogo);

        perfil.VersaoVigente.Should().BeNull();
        perfil.SituacaoDoAcesso.Should().Be(SituacaoDoAcessoDoPerfil.EmVigor);
    }

    [Fact(DisplayName = "Perfil comum sem suspensão e sem versão em vigor está sem versão em vigor")]
    public void PerfilComumSemVersaoEmVigor_EstaSemVersaoEmVigor() =>
        SemVersaoEmVigor().SituacaoDoAcesso.Should().Be(SituacaoDoAcessoDoPerfil.SemVersaoEmVigor);

    [Fact(DisplayName = "Perfil só desativado, com versão em vigor, continua em vigor")]
    public void PerfilSoDesativado_ContinuaEmVigor()
    {
        Perfil perfil = ComVersaoEmVigor();
        PerfisDeTeste.Desativar(perfil, _catalogo);

        perfil.Ativo.Should().BeFalse();
        perfil.SituacaoDoAcesso.Should().Be(SituacaoDoAcessoDoPerfil.EmVigor);
    }

    [Fact(DisplayName = "Perfil desativado e suspenso está suspenso e segue desativado")]
    public void PerfilDesativadoESuspenso_EstaSuspenso()
    {
        Perfil perfil = ComVersaoEmVigor();
        PerfisDeTeste.Desativar(perfil, _catalogo);
        Suspender(perfil);

        perfil.SituacaoDoAcesso.Should().Be(SituacaoDoAcessoDoPerfil.Suspenso);
        perfil.Ativo.Should().BeFalse();
        perfil.DesativacaoAberta.Should().NotBeNull();
    }

    [Fact(DisplayName = "Perfil desativado, sem suspensão e sem versão em vigor está sem versão em vigor")]
    public void PerfilDesativadoSemVersaoEmVigor_EstaSemVersaoEmVigor()
    {
        Perfil perfil = SemVersaoEmVigor();
        PerfisDeTeste.Desativar(perfil, _catalogo);

        perfil.Ativo.Should().BeFalse();
        perfil.SituacaoDoAcesso.Should().Be(SituacaoDoAcessoDoPerfil.SemVersaoEmVigor);
    }

    [Fact(DisplayName = "Perfil suspenso e desativado, sem versão em vigor, está suspenso")]
    public void PerfilSuspensoEDesativadoSemVersaoEmVigor_EstaSuspenso()
    {
        Perfil perfil = SemVersaoEmVigor();
        PerfisDeTeste.Desativar(perfil, _catalogo);
        Suspender(perfil);

        perfil.Ativo.Should().BeFalse();
        perfil.SituacaoDoAcesso.Should().Be(SituacaoDoAcessoDoPerfil.Suspenso);
    }

    [Fact(DisplayName = "Perfil com a suspensão retomada volta a estar em vigor")]
    public void SuspensaoRetomada_PerfilVoltaAEmVigor()
    {
        Perfil perfil = ComVersaoEmVigor();
        Suspender(perfil);
        perfil.DecidirRetomada(new ContextoDaRetomada(
            PerfisDeTeste.ProtecaoDeDados, DecisorTemConcessao: true, EtapaDaVersao.GestaoDeAcessos, MotivoInformado.Normalizar("Retomada avaliada."), _catalogo, PerfisDeTeste.Instante))
            .IsSuccess.Should().BeTrue();

        perfil.SuspensaoAberta.Should().BeNull();
        perfil.SituacaoDoAcesso.Should().Be(SituacaoDoAcessoDoPerfil.EmVigor);
    }

    private Perfil ComVersaoEmVigor() => PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], _catalogo);

    private Perfil SemVersaoEmVigor() => PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], _catalogo);

    private void Suspender(Perfil perfil) =>
        perfil.Suspender(PerfisDeTeste.Gestor, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.MotivoDaContencao, PerfisDeTeste.Instante, _catalogo)
            .IsSuccess.Should().BeTrue();
}
