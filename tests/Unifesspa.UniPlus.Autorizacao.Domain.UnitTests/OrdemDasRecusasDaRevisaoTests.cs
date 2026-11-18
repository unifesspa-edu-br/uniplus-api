namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class OrdemDasRecusasDaRevisaoTests
{
    private static readonly DateTimeOffset Agora = CenarioDeMigracao.Abertura.AddHours(1);

    [Fact]
    public void EnviarDecisoesDaGestao_VariasCondicoesInvalidas_RespeitaAOrdemSemAlterarConcessoes()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao propria = cenario.Conceder(PerfisDeTeste.Autor);
        Concessao outra = cenario.Conceder(CenarioDeMigracao.Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        AutoriaDaVersao autoria = CenarioDeMigracao.Autoria(PerfisDeTeste.Autor);

        var invalido = new PedidoDoEnvio([new(propria.Id, null)], false);
        Codigo(revisao.EnviarDecisoesDaGestao(invalido, autoria, cenario.Contexto(Agora))).Should().Be(RevisaoDasConcessoesErrorCodes.DecisoesInvalidas);
        Codigo(revisao.EnviarDecisoesDaGestao(CenarioDeMigracao.Envio(false, (propria, ResultadoDaMigracao.Recusar)), autoria, cenario.Contexto(Agora)))
            .Should().Be(RevisaoDasConcessoesErrorCodes.DecisaoDaPropriaConcessao);
        Codigo(revisao.EnviarDecisoesDaGestao(CenarioDeMigracao.Envio(false), autoria, cenario.Contexto(Agora))).Should().Be(RevisaoDasConcessoesErrorCodes.DecisoesIncompletas);

        NenhumaDecisaoRegistrada(revisao);
        ConcessoesInalteradas(cenario, propria, outra);
    }

    [Fact]
    public void DecidirRetida_SemEnvioEDecisaoInvalida_RecusaSituacaoSemAlterarConcessao()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao propria = cenario.Conceder(PerfisDeTeste.Autor);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();

        Result resultado = revisao.DecidirRetida(new(propria.Id, null, false), CenarioDeMigracao.Autoria(PerfisDeTeste.Autor), cenario.Contexto(Agora));

        Codigo(resultado).Should().Be(RevisaoDasConcessoesErrorCodes.DecisoesNaoEnviadas);
        revisao.Decisoes.Should().BeEmpty();
        revisao.Envios.Should().BeEmpty();
        propria.VersaoId.Should().Be(cenario.VersaoAntiga.Id);
        propria.RevogadaEm.Should().BeNull();
    }

    [Fact]
    public void DecidirEtapaDaProtecao_MesmaPessoaESemConfirmacao_RecusaPessoaSemAlterarDecisoes()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao concessao = cenario.Conceder(CenarioDeMigracao.Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        AutoriaDaVersao autoria = CenarioDeMigracao.Autoria(PerfisDeTeste.Autor);
        revisao.EnviarDecisoesDaGestao(CenarioDeMigracao.Envio(false, (concessao, ResultadoDaMigracao.Migrar)), autoria, cenario.Contexto(Agora)).IsSuccess.Should().BeTrue();
        Result resultado = revisao.DecidirEtapaDaProtecao(new([new(concessao.Id, ResultadoDaMigracao.Recusar)], false), autoria, cenario.Contexto(Agora));

        Codigo(resultado).Should().Be(RevisaoDasConcessoesErrorCodes.MesmaPessoaNasDuasEtapas);
        revisao.Decisoes.Should().ContainSingle();
        revisao.Envios.Should().ContainSingle();
        revisao.LinhaDe(concessao.Id)!.Situacao.Should().Be(SituacaoDaMigracao.AguardandoProtecaoDeDados);
        concessao.VersaoId.Should().Be(cenario.VersaoAntiga.Id);
        concessao.RevogadaEm.Should().BeNull();
    }

    [Fact]
    public void EncerrarManualmente_MotivoInvalidoESemConfirmacao_RecusaMotivoSemEncerrar()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao concessao = cenario.Conceder(CenarioDeMigracao.Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();

        Result resultado = revisao.EncerrarManualmente(new(CenarioDeMigracao.Autoria(PerfisDeTeste.Gestor), " "), false, cenario.Contexto(Agora));

        Codigo(resultado).Should().Be(RevisaoDasConcessoesErrorCodes.MotivoInvalido);
        revisao.Encerrada.Should().BeFalse();
        revisao.Decisoes.Should().BeEmpty();
        concessao.RevogadaEm.Should().BeNull();
    }

    private static string Codigo(Result resultado) => resultado.Errors.Single().Error.Code;

    private static void NenhumaDecisaoRegistrada(RevisaoDasConcessoesDoPerfil revisao)
    {
        revisao.Envios.Should().BeEmpty();
        revisao.Decisoes.Should().BeEmpty();
        revisao.Migracoes.Should().BeEmpty();
    }

    private static void ConcessoesInalteradas(CenarioDeMigracao cenario, Concessao propria, Concessao outra)
    {
        propria.VersaoId.Should().Be(cenario.VersaoAntiga.Id);
        outra.VersaoId.Should().Be(cenario.VersaoAntiga.Id);
        propria.RevogadaEm.Should().BeNull();
        outra.RevogadaEm.Should().BeNull();
    }
}
