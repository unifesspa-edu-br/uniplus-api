namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class JanelaDeMigracaoOrdemDasRecusasTests
{
    private static readonly Identidade Diogo = PerfisDeTeste.Autor;
    private static readonly Identidade Marlene = PerfisDeTeste.ProtecaoDeDados;
    private static readonly Identidade Leandro = CenarioDeMigracao.Leandro;
    private static readonly Identidade Isabella = CenarioDeMigracao.Isabella;
    private static readonly DateTimeOffset Agora = CenarioDeMigracao.Abertura.AddHours(1);

    [Fact(DisplayName = "O lote com envio anterior responde lote já enviado antes de conferir os itens")]
    public void EnviarLote_JaEnviadoEItemInvalido_RespondeLoteJaEnviado()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        JanelaDeMigracao janela = cenario.Abrir();
        janela.EnviarLote(CenarioDeMigracao.Lote(false, (deLeandro, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora));

        EnviarComo(janela, cenario, Diogo, new PedidoDoLote([new ItemDoLote(Guid.NewGuid(), ResultadoDaMigracao.Reter)], false))
            .Should().Equal(MigracaoErrorCodes.LoteJaEnviado);
    }

    [Fact(DisplayName = "O item inválido do lote é recusado antes da decisão da própria concessão")]
    public void EnviarLote_ItemInvalidoEPropria_RespondeLoteInvalido()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deDiogo = cenario.Conceder(Diogo);
        JanelaDeMigracao janela = cenario.Abrir();

        EnviarComo(janela, cenario, Diogo, new PedidoDoLote([new ItemDoLote(deDiogo.Id, ResultadoDaMigracao.Reter)], false))
            .Should().Equal(MigracaoErrorCodes.LoteInvalido);
    }

    [Fact(DisplayName = "A decisão da própria concessão é recusada antes de o lote incompleto")]
    public void EnviarLote_PropriaEIncompleto_RespondeDecisaoDaPropria()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deDiogo = cenario.Conceder(Diogo);
        cenario.Conceder(Leandro);
        JanelaDeMigracao janela = cenario.Abrir();

        EnviarComo(janela, cenario, Diogo, CenarioDeMigracao.Lote(false, (deDiogo, ResultadoDaMigracao.Migrar)))
            .Should().Equal(MigracaoErrorCodes.DecisaoDaPropriaConcessao);
    }

    [Fact(DisplayName = "O lote incompleto é recusado antes da exclusão não confirmada")]
    public void EnviarLote_IncompletoESemConfirmacao_RespondeLoteIncompleto()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        cenario.Conceder(Isabella);
        JanelaDeMigracao janela = cenario.Abrir();

        EnviarComo(janela, cenario, Diogo, CenarioDeMigracao.Lote(false, (deLeandro, ResultadoDaMigracao.Recusar)))
            .Should().Equal(MigracaoErrorCodes.LoteIncompleto);
    }

    [Fact(DisplayName = "A recusa do lote sai sem alterar a janela nem a concessão")]
    public void EnviarLote_Recusado_NaoAlteraNada()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        cenario.Conceder(Isabella);
        JanelaDeMigracao janela = cenario.Abrir();

        EnviarComo(janela, cenario, Diogo, CenarioDeMigracao.Lote(true, (deLeandro, ResultadoDaMigracao.Recusar))).Should().NotBeEmpty();

        NadaMudou(janela, cenario.Concessoes);
    }

    [Fact(DisplayName = "A retida de concessão sem linha, com o lote já enviado, responde lote não enviado")]
    public void DecidirRetida_SemLinhaComLoteEnviado_RespondeLoteNaoEnviado()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        JanelaDeMigracao janela = cenario.Abrir();
        janela.EnviarLote(CenarioDeMigracao.Lote(false, (deLeandro, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora));

        Codigos(janela.DecidirRetida(new PedidoDaRetida(Guid.NewGuid(), ResultadoDaMigracao.Migrar, false), CenarioDeMigracao.Autoria(Marlene), cenario.Contexto(Agora)))
            .Should().Equal(MigracaoErrorCodes.LoteNaoEnviado);
    }

    [Fact(DisplayName = "Na etapa da proteção, a primeira concessão fora da etapa decide a recusa")]
    public void DecidirEtapaDaProtecao_VariasConcessoes_PrimeiraRecusaDecide()
    {
        (JanelaDeMigracao janela, CenarioDeMigracao cenario, Concessao aguardando, Concessao retida, Concessao jaDecidida) = ComTresSituacoes();

        CodigosDaEtapa(janela, cenario, jaDecidida, retida).Should().Equal(MigracaoErrorCodes.ConcessaoJaDecidida);
        CodigosDaEtapa(janela, cenario, retida, jaDecidida).Should().Equal(MigracaoErrorCodes.ConcessaoForaDaEtapa);
        CodigosDaEtapa(janela, cenario, aguardando, retida).Should().Equal(MigracaoErrorCodes.ConcessaoForaDaEtapa);
    }

    [Fact(DisplayName = "Na etapa da proteção, a concessão fora da etapa é recusada antes da decisão da própria concessão")]
    public void DecidirEtapaDaProtecao_ForaDaEtapaEPropria_RespondeForaDaEtapa()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao deMarlene = cenario.Conceder(Marlene);
        Concessao deLeandro = cenario.Conceder(Leandro);
        JanelaDeMigracao janela = cenario.Abrir();
        janela.EnviarLote(
            CenarioDeMigracao.Lote(true, (deMarlene, ResultadoDaMigracao.Recusar), (deLeandro, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora));

        CodigosDaEtapa(janela, cenario, deMarlene).Should().Equal(MigracaoErrorCodes.ConcessaoJaDecidida);
    }

    [Fact(DisplayName = "Na etapa da proteção, quem decidiu a gestão é recusado antes da exclusão não confirmada")]
    public void DecidirEtapaDaProtecao_MesmaPessoaESemConfirmacao_RespondeMesmaPessoa()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao deLeandro = cenario.Conceder(Leandro);
        JanelaDeMigracao janela = cenario.Abrir();
        janela.EnviarLote(CenarioDeMigracao.Lote(false, (deLeandro, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora));
        var pedido = new PedidoDaEtapaDaProtecao([new ItemDoLote(deLeandro.Id, ResultadoDaMigracao.Recusar)], false);

        Codigos(janela.DecidirEtapaDaProtecao(pedido, CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora)))
            .Should().Equal(MigracaoErrorCodes.MesmaPessoaNasDuasEtapas);
    }

    [Fact(DisplayName = "A recusa da etapa da proteção sai sem alterar a janela nem as concessões")]
    public void DecidirEtapaDaProtecao_Recusada_NaoAlteraNada()
    {
        (JanelaDeMigracao janela, CenarioDeMigracao cenario, Concessao _, Concessao retida, Concessao jaDecidida) = ComTresSituacoes();
        int lotes = janela.Lotes.Count;
        int decisoes = janela.Decisoes.Count;
        int migracoes = janela.Migracoes.Count;

        CodigosDaEtapa(janela, cenario, retida, jaDecidida).Should().NotBeEmpty();

        janela.Lotes.Should().HaveCount(lotes);
        janela.Decisoes.Should().HaveCount(decisoes);
        janela.Migracoes.Should().HaveCount(migracoes);
        janela.Encerrada.Should().BeFalse();
    }

    private static string[] Codigos(Result resultado) => [.. resultado.Errors.Select(recusa => recusa.Error.Code)];

    private static string[] EnviarComo(JanelaDeMigracao janela, CenarioDeMigracao cenario, Identidade quem, PedidoDoLote lote) =>
        Codigos(janela.EnviarLote(lote, CenarioDeMigracao.Autoria(quem), cenario.Contexto(Agora)));

    private static string[] CodigosDaEtapa(JanelaDeMigracao janela, CenarioDeMigracao cenario, params Concessao[] concessoes) =>
        Codigos(janela.DecidirEtapaDaProtecao(
            new([.. concessoes.Select(static concessao => new ItemDoLote(concessao.Id, ResultadoDaMigracao.Migrar))], false),
            CenarioDeMigracao.Autoria(Marlene),
            cenario.Contexto(Agora)));

    private static void NadaMudou(JanelaDeMigracao janela, IReadOnlyList<Concessao> concessoes)
    {
        janela.Lotes.Should().BeEmpty();
        janela.Migracoes.Should().BeEmpty();
        janela.Decisoes.Should().BeEmpty();
        janela.Encerrada.Should().BeFalse();
        concessoes.Should().OnlyContain(static concessao => concessao.RevogadaEm == null);
    }

    // Leandro aguarda a proteção de dados, Diogo ficou retido no lote dele e Isabella foi recusada.
    private static (JanelaDeMigracao Janela, CenarioDeMigracao Cenario, Concessao Aguardando, Concessao Retida, Concessao JaDecidida) ComTresSituacoes()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao aguardando = cenario.Conceder(Leandro);
        Concessao retida = cenario.Conceder(Diogo);
        Concessao jaDecidida = cenario.Conceder(Isabella);
        JanelaDeMigracao janela = cenario.Abrir();
        janela.EnviarLote(
            CenarioDeMigracao.Lote(true, (aguardando, ResultadoDaMigracao.Migrar), (jaDecidida, ResultadoDaMigracao.Recusar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora));

        return (janela, cenario, aguardando, retida, jaDecidida);
    }
}
