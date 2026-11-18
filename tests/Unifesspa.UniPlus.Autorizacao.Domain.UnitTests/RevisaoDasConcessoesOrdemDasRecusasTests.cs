namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RevisaoDasConcessoesDoPerfilOrdemDasRecusasTests
{
    private static readonly Identidade Diogo = PerfisDeTeste.Autor;
    private static readonly Identidade Marlene = PerfisDeTeste.ProtecaoDeDados;
    private static readonly Identidade Leandro = CenarioDeMigracao.Leandro;
    private static readonly Identidade Isabella = CenarioDeMigracao.Isabella;
    private static readonly DateTimeOffset Agora = CenarioDeMigracao.Abertura.AddHours(1);

    [Fact(DisplayName = "O envio com envio anterior responde envio já enviado antes de conferir os itens")]
    public void EnviarEnvio_JaEnviadoEItemInvalido_RespondeEnvioJaEnviado()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.EnviarDecisoesDaGestao(CenarioDeMigracao.Envio(false, (deLeandro, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora));

        EnviarComo(revisao, cenario, Diogo, new PedidoDoEnvio([new ItemDoEnvio(Guid.NewGuid(), ResultadoDaMigracao.Reter)], false))
            .Should().Equal(RevisaoDasConcessoesErrorCodes.DecisoesJaEnviadas);
    }

    [Fact(DisplayName = "O item inválido do envio é recusado antes da decisão da própria concessão")]
    public void EnviarEnvio_ItemInvalidoEPropria_RespondeEnvioInvalido()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deDiogo = cenario.Conceder(Diogo);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();

        EnviarComo(revisao, cenario, Diogo, new PedidoDoEnvio([new ItemDoEnvio(deDiogo.Id, ResultadoDaMigracao.Reter)], false))
            .Should().Equal(RevisaoDasConcessoesErrorCodes.DecisoesInvalidas);
    }

    [Fact(DisplayName = "A decisão da própria concessão é recusada antes de o envio incompleto")]
    public void EnviarEnvio_PropriaEIncompleto_RespondeDecisaoDaPropria()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deDiogo = cenario.Conceder(Diogo);
        cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();

        EnviarComo(revisao, cenario, Diogo, CenarioDeMigracao.Envio(false, (deDiogo, ResultadoDaMigracao.Migrar)))
            .Should().Equal(RevisaoDasConcessoesErrorCodes.DecisaoDaPropriaConcessao);
    }

    [Fact(DisplayName = "O envio incompleto é recusado antes da exclusão não confirmada")]
    public void EnviarEnvio_IncompletoESemConfirmacao_RespondeEnvioIncompleto()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        cenario.Conceder(Isabella);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();

        EnviarComo(revisao, cenario, Diogo, CenarioDeMigracao.Envio(false, (deLeandro, ResultadoDaMigracao.Recusar)))
            .Should().Equal(RevisaoDasConcessoesErrorCodes.DecisoesIncompletas);
    }

    [Fact(DisplayName = "A recusa do envio sai sem alterar a revisão nem a concessão")]
    public void EnviarEnvio_Recusado_NaoAlteraNada()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        cenario.Conceder(Isabella);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();

        EnviarComo(revisao, cenario, Diogo, CenarioDeMigracao.Envio(true, (deLeandro, ResultadoDaMigracao.Recusar))).Should().NotBeEmpty();

        NadaMudou(revisao, cenario.Concessoes);
    }

    [Fact(DisplayName = "A retida de concessão sem linha, com o envio já enviado, responde envio não enviado")]
    public void DecidirRetida_SemLinhaComEnvioEnviado_RespondeEnvioNaoEnviado()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.EnviarDecisoesDaGestao(CenarioDeMigracao.Envio(false, (deLeandro, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora));

        Codigos(revisao.DecidirRetida(new PedidoDaRetida(Guid.NewGuid(), ResultadoDaMigracao.Migrar, false), CenarioDeMigracao.Autoria(Marlene), cenario.Contexto(Agora)))
            .Should().Equal(RevisaoDasConcessoesErrorCodes.DecisoesNaoEnviadas);
    }

    [Fact(DisplayName = "Na etapa da proteção, a primeira concessão fora da etapa decide a recusa")]
    public void DecidirEtapaDaProtecao_VariasConcessoes_PrimeiraRecusaDecide()
    {
        (RevisaoDasConcessoesDoPerfil revisao, CenarioDeMigracao cenario, Concessao aguardando, Concessao retida, Concessao jaDecidida) = ComTresSituacoes();

        CodigosDaEtapa(revisao, cenario, jaDecidida, retida).Should().Equal(RevisaoDasConcessoesErrorCodes.ConcessaoJaDecidida);
        CodigosDaEtapa(revisao, cenario, retida, jaDecidida).Should().Equal(RevisaoDasConcessoesErrorCodes.ConcessaoForaDaEtapa);
        CodigosDaEtapa(revisao, cenario, aguardando, retida).Should().Equal(RevisaoDasConcessoesErrorCodes.ConcessaoForaDaEtapa);
    }

    [Fact(DisplayName = "Na etapa da proteção, a concessão fora da etapa é recusada antes da decisão da própria concessão")]
    public void DecidirEtapaDaProtecao_ForaDaEtapaEPropria_RespondeForaDaEtapa()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao deMarlene = cenario.Conceder(Marlene);
        Concessao deLeandro = cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.EnviarDecisoesDaGestao(
            CenarioDeMigracao.Envio(true, (deMarlene, ResultadoDaMigracao.Recusar), (deLeandro, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora));

        CodigosDaEtapa(revisao, cenario, deMarlene).Should().Equal(RevisaoDasConcessoesErrorCodes.ConcessaoJaDecidida);
    }

    [Fact(DisplayName = "Na etapa da proteção, quem decidiu a gestão é recusado antes da exclusão não confirmada")]
    public void DecidirEtapaDaProtecao_MesmaPessoaESemConfirmacao_RespondeMesmaPessoa()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao deLeandro = cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.EnviarDecisoesDaGestao(CenarioDeMigracao.Envio(false, (deLeandro, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora));
        var pedido = new PedidoDaEtapaDaProtecao([new ItemDoEnvio(deLeandro.Id, ResultadoDaMigracao.Recusar)], false);

        Codigos(revisao.DecidirEtapaDaProtecao(pedido, CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora)))
            .Should().Equal(RevisaoDasConcessoesErrorCodes.MesmaPessoaNasDuasEtapas);
    }

    [Fact(DisplayName = "A recusa da etapa da proteção sai sem alterar a revisão nem as concessões")]
    public void DecidirEtapaDaProtecao_Recusada_NaoAlteraNada()
    {
        (RevisaoDasConcessoesDoPerfil revisao, CenarioDeMigracao cenario, Concessao _, Concessao retida, Concessao jaDecidida) = ComTresSituacoes();
        int envios = revisao.Envios.Count;
        int decisoes = revisao.Decisoes.Count;
        int migracoes = revisao.Migracoes.Count;

        CodigosDaEtapa(revisao, cenario, retida, jaDecidida).Should().NotBeEmpty();

        revisao.Envios.Should().HaveCount(envios);
        revisao.Decisoes.Should().HaveCount(decisoes);
        revisao.Migracoes.Should().HaveCount(migracoes);
        revisao.Encerrada.Should().BeFalse();
    }

    private static string[] Codigos(Result resultado) => [.. resultado.Errors.Select(recusa => recusa.Error.Code)];

    private static string[] EnviarComo(RevisaoDasConcessoesDoPerfil revisao, CenarioDeMigracao cenario, Identidade quem, PedidoDoEnvio envio) =>
        Codigos(revisao.EnviarDecisoesDaGestao(envio, CenarioDeMigracao.Autoria(quem), cenario.Contexto(Agora)));

    private static string[] CodigosDaEtapa(RevisaoDasConcessoesDoPerfil revisao, CenarioDeMigracao cenario, params Concessao[] concessoes) =>
        Codigos(revisao.DecidirEtapaDaProtecao(
            new([.. concessoes.Select(static concessao => new ItemDoEnvio(concessao.Id, ResultadoDaMigracao.Migrar))], false),
            CenarioDeMigracao.Autoria(Marlene),
            cenario.Contexto(Agora)));

    private static void NadaMudou(RevisaoDasConcessoesDoPerfil revisao, IReadOnlyList<Concessao> concessoes)
    {
        revisao.Envios.Should().BeEmpty();
        revisao.Migracoes.Should().BeEmpty();
        revisao.Decisoes.Should().BeEmpty();
        revisao.Encerrada.Should().BeFalse();
        concessoes.Should().OnlyContain(static concessao => concessao.RevogadaEm == null);
    }

    // Leandro aguarda a proteção de dados, Diogo ficou retido no envio dele e Isabella foi recusada.
    private static (RevisaoDasConcessoesDoPerfil Revisao, CenarioDeMigracao Cenario, Concessao Aguardando, Concessao Retida, Concessao JaDecidida) ComTresSituacoes()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao aguardando = cenario.Conceder(Leandro);
        Concessao retida = cenario.Conceder(Diogo);
        Concessao jaDecidida = cenario.Conceder(Isabella);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.EnviarDecisoesDaGestao(
            CenarioDeMigracao.Envio(true, (aguardando, ResultadoDaMigracao.Migrar), (jaDecidida, ResultadoDaMigracao.Recusar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora));

        return (revisao, cenario, aguardando, retida, jaDecidida);
    }
}
