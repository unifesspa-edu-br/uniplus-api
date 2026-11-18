namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

public sealed class RevisaoDeMigracaoPautaTests
{
    private static readonly Identidade Diogo = PerfisDeTeste.Autor;
    private static readonly Identidade Betina = PerfisDeTeste.Gestor;
    private static readonly Identidade Marlene = PerfisDeTeste.ProtecaoDeDados;
    private static readonly Identidade Leandro = CenarioDeMigracao.Leandro;
    private static readonly DateTimeOffset Agora = CenarioDeMigracao.Abertura.AddHours(1);

    [Fact(DisplayName = "Com as decisões por enviar, a gestão decide as concessões das outras pessoas")]
    public void Gestao_DecisoesNaoEnviadas_ContaAsDeOutrasPessoas()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Conceder(Leandro);
        cenario.Conceder(Diogo);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();

        revisao.QuantidadeADecidir(EtapaDaMigracao.GestaoDeAcessos, Betina, cenario.Concessoes, Agora).Should().Be(2);
        revisao.QuantidadeADecidir(EtapaDaMigracao.GestaoDeAcessos, Diogo, cenario.Concessoes, Agora).Should().Be(1);
    }

    [Fact(DisplayName = "Quem só tem concessão própria ainda encontra as decisões por enviar, que retêm a dela")]
    public void Gestao_DecisoesNaoEnviadas_SoComAPropria_AindaTemDecisoesPorEnviar()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Conceder(Diogo);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();

        revisao.QuantidadeADecidir(EtapaDaMigracao.GestaoDeAcessos, Diogo, cenario.Concessoes, Agora).Should().Be(0);
    }

    [Fact(DisplayName = "Depois do envio das decisões, a gestão só encontra as retidas que não são dela")]
    public void Gestao_DecisoesEnviadas_ContaAsRetidasDeOutraPessoa()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        Concessao deDiogo = cenario.Conceder(Diogo);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.EnviarDecisoesDaGestao(CenarioDeMigracao.Envio(false, (deLeandro, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora))
            .IsSuccess.Should().BeTrue();

        revisao.QuantidadeADecidir(EtapaDaMigracao.GestaoDeAcessos, Betina, cenario.Concessoes, Agora).Should().Be(1);
        revisao.QuantidadeADecidir(EtapaDaMigracao.GestaoDeAcessos, Diogo, cenario.Concessoes, Agora).Should().BeNull();
        revisao.LinhaDe(deDiogo.Id)!.Situacao.Should().Be(SituacaoDaMigracao.Retida);
    }

    [Fact(DisplayName = "A proteção de dados encontra o que a gestão migrou, menos o que foi ela mesma a decidir ou é dela")]
    public void Protecao_ContaOQueAguardaAEtapa_SemAsDelaNemAsQueDecidiuNaGestao()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao deLeandro = cenario.Conceder(Leandro);
        Concessao deMarlene = cenario.Conceder(Marlene);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.EnviarDecisoesDaGestao(
                CenarioDeMigracao.Envio(false, (deLeandro, ResultadoDaMigracao.Migrar), (deMarlene, ResultadoDaMigracao.Migrar)),
                CenarioDeMigracao.Autoria(Diogo),
                cenario.Contexto(Agora))
            .IsSuccess.Should().BeTrue();

        revisao.QuantidadeADecidir(EtapaDaMigracao.ProtecaoDeDados, Marlene, cenario.Concessoes, Agora).Should().Be(1);
        revisao.QuantidadeADecidir(EtapaDaMigracao.ProtecaoDeDados, Betina, cenario.Concessoes, Agora).Should().Be(2);
        revisao.QuantidadeADecidir(EtapaDaMigracao.ProtecaoDeDados, Diogo, cenario.Concessoes, Agora).Should().BeNull();
        revisao.QuantidadeADecidir(EtapaDaMigracao.GestaoDeAcessos, Betina, cenario.Concessoes, Agora).Should().BeNull();
    }

    [Fact(DisplayName = "A proteção de dados não encontra nada antes das decisões da gestão")]
    public void Protecao_AntesDasDecisoes_NaoTemNada()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();

        revisao.QuantidadeADecidir(EtapaDaMigracao.ProtecaoDeDados, Marlene, cenario.Concessoes, Agora).Should().BeNull();
    }

    [Fact(DisplayName = "Revisão vencida, sem pendência ou encerrada não tem nada para ninguém")]
    public void RevisaoQueNaoEstaAberta_NaoTemNada()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Conceder(Leandro);
        Concessao vencendo = cenario.Conceder(Diogo, validaAte: CenarioDeMigracao.Abertura.AddHours(2));
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();

        revisao.QuantidadeADecidir(EtapaDaMigracao.GestaoDeAcessos, Betina, cenario.Concessoes, revisao.TerminaEm).Should().BeNull();
        revisao.QuantidadeADecidir(EtapaDaMigracao.GestaoDeAcessos, Betina, [], Agora).Should().BeNull();
        revisao.QuantidadeADecidir(EtapaDaMigracao.GestaoDeAcessos, Betina, cenario.Concessoes, CenarioDeMigracao.Abertura.AddHours(3)).Should().Be(1);
        vencendo.EstaAtiva(CenarioDeMigracao.Abertura.AddHours(3)).Should().BeFalse();
    }
}
