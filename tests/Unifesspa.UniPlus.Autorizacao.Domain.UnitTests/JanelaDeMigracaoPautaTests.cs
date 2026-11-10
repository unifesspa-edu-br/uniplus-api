namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

public sealed class JanelaDeMigracaoPautaTests
{
    private static readonly Identidade Diogo = PerfisDeTeste.Autor;
    private static readonly Identidade Betina = PerfisDeTeste.Gestora;
    private static readonly Identidade Marlene = PerfisDeTeste.ProtecaoDeDados;
    private static readonly Identidade Leandro = CenarioDeMigracao.Leandro;
    private static readonly DateTimeOffset Agora = CenarioDeMigracao.Abertura.AddHours(1);

    [Fact(DisplayName = "Com o lote por enviar, a gestão decide as concessões das outras pessoas")]
    public void Gestao_LoteNaoEnviado_ContaAsDeOutrasPessoas()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Conceder(Leandro);
        cenario.Conceder(Diogo);
        JanelaDeMigracao janela = cenario.Abrir();

        janela.QuantidadeADecidir(EtapaDaMigracao.GestaoDeAcessos, Betina, cenario.Concessoes, Agora).Should().Be(2);
        janela.QuantidadeADecidir(EtapaDaMigracao.GestaoDeAcessos, Diogo, cenario.Concessoes, Agora).Should().Be(1);
    }

    [Fact(DisplayName = "Quem só tem concessão própria ainda encontra o lote por enviar, que retém a dela")]
    public void Gestao_LoteNaoEnviado_SoComAPropria_AindaTemOLote()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Conceder(Diogo);
        JanelaDeMigracao janela = cenario.Abrir();

        janela.QuantidadeADecidir(EtapaDaMigracao.GestaoDeAcessos, Diogo, cenario.Concessoes, Agora).Should().Be(0);
    }

    [Fact(DisplayName = "Depois do lote, a gestão só encontra as retidas que não são dela")]
    public void Gestao_LoteEnviado_ContaAsRetidasDeOutraPessoa()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        Concessao deDiogo = cenario.Conceder(Diogo);
        JanelaDeMigracao janela = cenario.Abrir();
        janela.EnviarLote(CenarioDeMigracao.Lote(false, (deLeandro, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora))
            .IsSuccess.Should().BeTrue();

        janela.QuantidadeADecidir(EtapaDaMigracao.GestaoDeAcessos, Betina, cenario.Concessoes, Agora).Should().Be(1);
        janela.QuantidadeADecidir(EtapaDaMigracao.GestaoDeAcessos, Diogo, cenario.Concessoes, Agora).Should().BeNull();
        janela.LinhaDe(deDiogo.Id)!.Situacao.Should().Be(SituacaoDaMigracao.Retida);
    }

    [Fact(DisplayName = "A proteção de dados encontra o que a gestão migrou, menos o que foi ela mesma a decidir ou é dela")]
    public void Protecao_ContaOQueAguardaAEtapa_SemAsDelaNemAsQueDecidiuNaGestao()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao deLeandro = cenario.Conceder(Leandro);
        Concessao deMarlene = cenario.Conceder(Marlene);
        JanelaDeMigracao janela = cenario.Abrir();
        janela.EnviarLote(
                CenarioDeMigracao.Lote(false, (deLeandro, ResultadoDaMigracao.Migrar), (deMarlene, ResultadoDaMigracao.Migrar)),
                CenarioDeMigracao.Autoria(Diogo),
                cenario.Contexto(Agora))
            .IsSuccess.Should().BeTrue();

        janela.QuantidadeADecidir(EtapaDaMigracao.ProtecaoDeDados, Marlene, cenario.Concessoes, Agora).Should().Be(1);
        janela.QuantidadeADecidir(EtapaDaMigracao.ProtecaoDeDados, Betina, cenario.Concessoes, Agora).Should().Be(2);
        janela.QuantidadeADecidir(EtapaDaMigracao.ProtecaoDeDados, Diogo, cenario.Concessoes, Agora).Should().BeNull();
        janela.QuantidadeADecidir(EtapaDaMigracao.GestaoDeAcessos, Betina, cenario.Concessoes, Agora).Should().BeNull();
    }

    [Fact(DisplayName = "A proteção de dados não encontra nada antes do lote da gestão")]
    public void Protecao_AntesDoLote_NaoTemNada()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        cenario.Conceder(Leandro);
        JanelaDeMigracao janela = cenario.Abrir();

        janela.QuantidadeADecidir(EtapaDaMigracao.ProtecaoDeDados, Marlene, cenario.Concessoes, Agora).Should().BeNull();
    }

    [Fact(DisplayName = "Janela vencida, sem pendência ou encerrada não tem nada para ninguém")]
    public void JanelaQueNaoEstaAberta_NaoTemNada()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Conceder(Leandro);
        Concessao vencendo = cenario.Conceder(Diogo, validaAte: CenarioDeMigracao.Abertura.AddHours(2));
        JanelaDeMigracao janela = cenario.Abrir();

        janela.QuantidadeADecidir(EtapaDaMigracao.GestaoDeAcessos, Betina, cenario.Concessoes, janela.Prazo).Should().BeNull();
        janela.QuantidadeADecidir(EtapaDaMigracao.GestaoDeAcessos, Betina, [], Agora).Should().BeNull();
        janela.QuantidadeADecidir(EtapaDaMigracao.GestaoDeAcessos, Betina, cenario.Concessoes, CenarioDeMigracao.Abertura.AddHours(3)).Should().Be(1);
        vencendo.EstaAtiva(CenarioDeMigracao.Abertura.AddHours(3)).Should().BeFalse();
    }
}
