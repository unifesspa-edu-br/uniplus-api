namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class JanelaDeMigracaoLoteTests
{
    private static readonly Identidade Diogo = PerfisDeTeste.Autor;
    private static readonly Identidade Betina = PerfisDeTeste.Gestor;
    private static readonly Identidade Marlene = PerfisDeTeste.ProtecaoDeDados;
    private static readonly Identidade Leandro = CenarioDeMigracao.Leandro;
    private static readonly Identidade Isabella = CenarioDeMigracao.Isabella;
    private static readonly DateTimeOffset Agora = CenarioDeMigracao.Abertura.AddHours(1);

    [Fact(DisplayName = "A janela abre com o prazo de 72 horas corridas e a versão nova como alvo")]
    public void Abrir_GravaPrazoEAlvo()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Conceder(Leandro);

        JanelaDeMigracao janela = cenario.Abrir();

        janela.Prazo.Should().Be(CenarioDeMigracao.Abertura.AddHours(72));
        janela.VersaoAlvoId.Should().Be(cenario.VersaoNova!.Id);
        janela.NumeroDaVersaoNova.Should().Be(2);
        janela.Encerrada.Should().BeFalse();
    }

    [Fact(DisplayName = "Elegível é a concessão ativa do perfil fora da versão-alvo")]
    public void Elegivel_SoAtivaForaDoAlvo()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao ativa = cenario.Conceder(Leandro);
        Concessao revogada = cenario.Conceder(Isabella);
        Concessao vencida = cenario.Conceder(Diogo, validaAte: CenarioDeMigracao.Abertura.AddHours(2));
        revogada.Revogar(new AtoDoAgente(Diogo, CenarioDeMigracao.Abertura.AddMinutes(-1)), "Teste de revogação.").IsSuccess.Should().BeTrue();
        JanelaDeMigracao janela = cenario.Abrir();
        DateTimeOffset depoisDoVencimento = CenarioDeMigracao.Abertura.AddHours(3);

        janela.Elegivel(ativa, depoisDoVencimento).Should().BeTrue();
        janela.Elegivel(revogada, depoisDoVencimento).Should().BeFalse();
        janela.Elegivel(vencida, depoisDoVencimento).Should().BeFalse();
        janela.EstaAberta(depoisDoVencimento, janela.Pendencias(cenario.Concessoes, depoisDoVencimento)).Should().BeTrue();
        janela.EstaAberta(janela.Prazo, 1).Should().BeFalse();
        janela.EstaAberta(Agora, 0).Should().BeFalse();
    }

    [Fact(DisplayName = "O lote migra e recusa cada concessão e a recusa revoga pelo sistema")]
    public void EnviarLote_MigraERecusa()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        (Concessao paraMigrar, Concessao paraRecusar, JanelaDeMigracao janela) = MigrarERecusar(cenario);

        paraMigrar.VersaoId.Should().Be(cenario.VersaoNova!.Id);
        paraRecusar.RevogadaEm.Should().Be(Agora);
        paraRecusar.RevogadaPelaJanelaId.Should().Be(janela.Id);
        paraRecusar.MotivoRevogacao.Should().Be("recusado na migração");
        janela.LinhaDe(paraMigrar.Id)!.Situacao.Should().Be(SituacaoDaMigracao.Migrada);
        janela.LinhaDe(paraRecusar.Id)!.Situacao.Should().Be(SituacaoDaMigracao.Recusada);
    }

    [Fact(DisplayName = "O lote que decide todas as concessões registra quem decidiu e conclui a janela")]
    public void EnviarLote_TodasDecididas_ConcluiAJanela()
    {
        (_, _, JanelaDeMigracao janela) = MigrarERecusar(new CenarioDeMigracao(protegido: false));

        janela.Decisoes.Should().OnlyContain(d => d.Decisor == Diogo && d.Ator == AtorDaMigracao.Humano && d.LoteDeOrigemId == janela.LoteDaGestao!.Id);
        janela.Encerrada.Should().BeTrue();
        janela.Forma.Should().Be(FormaDoEncerramento.Concluida);
    }

    [Fact(DisplayName = "As concessões do próprio gestor ficam retidas, em qualquer escopo, e o gestor não aparece como decisor delas")]
    public void EnviarLote_ConcessoesDoGestorFicamRetidas()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao daCeps = cenario.Conceder(Diogo, Guid.NewGuid());
        Concessao daCtic = cenario.Conceder(Diogo, Guid.NewGuid());
        JanelaDeMigracao janela = AbrirELote(cenario, cenario.Conceder(Leandro));

        janela.LinhaDe(daCeps.Id)!.Situacao.Should().Be(SituacaoDaMigracao.Retida);
        janela.LinhaDe(daCtic.Id)!.Situacao.Should().Be(SituacaoDaMigracao.Retida);
        janela.Decisoes.Where(d => d.ConcessaoId == daCeps.Id).Should().ContainSingle()
            .Which.Should().Match<DecisaoDaMigracao>(d => d.Ator == AtorDaMigracao.Sistema && d.Resultado == ResultadoDaMigracao.Reter && d.Decisor == null);
        janela.Encerrada.Should().BeFalse();
    }

    [Fact(DisplayName = "A concessão retida é decidida por outra pessoa, que não altera o que o lote decidiu")]
    public void DecidirRetida_OutraPessoaMigra()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deDiogo = cenario.Conceder(Diogo);
        Concessao deLeandro = cenario.Conceder(Leandro);
        JanelaDeMigracao janela = AbrirELote(cenario, deLeandro);

        DecidirRetida(janela, cenario, deDiogo).IsSuccess.Should().BeTrue();

        deDiogo.VersaoId.Should().Be(cenario.VersaoNova!.Id);
        janela.LinhaDe(deLeandro.Id)!.Situacao.Should().Be(SituacaoDaMigracao.Migrada);
        janela.Decisoes[^1].Decisor.Should().Be(Betina);
        janela.Decisoes[^1].EnvioId.Should().BeNull();
        janela.Encerrada.Should().BeTrue();
    }

    [Fact(DisplayName = "A retida com acréscimo protegido aguarda a etapa de proteção de dados, sem dispensa para nenhum beneficiário")]
    public void DecidirRetida_Protegida_AguardaAProtecao()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao deDiogo = cenario.Conceder(Diogo);
        JanelaDeMigracao janela = AbrirELote(cenario, cenario.Conceder(Leandro));

        DecidirRetida(janela, cenario, deDiogo).IsSuccess.Should().BeTrue();

        janela.LinhaDe(deDiogo.Id)!.Situacao.Should().Be(SituacaoDaMigracao.AguardandoProtecaoDeDados);
        deDiogo.VersaoId.Should().Be(cenario.VersaoAntiga.Id);
    }

    [Fact(DisplayName = "Ninguém decide a própria concessão retida")]
    public void DecidirRetida_Propria_Recusa()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deDiogo = cenario.Conceder(Diogo);
        cenario.Conceder(Leandro);
        JanelaDeMigracao janela = cenario.Abrir();
        janela.EnviarLote(CenarioDeMigracao.Lote(false, (cenario.Concessoes[1], ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora));

        Codigos(janela.DecidirRetida(new PedidoDaRetida(deDiogo.Id, ResultadoDaMigracao.Migrar, false), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora)))
            .Should().Equal(MigracaoErrorCodes.DecisaoDaPropriaConcessao);
    }

    [Fact(DisplayName = "Decidir a retida antes do envio do lote é recusado")]
    public void DecidirRetida_SemLote_Recusa()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deDiogo = cenario.Conceder(Diogo);
        JanelaDeMigracao janela = cenario.Abrir();

        Codigos(janela.DecidirRetida(new PedidoDaRetida(deDiogo.Id, ResultadoDaMigracao.Migrar, false), CenarioDeMigracao.Autoria(Betina), cenario.Contexto(Agora)))
            .Should().Equal(MigracaoErrorCodes.LoteNaoEnviado);
    }

    [Fact(DisplayName = "O lote incompleto, repetido ou de outro perfil não é enviado e nada muda")]
    public void EnviarLote_IncompletoOuRepetido_NaoGravaNada()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao primeira = cenario.Conceder(Leandro);
        Concessao segunda = cenario.Conceder(Isabella);
        JanelaDeMigracao janela = cenario.Abrir();

        CodigosDoLote(janela, cenario, CenarioDeMigracao.Lote(false, (primeira, ResultadoDaMigracao.Migrar)))
            .Should().Equal(MigracaoErrorCodes.LoteIncompleto);
        CodigosDoLote(janela, cenario, CenarioDeMigracao.Lote(true, (primeira, ResultadoDaMigracao.Migrar), (primeira, ResultadoDaMigracao.Migrar), (segunda, ResultadoDaMigracao.Migrar)))
            .Should().Equal(MigracaoErrorCodes.LoteInvalido);
        CodigosDoLote(janela, cenario, new PedidoDoLote([new ItemDoLote(Guid.NewGuid(), ResultadoDaMigracao.Migrar)], true))
            .Should().Equal(MigracaoErrorCodes.LoteInvalido);
        NadaGravado(janela, cenario, primeira);
    }

    [Fact(DisplayName = "O lote com decisão inválida ou sem confirmar a exclusão não é enviado e nada muda")]
    public void EnviarLote_DecisaoInvalidaOuSemConfirmacao_NaoGravaNada()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao primeira = cenario.Conceder(Leandro);
        Concessao segunda = cenario.Conceder(Isabella);
        JanelaDeMigracao janela = cenario.Abrir();

        CodigosDoLote(janela, cenario, new PedidoDoLote([new ItemDoLote(primeira.Id, ResultadoDaMigracao.Reter), new ItemDoLote(segunda.Id, null)], true))
            .Should().Equal(MigracaoErrorCodes.LoteInvalido);
        CodigosDoLote(janela, cenario, CenarioDeMigracao.Lote(false, (primeira, ResultadoDaMigracao.Migrar), (segunda, ResultadoDaMigracao.Recusar)))
            .Should().Equal(MigracaoErrorCodes.ExclusaoNaoConfirmada);
        NadaGravado(janela, cenario, primeira);
    }

    [Fact(DisplayName = "Decidir a própria concessão no lote é recusado, e o segundo lote também")]
    public void EnviarLote_PropriaEJaEnviado_Recusa()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deDiogo = cenario.Conceder(Diogo);
        Concessao deLeandro = cenario.Conceder(Leandro);
        JanelaDeMigracao janela = cenario.Abrir();
        AutoriaDaVersao quem = CenarioDeMigracao.Autoria(Diogo);

        Codigos(janela.EnviarLote(CenarioDeMigracao.Lote(false, (deDiogo, ResultadoDaMigracao.Migrar), (deLeandro, ResultadoDaMigracao.Migrar)), quem, cenario.Contexto(Agora)))
            .Should().Equal(MigracaoErrorCodes.DecisaoDaPropriaConcessao);

        janela.EnviarLote(CenarioDeMigracao.Lote(false, (deLeandro, ResultadoDaMigracao.Migrar)), quem, cenario.Contexto(Agora));
        Codigos(janela.EnviarLote(CenarioDeMigracao.Lote(false, (deLeandro, ResultadoDaMigracao.Migrar)), quem, cenario.Contexto(Agora)))
            .Should().Equal(MigracaoErrorCodes.LoteJaEnviado);
    }

    [Fact(DisplayName = "Com acréscimo protegido, a migração aguarda a proteção de dados e a recusa revoga no envio")]
    public void EnviarLote_Protegido_AguardaProtecao()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        (Concessao paraMigrar, Concessao paraRecusar, JanelaDeMigracao janela) = MigrarERecusar(cenario);

        janela.LinhaDe(paraMigrar.Id)!.Situacao.Should().Be(SituacaoDaMigracao.AguardandoProtecaoDeDados);
        paraMigrar.VersaoId.Should().Be(cenario.VersaoAntiga.Id);
        paraRecusar.RevogadaEm.Should().NotBeNull();
        janela.Decisoes.First(d => d.ConcessaoId == paraMigrar.Id).ComposicaoAvaliada.Should().Contain(CatalogoMutavelDeTeste.Dados);
        janela.Decisoes.First(d => d.ConcessaoId == paraMigrar.Id).AcrescimoAvaliado.Should().Equal(CatalogoMutavelDeTeste.Dados);
        janela.Encerrada.Should().BeFalse();
    }

    [Fact(DisplayName = "A mesma pessoa não decide as duas etapas, a recusa exige a confirmação e o envio vazio é recusado")]
    public void DecidirEtapaDaProtecao_ConferePessoaEConfirmacao()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        (JanelaDeMigracao janela, PedidoDaEtapaDaProtecao pedido) = AguardandoAProtecao(cenario);

        Codigos(janela.DecidirEtapaDaProtecao(pedido, CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora)))
            .Should().Equal(MigracaoErrorCodes.MesmaPessoaNasDuasEtapas);
        Codigos(janela.DecidirEtapaDaProtecao(pedido with { ConfirmouExclusao = false }, CenarioDeMigracao.Autoria(Marlene), cenario.Contexto(Agora)))
            .Should().Equal(MigracaoErrorCodes.ExclusaoNaoConfirmada);
        Codigos(janela.DecidirEtapaDaProtecao(new([], false), CenarioDeMigracao.Autoria(Marlene), cenario.Contexto(Agora)))
            .Should().Equal(MigracaoErrorCodes.LoteInvalido);
    }

    [Fact(DisplayName = "A etapa de proteção de dados migra ou recusa a concessão que aguarda e conclui a janela")]
    public void DecidirEtapaDaProtecao_MigraERecusa()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        (JanelaDeMigracao janela, PedidoDaEtapaDaProtecao pedido) = AguardandoAProtecao(cenario);

        janela.DecidirEtapaDaProtecao(pedido, CenarioDeMigracao.Autoria(Marlene), cenario.Contexto(Agora)).IsSuccess.Should().BeTrue();

        cenario.Concessoes[0].VersaoId.Should().Be(cenario.VersaoNova!.Id);
        cenario.Concessoes[1].RevogadaPelaJanelaId.Should().Be(janela.Id);
        janela.Lotes.Should().HaveCount(2).And.Contain(l => l.Etapa == EtapaDaMigracao.ProtecaoDeDados && l.Quantidade == 2);
        janela.Decisoes.Where(d => d.Etapa == EtapaDaMigracao.ProtecaoDeDados).Should().OnlyContain(d => d.EnvioId != null && d.Decisor == Marlene);
        janela.Encerrada.Should().BeTrue();
    }

    [Fact(DisplayName = "Decidir de novo a concessão já decidida, ou fora da etapa, ou sem linha é recusado")]
    public void DecidirEtapaDaProtecao_ForaDaEtapa_Recusa()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao paraMigrar = cenario.Conceder(Leandro);
        Concessao retida = cenario.Conceder(Diogo);
        Concessao jaDecidida = cenario.Conceder(Isabella);
        JanelaDeMigracao janela = cenario.Abrir();
        janela.EnviarLote(
            CenarioDeMigracao.Lote(true, (paraMigrar, ResultadoDaMigracao.Migrar), (jaDecidida, ResultadoDaMigracao.Recusar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora));

        CodigosDaEtapa(janela, cenario, jaDecidida.Id).Should().Equal(MigracaoErrorCodes.ConcessaoJaDecidida);
        CodigosDaEtapa(janela, cenario, retida.Id).Should().Equal(MigracaoErrorCodes.ConcessaoForaDaEtapa);
        CodigosDaEtapa(janela, cenario, Guid.NewGuid()).Should().Equal(MigracaoErrorCodes.LoteInvalido);
        Codigos(janela.DecidirRetida(new PedidoDaRetida(paraMigrar.Id, ResultadoDaMigracao.Migrar, false), CenarioDeMigracao.Autoria(Betina), cenario.Contexto(Agora)))
            .Should().Equal(MigracaoErrorCodes.ConcessaoForaDaEtapa);
    }

    [Fact(DisplayName = "O lote com acréscimo protegido não dispensa a segunda etapa para nenhum beneficiário, nem para quem é Representante")]
    public void EnviarLote_Protegido_SemDispensaParaNinguem()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao deMarlene = cenario.Conceder(Marlene);
        JanelaDeMigracao janela = AbrirELote(cenario, deMarlene);

        janela.LinhaDe(deMarlene.Id)!.Situacao.Should().Be(SituacaoDaMigracao.AguardandoProtecaoDeDados);
        janela.Decisoes.Single().Resultado.Should().Be(ResultadoDaMigracao.Migrar);
        deMarlene.VersaoId.Should().Be(cenario.VersaoAntiga.Id);
    }

    [Fact(DisplayName = "Registrar o lote de uma pessoa grava se ela tinha concessão do perfil naquele instante")]
    public void EnviarLote_RegistraSeQuemDecideTemConcessao()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Conceder(Diogo);
        Concessao deLeandro = cenario.Conceder(Leandro);
        JanelaDeMigracao janela = cenario.Abrir();

        janela.EnviarLote(CenarioDeMigracao.Lote(false, (deLeandro, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora));

        janela.LoteDaGestao!.EnviadoPorTemConcessao.Should().BeTrue();
        janela.Decisoes.Single(d => d.Ator == AtorDaMigracao.Humano).DecisorTemConcessao.Should().BeTrue();
    }

    private static string[] Codigos(Result resultado) => [.. resultado.Errors.Select(e => e.Error.Code)];

    private static string[] CodigosDoLote(JanelaDeMigracao janela, CenarioDeMigracao cenario, PedidoDoLote lote) =>
        Codigos(janela.EnviarLote(lote, CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora)));

    private static string[] CodigosDaEtapa(JanelaDeMigracao janela, CenarioDeMigracao cenario, Guid concessaoId) =>
        Codigos(janela.DecidirEtapaDaProtecao(
            new([new ItemDoLote(concessaoId, ResultadoDaMigracao.Migrar)], false), CenarioDeMigracao.Autoria(Marlene), cenario.Contexto(Agora)));

    private static void NadaGravado(JanelaDeMigracao janela, CenarioDeMigracao cenario, Concessao concessao)
    {
        janela.Lotes.Should().BeEmpty();
        janela.Migracoes.Should().BeEmpty();
        janela.Decisoes.Should().BeEmpty();
        concessao.VersaoId.Should().Be(cenario.VersaoAntiga.Id);
    }

    // Leandro migra e Isabella é recusada, num lote de Diogo que confirma a exclusão.
    private static (Concessao ParaMigrar, Concessao ParaRecusar, JanelaDeMigracao Janela) MigrarERecusar(CenarioDeMigracao cenario)
    {
        Concessao paraMigrar = cenario.Conceder(Leandro);
        Concessao paraRecusar = cenario.Conceder(Isabella);
        JanelaDeMigracao janela = cenario.Abrir();

        janela.EnviarLote(
            CenarioDeMigracao.Lote(true, (paraMigrar, ResultadoDaMigracao.Migrar), (paraRecusar, ResultadoDaMigracao.Recusar)),
            CenarioDeMigracao.Autoria(Diogo),
            cenario.Contexto(Agora)).IsSuccess.Should().BeTrue();

        return (paraMigrar, paraRecusar, janela);
    }

    // Abre a janela e envia, como Diogo, o lote que migra a concessão.
    private static JanelaDeMigracao AbrirELote(CenarioDeMigracao cenario, Concessao paraMigrar)
    {
        JanelaDeMigracao janela = cenario.Abrir();
        janela.EnviarLote(CenarioDeMigracao.Lote(false, (paraMigrar, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora))
            .IsSuccess.Should().BeTrue();

        return janela;
    }

    private static Result DecidirRetida(JanelaDeMigracao janela, CenarioDeMigracao cenario, Concessao retida) =>
        janela.DecidirRetida(new PedidoDaRetida(retida.Id, ResultadoDaMigracao.Migrar, false), CenarioDeMigracao.Autoria(Betina), cenario.Contexto(Agora));

    // Leandro e Isabella aguardam a etapa de proteção de dados; o pedido migra Leandro e recusa Isabella.
    private static (JanelaDeMigracao Janela, PedidoDaEtapaDaProtecao Pedido) AguardandoAProtecao(CenarioDeMigracao cenario)
    {
        Concessao paraMigrar = cenario.Conceder(Leandro);
        Concessao paraRecusar = cenario.Conceder(Isabella);
        JanelaDeMigracao janela = cenario.Abrir();
        janela.EnviarLote(
            CenarioDeMigracao.Lote(false, (paraMigrar, ResultadoDaMigracao.Migrar), (paraRecusar, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora));

        return (janela, new([new ItemDoLote(paraMigrar.Id, ResultadoDaMigracao.Migrar), new ItemDoLote(paraRecusar.Id, ResultadoDaMigracao.Recusar)], true));
    }
}
