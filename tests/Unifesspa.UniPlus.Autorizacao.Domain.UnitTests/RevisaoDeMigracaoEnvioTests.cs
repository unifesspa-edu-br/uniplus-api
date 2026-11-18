namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RevisaoDeMigracaoEnvioTests
{
    private static readonly Identidade Diogo = PerfisDeTeste.Autor;
    private static readonly Identidade Betina = PerfisDeTeste.Gestor;
    private static readonly Identidade Marlene = PerfisDeTeste.ProtecaoDeDados;
    private static readonly Identidade Leandro = CenarioDeMigracao.Leandro;
    private static readonly Identidade Isabella = CenarioDeMigracao.Isabella;
    private static readonly DateTimeOffset Agora = CenarioDeMigracao.Abertura.AddHours(1);

    [Fact(DisplayName = "A revisão abre com o prazo de 72 horas corridas e a versão nova como alvo")]
    public void Abrir_GravaPrazoEAlvo()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Conceder(Leandro);

        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();

        revisao.TerminaEm.Should().Be(CenarioDeMigracao.Abertura.AddHours(72));
        revisao.VersaoAlvoId.Should().Be(cenario.VersaoNova!.Id);
        revisao.NumeroDaVersaoNova.Should().Be(2);
        revisao.Encerrada.Should().BeFalse();
    }

    [Fact(DisplayName = "Elegível é a concessão ativa do perfil fora da versão-alvo")]
    public void Elegivel_SoAtivaForaDoAlvo()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao ativa = cenario.Conceder(Leandro);
        Concessao revogada = cenario.Conceder(Isabella);
        Concessao vencida = cenario.Conceder(Diogo, validaAte: CenarioDeMigracao.Abertura.AddHours(2));
        revogada.Revogar(new AtoDoAgente(Diogo, CenarioDeMigracao.Abertura.AddMinutes(-1)), "Teste de revogação.").IsSuccess.Should().BeTrue();
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        DateTimeOffset depoisDoVencimento = CenarioDeMigracao.Abertura.AddHours(3);

        revisao.Elegivel(ativa, depoisDoVencimento).Should().BeTrue();
        revisao.Elegivel(revogada, depoisDoVencimento).Should().BeFalse();
        revisao.Elegivel(vencida, depoisDoVencimento).Should().BeFalse();
        revisao.EstaAberta(depoisDoVencimento, revisao.Pendencias(cenario.Concessoes, depoisDoVencimento)).Should().BeTrue();
        revisao.EstaAberta(revisao.TerminaEm, 1).Should().BeFalse();
        revisao.EstaAberta(Agora, 0).Should().BeFalse();
    }

    [Fact(DisplayName = "O envio migra e recusa cada concessão e a recusa revoga pelo sistema")]
    public void EnviarDecisoesDaGestao_MigraERecusa()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        (Concessao paraMigrar, Concessao paraRecusar, RevisaoDasConcessoesDoPerfil revisao) = MigrarERecusar(cenario);

        paraMigrar.VersaoId.Should().Be(cenario.VersaoNova!.Id);
        paraRecusar.RevogadaEm.Should().Be(Agora);
        paraRecusar.RevogadaPelaRevisaoId.Should().Be(revisao.Id);
        paraRecusar.MotivoRevogacao.Should().Be("recusado na migração");
        revisao.LinhaDe(paraMigrar.Id)!.Situacao.Should().Be(SituacaoDaMigracao.Migrada);
        revisao.LinhaDe(paraRecusar.Id)!.Situacao.Should().Be(SituacaoDaMigracao.Recusada);
    }

    [Fact(DisplayName = "O envio que decide todas as concessões registra quem decidiu e conclui a revisão")]
    public void EnviarDecisoesDaGestao_TodasDecididas_ConcluiARevisao()
    {
        (_, _, RevisaoDasConcessoesDoPerfil revisao) = MigrarERecusar(new CenarioDeMigracao(protegido: false));

        revisao.Decisoes.Should().OnlyContain(d => d.Decisor == Diogo && d.Ator == AtorDaMigracao.Humano && d.EnvioDeOrigemId == revisao.EnvioDaGestao!.Id);
        revisao.Encerrada.Should().BeTrue();
        revisao.Forma.Should().Be(FormaDoEncerramento.Concluida);
    }

    [Fact(DisplayName = "As concessões do próprio gestor ficam retidas, em qualquer escopo, e o gestor não aparece como decisor delas")]
    public void EnviarDecisoesDaGestao_ConcessoesDoGestorFicamRetidas()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao daCeps = cenario.Conceder(Diogo, Guid.NewGuid());
        Concessao daCtic = cenario.Conceder(Diogo, Guid.NewGuid());
        RevisaoDasConcessoesDoPerfil revisao = AbrirEEnvio(cenario, cenario.Conceder(Leandro));

        revisao.LinhaDe(daCeps.Id)!.Situacao.Should().Be(SituacaoDaMigracao.Retida);
        revisao.LinhaDe(daCtic.Id)!.Situacao.Should().Be(SituacaoDaMigracao.Retida);
        revisao.Decisoes.Where(d => d.ConcessaoId == daCeps.Id).Should().ContainSingle()
            .Which.Should().Match<DecisaoDaMigracao>(d => d.Ator == AtorDaMigracao.Sistema && d.Resultado == ResultadoDaMigracao.Reter && d.Decisor == null);
        revisao.Encerrada.Should().BeFalse();
    }

    [Fact(DisplayName = "A concessão retida é decidida por outra pessoa, que não altera o que o envio decidiu")]
    public void DecidirRetida_OutraPessoaMigra()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deDiogo = cenario.Conceder(Diogo);
        Concessao deLeandro = cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = AbrirEEnvio(cenario, deLeandro);

        DecidirRetida(revisao, cenario, deDiogo).IsSuccess.Should().BeTrue();

        deDiogo.VersaoId.Should().Be(cenario.VersaoNova!.Id);
        revisao.LinhaDe(deLeandro.Id)!.Situacao.Should().Be(SituacaoDaMigracao.Migrada);
        revisao.Decisoes[^1].Decisor.Should().Be(Betina);
        revisao.Decisoes[^1].EnvioId.Should().BeNull();
        revisao.Encerrada.Should().BeTrue();
    }

    [Fact(DisplayName = "A retida com acréscimo protegido aguarda a etapa de proteção de dados, sem dispensa para nenhum beneficiário")]
    public void DecidirRetida_Protegida_AguardaAProtecao()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao deDiogo = cenario.Conceder(Diogo);
        RevisaoDasConcessoesDoPerfil revisao = AbrirEEnvio(cenario, cenario.Conceder(Leandro));

        DecidirRetida(revisao, cenario, deDiogo).IsSuccess.Should().BeTrue();

        revisao.LinhaDe(deDiogo.Id)!.Situacao.Should().Be(SituacaoDaMigracao.AguardandoProtecaoDeDados);
        deDiogo.VersaoId.Should().Be(cenario.VersaoAntiga.Id);
    }

    [Fact(DisplayName = "Ninguém decide a própria concessão retida")]
    public void DecidirRetida_Propria_Recusa()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deDiogo = cenario.Conceder(Diogo);
        cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.EnviarDecisoesDaGestao(CenarioDeMigracao.Envio(false, (cenario.Concessoes[1], ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora));

        Codigos(revisao.DecidirRetida(new PedidoDaRetida(deDiogo.Id, ResultadoDaMigracao.Migrar, false), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora)))
            .Should().Equal(RevisaoDasConcessoesErrorCodes.DecisaoDaPropriaConcessao);
    }

    [Fact(DisplayName = "Decidir a retida antes do envio das decisões é recusado")]
    public void DecidirRetida_SemEnvio_Recusa()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deDiogo = cenario.Conceder(Diogo);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();

        Codigos(revisao.DecidirRetida(new PedidoDaRetida(deDiogo.Id, ResultadoDaMigracao.Migrar, false), CenarioDeMigracao.Autoria(Betina), cenario.Contexto(Agora)))
            .Should().Equal(RevisaoDasConcessoesErrorCodes.DecisoesNaoEnviadas);
    }

    [Fact(DisplayName = "O envio incompleto, repetido ou de outro perfil não é enviado e nada muda")]
    public void EnviarDecisoesDaGestao_IncompletoOuRepetido_NaoGravaNada()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao primeira = cenario.Conceder(Leandro);
        Concessao segunda = cenario.Conceder(Isabella);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();

        CodigosDoEnvio(revisao, cenario, CenarioDeMigracao.Envio(false, (primeira, ResultadoDaMigracao.Migrar)))
            .Should().Equal(RevisaoDasConcessoesErrorCodes.DecisoesIncompletas);
        CodigosDoEnvio(revisao, cenario, CenarioDeMigracao.Envio(true, (primeira, ResultadoDaMigracao.Migrar), (primeira, ResultadoDaMigracao.Migrar), (segunda, ResultadoDaMigracao.Migrar)))
            .Should().Equal(RevisaoDasConcessoesErrorCodes.DecisoesInvalidas);
        CodigosDoEnvio(revisao, cenario, new PedidoDoEnvio([new ItemDoEnvio(Guid.NewGuid(), ResultadoDaMigracao.Migrar)], true))
            .Should().Equal(RevisaoDasConcessoesErrorCodes.DecisoesInvalidas);
        NadaGravado(revisao, cenario, primeira);
    }

    [Fact(DisplayName = "O envio com decisão inválida ou sem confirmar a exclusão não é enviado e nada muda")]
    public void EnviarDecisoesDaGestao_DecisaoInvalidaOuSemConfirmacao_NaoGravaNada()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao primeira = cenario.Conceder(Leandro);
        Concessao segunda = cenario.Conceder(Isabella);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();

        CodigosDoEnvio(revisao, cenario, new PedidoDoEnvio([new ItemDoEnvio(primeira.Id, ResultadoDaMigracao.Reter), new ItemDoEnvio(segunda.Id, null)], true))
            .Should().Equal(RevisaoDasConcessoesErrorCodes.DecisoesInvalidas);
        CodigosDoEnvio(revisao, cenario, CenarioDeMigracao.Envio(false, (primeira, ResultadoDaMigracao.Migrar), (segunda, ResultadoDaMigracao.Recusar)))
            .Should().Equal(RevisaoDasConcessoesErrorCodes.ExclusaoNaoConfirmada);
        NadaGravado(revisao, cenario, primeira);
    }

    [Fact(DisplayName = "Decidir a própria concessão no envio é recusado, e o segundo envio também")]
    public void EnviarDecisoesDaGestao_PropriaEJaEnviado_Recusa()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deDiogo = cenario.Conceder(Diogo);
        Concessao deLeandro = cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        AutoriaDaVersao quem = CenarioDeMigracao.Autoria(Diogo);

        Codigos(revisao.EnviarDecisoesDaGestao(CenarioDeMigracao.Envio(false, (deDiogo, ResultadoDaMigracao.Migrar), (deLeandro, ResultadoDaMigracao.Migrar)), quem, cenario.Contexto(Agora)))
            .Should().Equal(RevisaoDasConcessoesErrorCodes.DecisaoDaPropriaConcessao);

        revisao.EnviarDecisoesDaGestao(CenarioDeMigracao.Envio(false, (deLeandro, ResultadoDaMigracao.Migrar)), quem, cenario.Contexto(Agora));
        Codigos(revisao.EnviarDecisoesDaGestao(CenarioDeMigracao.Envio(false, (deLeandro, ResultadoDaMigracao.Migrar)), quem, cenario.Contexto(Agora)))
            .Should().Equal(RevisaoDasConcessoesErrorCodes.DecisoesJaEnviadas);
    }

    [Fact(DisplayName = "Com acréscimo protegido, a migração aguarda a proteção de dados e a recusa revoga no envio")]
    public void EnviarDecisoesDaGestao_Protegido_AguardaProtecao()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        (Concessao paraMigrar, Concessao paraRecusar, RevisaoDasConcessoesDoPerfil revisao) = MigrarERecusar(cenario);

        revisao.LinhaDe(paraMigrar.Id)!.Situacao.Should().Be(SituacaoDaMigracao.AguardandoProtecaoDeDados);
        paraMigrar.VersaoId.Should().Be(cenario.VersaoAntiga.Id);
        paraRecusar.RevogadaEm.Should().NotBeNull();
        revisao.Decisoes.First(d => d.ConcessaoId == paraMigrar.Id).ComposicaoAvaliada.Should().Contain(CatalogoMutavelDeTeste.Dados);
        revisao.Decisoes.First(d => d.ConcessaoId == paraMigrar.Id).AcrescimoAvaliado.Should().Equal(CatalogoMutavelDeTeste.Dados);
        revisao.Encerrada.Should().BeFalse();
    }

    [Fact(DisplayName = "A mesma pessoa não decide as duas etapas, a recusa exige a confirmação e o envio vazio é recusado")]
    public void DecidirEtapaDaProtecao_ConferePessoaEConfirmacao()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        (RevisaoDasConcessoesDoPerfil revisao, PedidoDaEtapaDaProtecao pedido) = AguardandoAProtecao(cenario);

        Codigos(revisao.DecidirEtapaDaProtecao(pedido, CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora)))
            .Should().Equal(RevisaoDasConcessoesErrorCodes.MesmaPessoaNasDuasEtapas);
        Codigos(revisao.DecidirEtapaDaProtecao(pedido with { ConfirmouExclusao = false }, CenarioDeMigracao.Autoria(Marlene), cenario.Contexto(Agora)))
            .Should().Equal(RevisaoDasConcessoesErrorCodes.ExclusaoNaoConfirmada);
        Codigos(revisao.DecidirEtapaDaProtecao(new([], false), CenarioDeMigracao.Autoria(Marlene), cenario.Contexto(Agora)))
            .Should().Equal(RevisaoDasConcessoesErrorCodes.DecisoesInvalidas);
    }

    [Fact(DisplayName = "A etapa de proteção de dados migra ou recusa a concessão que aguarda e conclui a revisão")]
    public void DecidirEtapaDaProtecao_MigraERecusa()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        (RevisaoDasConcessoesDoPerfil revisao, PedidoDaEtapaDaProtecao pedido) = AguardandoAProtecao(cenario);

        revisao.DecidirEtapaDaProtecao(pedido, CenarioDeMigracao.Autoria(Marlene), cenario.Contexto(Agora)).IsSuccess.Should().BeTrue();

        cenario.Concessoes[0].VersaoId.Should().Be(cenario.VersaoNova!.Id);
        cenario.Concessoes[1].RevogadaPelaRevisaoId.Should().Be(revisao.Id);
        revisao.Envios.Should().HaveCount(2).And.Contain(l => l.Etapa == EtapaDaMigracao.ProtecaoDeDados && l.Quantidade == 2);
        revisao.Decisoes.Where(d => d.Etapa == EtapaDaMigracao.ProtecaoDeDados).Should().OnlyContain(d => d.EnvioId != null && d.Decisor == Marlene);
        revisao.Encerrada.Should().BeTrue();
    }

    [Fact(DisplayName = "Decidir de novo a concessão já decidida, ou fora da etapa, ou sem linha é recusado")]
    public void DecidirEtapaDaProtecao_ForaDaEtapa_Recusa()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao paraMigrar = cenario.Conceder(Leandro);
        Concessao retida = cenario.Conceder(Diogo);
        Concessao jaDecidida = cenario.Conceder(Isabella);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.EnviarDecisoesDaGestao(
            CenarioDeMigracao.Envio(true, (paraMigrar, ResultadoDaMigracao.Migrar), (jaDecidida, ResultadoDaMigracao.Recusar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora));

        CodigosDaEtapa(revisao, cenario, jaDecidida.Id).Should().Equal(RevisaoDasConcessoesErrorCodes.ConcessaoJaDecidida);
        CodigosDaEtapa(revisao, cenario, retida.Id).Should().Equal(RevisaoDasConcessoesErrorCodes.ConcessaoForaDaEtapa);
        CodigosDaEtapa(revisao, cenario, Guid.NewGuid()).Should().Equal(RevisaoDasConcessoesErrorCodes.DecisoesInvalidas);
        Codigos(revisao.DecidirRetida(new PedidoDaRetida(paraMigrar.Id, ResultadoDaMigracao.Migrar, false), CenarioDeMigracao.Autoria(Betina), cenario.Contexto(Agora)))
            .Should().Equal(RevisaoDasConcessoesErrorCodes.ConcessaoForaDaEtapa);
    }

    [Fact(DisplayName = "O envio com acréscimo protegido não dispensa a segunda etapa para nenhum beneficiário, nem para quem é Representante")]
    public void EnviarDecisoesDaGestao_Protegido_SemDispensaParaNinguem()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao deMarlene = cenario.Conceder(Marlene);
        RevisaoDasConcessoesDoPerfil revisao = AbrirEEnvio(cenario, deMarlene);

        revisao.LinhaDe(deMarlene.Id)!.Situacao.Should().Be(SituacaoDaMigracao.AguardandoProtecaoDeDados);
        revisao.Decisoes.Single().Resultado.Should().Be(ResultadoDaMigracao.Migrar);
        deMarlene.VersaoId.Should().Be(cenario.VersaoAntiga.Id);
    }

    [Fact(DisplayName = "Registrar o envio de uma pessoa grava se ela tinha concessão do perfil naquele instante")]
    public void EnviarDecisoesDaGestao_RegistraSeQuemDecideTemConcessao()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Conceder(Diogo);
        Concessao deLeandro = cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();

        revisao.EnviarDecisoesDaGestao(CenarioDeMigracao.Envio(false, (deLeandro, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora));

        revisao.EnvioDaGestao!.EnviadoPorTemConcessao.Should().BeTrue();
        revisao.Decisoes.Single(d => d.Ator == AtorDaMigracao.Humano).DecisorTemConcessao.Should().BeTrue();
    }

    private static string[] Codigos(Result resultado) => [.. resultado.Errors.Select(e => e.Error.Code)];

    private static string[] CodigosDoEnvio(RevisaoDasConcessoesDoPerfil revisao, CenarioDeMigracao cenario, PedidoDoEnvio envio) =>
        Codigos(revisao.EnviarDecisoesDaGestao(envio, CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora)));

    private static string[] CodigosDaEtapa(RevisaoDasConcessoesDoPerfil revisao, CenarioDeMigracao cenario, Guid concessaoId) =>
        Codigos(revisao.DecidirEtapaDaProtecao(
            new([new ItemDoEnvio(concessaoId, ResultadoDaMigracao.Migrar)], false), CenarioDeMigracao.Autoria(Marlene), cenario.Contexto(Agora)));

    private static void NadaGravado(RevisaoDasConcessoesDoPerfil revisao, CenarioDeMigracao cenario, Concessao concessao)
    {
        revisao.Envios.Should().BeEmpty();
        revisao.Migracoes.Should().BeEmpty();
        revisao.Decisoes.Should().BeEmpty();
        concessao.VersaoId.Should().Be(cenario.VersaoAntiga.Id);
    }

    // Leandro migra e Isabella é recusada, num envio de Diogo que confirma a exclusão.
    private static (Concessao ParaMigrar, Concessao ParaRecusar, RevisaoDasConcessoesDoPerfil Revisao) MigrarERecusar(CenarioDeMigracao cenario)
    {
        Concessao paraMigrar = cenario.Conceder(Leandro);
        Concessao paraRecusar = cenario.Conceder(Isabella);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();

        revisao.EnviarDecisoesDaGestao(
            CenarioDeMigracao.Envio(true, (paraMigrar, ResultadoDaMigracao.Migrar), (paraRecusar, ResultadoDaMigracao.Recusar)),
            CenarioDeMigracao.Autoria(Diogo),
            cenario.Contexto(Agora)).IsSuccess.Should().BeTrue();

        return (paraMigrar, paraRecusar, revisao);
    }

    // Abre a revisão e envia, como Diogo, o envio que migra a concessão.
    private static RevisaoDasConcessoesDoPerfil AbrirEEnvio(CenarioDeMigracao cenario, Concessao paraMigrar)
    {
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.EnviarDecisoesDaGestao(CenarioDeMigracao.Envio(false, (paraMigrar, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora))
            .IsSuccess.Should().BeTrue();

        return revisao;
    }

    private static Result DecidirRetida(RevisaoDasConcessoesDoPerfil revisao, CenarioDeMigracao cenario, Concessao retida) =>
        revisao.DecidirRetida(new PedidoDaRetida(retida.Id, ResultadoDaMigracao.Migrar, false), CenarioDeMigracao.Autoria(Betina), cenario.Contexto(Agora));

    // Leandro e Isabella aguardam a etapa de proteção de dados; o pedido migra Leandro e recusa Isabella.
    private static (RevisaoDasConcessoesDoPerfil Revisao, PedidoDaEtapaDaProtecao Pedido) AguardandoAProtecao(CenarioDeMigracao cenario)
    {
        Concessao paraMigrar = cenario.Conceder(Leandro);
        Concessao paraRecusar = cenario.Conceder(Isabella);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.EnviarDecisoesDaGestao(
            CenarioDeMigracao.Envio(false, (paraMigrar, ResultadoDaMigracao.Migrar), (paraRecusar, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Agora));

        return (revisao, new([new ItemDoEnvio(paraMigrar.Id, ResultadoDaMigracao.Migrar), new ItemDoEnvio(paraRecusar.Id, ResultadoDaMigracao.Recusar)], true));
    }
}
