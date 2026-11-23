namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static FaseDaRetificacaoDeTeste;

/// <summary>
/// O que a retificação não muda nos prazos (UNI-REQ-0211, UNI-REQ-0106): a fase concluída pelo definitivo fica integral; a fase
/// em andamento mantém o início e não antecipa o término; as inscrições nunca antecipam o término, mas podem ser
/// prorrogadas; a fase sem definitivo que já concluiu pelo fim do período não é prorrogada depois que uma fase posterior
/// começou; o período de isenção não se reduz nem reabre; antes de começar, o prazo muda livremente.
/// </summary>
public sealed class ConferenciaDosPrazosDaRetificacaoTests
{
    private static readonly DateTimeOffset Passado = Instante.AddDays(-20);

    private static readonly TimeSpan UmMicrossegundo = TimeSpan.FromTicks(10);

    private static IReadOnlyList<FieldError> Conferir(FaseCronograma[] fasesDaBase, FaseCronograma[] fasesNovas, FasesIniciadas iniciadas) =>
        ConferenciaDosPrazosDaRetificacao.Conferir(fasesNovas, [], new AndamentoDasFases(fasesDaBase, [], Fatos(iniciadas)));

    private static IEnumerable<(string? Campo, string Codigo)> Recusas(IReadOnlyList<FieldError> recusas) =>
        recusas.Select(static recusa => (recusa.Field, recusa.Error.Code));

    private static FaseCronograma EmAndamento(string codigo = "PROVA", bool inscricao = false, bool isencao = false) =>
        Fase(1, codigo, (Passado, Instante.AddDays(10)), inscricao, isencao);

    private static FaseCronograma Encerrada(int ordem, string codigo, bool inscricao = false, bool isencao = false, params ProdutoDaFase[] produtos) =>
        Fase(ordem, codigo, (Passado, Instante.AddDays(-1)), inscricao, isencao, produtos: produtos);

    private static FaseCronograma Prorrogada(FaseCronograma fase) => ComJanela(fase, fase.Inicio, Instante.AddDays(3));

    [Fact(DisplayName = "A fase concluída pelo definitivo publicado, com o prazo mudado, é recusada no início e no término, e a idêntica passa")]
    public void FaseComDefinitivoPublicado_PrazoMudado_Recusa()
    {
        ProdutoDaFase definitivo = Definitivo("RESULTADO_PROVA");
        FaseCronograma prova = Fase(1, "PROVA", (Passado, Passado.AddDays(5)), produtos: [definitivo]);
        FaseCronograma resultado = Fase(2, "RESULTADO");
        FasesIniciadas iniciadas = Iniciadas(prova, resultado);
        FatosDaExecucao fatos = Fatos(iniciadas, Item(prova, definitivo));

        ConferenciaDosPrazosDaRetificacao.Conferir([prova, resultado], [], new AndamentoDasFases([prova, resultado], [], fatos)).Should().BeEmpty();
        Recusas(ConferenciaDosPrazosDaRetificacao.Conferir(
            [ComJanela(prova, Passado.AddDays(1), Passado.AddDays(9)), resultado], [], new AndamentoDasFases([prova, resultado], [], fatos))).Should().Equal(
            ("fases[0].inicio", RetificacaoErrorCodes.FaseConcluidaAlterada),
            ("fases[0].fim", RetificacaoErrorCodes.FaseConcluidaAlterada));
    }

    [Fact(DisplayName = "O início da fase em andamento não muda, nem adiado nem antecipado")]
    public void FaseEmAndamento_InicioMuda_Recusa()
    {
        FaseCronograma prova = EmAndamento();
        FasesIniciadas iniciadas = Iniciadas(prova);

        foreach (DateTimeOffset novo in new[] { Passado.AddDays(1), Passado.AddDays(-1) })
        {
            Recusas(Conferir([prova], [ComJanela(prova, novo, prova.Fim)], iniciadas)).Should().Equal(
                ("fases[0].inicio", RetificacaoErrorCodes.InicioDeFaseIniciadaAlterado));
        }
    }

    [Fact(DisplayName = "Antes de começar, a fase muda o início e o término livremente, inclusive antecipando")]
    public void FaseNaoIniciada_MudaLivremente()
    {
        FaseCronograma prova = Fase(1, "PROVA", (Instante.AddDays(5), Instante.AddDays(9)));

        Conferir([prova], [ComJanela(prova, Instante.AddDays(20), Instante.AddDays(21))], Iniciadas()).Should().BeEmpty();
        Conferir([prova], [ComJanela(prova, Instante.AddDays(2), Instante.AddDays(3))], Iniciadas()).Should().BeEmpty();
    }

    [Fact(DisplayName = "O término da fase em andamento não é antecipado, mas pode ser prorrogado")]
    public void FaseEmAndamento_Termino()
    {
        FaseCronograma prova = EmAndamento();
        FasesIniciadas iniciadas = Iniciadas(prova);

        Recusas(Conferir([prova], [ComJanela(prova, prova.Inicio, Instante.AddDays(9))], iniciadas)).Should().Equal(
            ("fases[0].fim", RetificacaoErrorCodes.TerminoDeFaseEmAndamentoAntecipado));
        Conferir([prova], [ComJanela(prova, prova.Inicio, Instante.AddDays(30))], iniciadas).Should().BeEmpty();
    }

    [Fact(DisplayName = "O início e o término da fase em andamento recusados juntos acumulam, cada um no seu campo")]
    public void FaseEmAndamento_InicioETermino_Acumulam()
    {
        FaseCronograma prova = EmAndamento();

        Recusas(Conferir([prova], [ComJanela(prova, Passado.AddDays(1), Instante.AddDays(9))], Iniciadas(prova))).Should().Equal(
            ("fases[0].inicio", RetificacaoErrorCodes.InicioDeFaseIniciadaAlterado),
            ("fases[0].fim", RetificacaoErrorCodes.TerminoDeFaseEmAndamentoAntecipado));
    }

    [Fact(DisplayName = "O término das inscrições nunca é antecipado, nem antes de abrir; prorrogar vale, mesmo com o prazo vencido, sem fase posterior iniciada")]
    public void Inscricao_TerminoNuncaAntecipado()
    {
        FaseCronograma aberta = Fase(1, "INSCRICAO", (Passado, Instante.AddDays(10)), coletaInscricao: true);
        FaseCronograma vencida = Encerrada(1, "INSCRICAO", inscricao: true);
        FaseCronograma futura = Fase(1, "INSCRICAO", (Instante.AddDays(5), Instante.AddDays(15)), coletaInscricao: true);
        FaseCronograma homologacao = Fase(2, "HOMOLOGACAO");

        Recusas(Conferir([aberta], [ComJanela(aberta, aberta.Inicio, Instante.AddDays(9))], Iniciadas(aberta))).Should().Equal(
            ("fases[0].fim", RetificacaoErrorCodes.TerminoDaInscricaoAntecipado));
        Recusas(Conferir([futura], [ComJanela(futura, futura.Inicio, Instante.AddDays(14))], Iniciadas())).Should().Equal(
            ("fases[0].fim", RetificacaoErrorCodes.TerminoDaInscricaoAntecipado));
        Recusas(Conferir([vencida], [ComJanela(vencida, vencida.Inicio, Instante.AddDays(-3))], Iniciadas(vencida))).Should().Equal(
            ("fases[0].fim", RetificacaoErrorCodes.TerminoDaInscricaoAntecipado));
        Recusas(Conferir([vencida], [ComJanela(vencida, Passado.AddDays(1), vencida.Fim)], Iniciadas(vencida))).Should().Equal(
            ("fases[0].inicio", RetificacaoErrorCodes.InicioDeFaseIniciadaAlterado));
        Conferir([vencida, homologacao], [Prorrogada(vencida), homologacao], Iniciadas(vencida)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase sem definitivo, iniciada e encerrada, com a fase imediatamente seguinte iniciada, não é prorrogada, e a mensagem cita as duas")]
    public void FaseConcluidaSemDefinitivo_ComASeguinteIniciada_NaoProrroga()
    {
        FaseCronograma vencida = Encerrada(1, "INSCRICAO", inscricao: true);
        FaseCronograma homologacao = Fase(2, "HOMOLOGACAO");

        IReadOnlyList<FieldError> recusas = Conferir([vencida, homologacao], [Prorrogada(vencida), homologacao], Iniciadas(vencida, homologacao));

        recusas.Should().ContainSingle().Which.Field.Should().Be("fases[0].fim");
        recusas.Single().Error.Code.Should().Be(RetificacaoErrorCodes.ProrrogacaoComFasePosteriorIniciada);
        recusas.Single().Error.Message.Should().Contain("'INSCRICAO'").And.Contain("'HOMOLOGACAO'");
    }

    [Fact(DisplayName = "A fase sem definitivo, iniciada e encerrada, com só uma fase posterior distante iniciada, também não é prorrogada")]
    public void FaseConcluidaSemDefinitivo_ComPosteriorDistanteIniciada_NaoProrroga()
    {
        FaseCronograma prova = Encerrada(1, "PROVA");
        FaseCronograma intermediaria = Fase(2, "ANALISE");
        FaseCronograma distante = Fase(3, "MATRICULA");

        IReadOnlyList<FieldError> recusas = Conferir([prova, intermediaria, distante], [Prorrogada(prova), intermediaria, distante], Iniciadas(prova, distante));

        recusas.Should().ContainSingle().Which.Error.Code.Should().Be(RetificacaoErrorCodes.ProrrogacaoComFasePosteriorIniciada);
        recusas.Single().Error.Message.Should().Contain("'MATRICULA'");
    }

    [Fact(DisplayName = "A fase nunca iniciada e encerrada é prorrogada, com ou sem fase posterior iniciada")]
    public void FaseNuncaIniciadaEEncerrada_Prorroga()
    {
        FaseCronograma prova = Encerrada(1, "PROVA");
        FaseCronograma seguinte = Fase(2, "RESULTADO");

        Conferir([prova, seguinte], [Prorrogada(prova), seguinte], Iniciadas()).Should().BeEmpty();
        Conferir([prova, seguinte], [Prorrogada(prova), seguinte], Iniciadas(seguinte)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A isenção contemporânea da inscrição não é fase posterior, e uma fase posterior verdadeira iniciada continua recusando")]
    public void FaseDeMesmoMomento_NaoEPosterior()
    {
        FaseCronograma isencao = Fase(1, "ISENCAO", (Passado, Instante.AddDays(2)), coletaIsencao: true);
        FaseCronograma inscricao = Encerrada(2, "INSCRICAO", inscricao: true);
        FaseCronograma seguinte = Fase(3, "HOMOLOGACAO");

        Conferir([isencao, inscricao, seguinte], [isencao, Prorrogada(inscricao), seguinte], Iniciadas(isencao, inscricao)).Should().BeEmpty();
        Recusas(Conferir([isencao, inscricao, seguinte], [isencao, Prorrogada(inscricao), seguinte], Iniciadas(isencao, inscricao, seguinte))).Should().Equal(
            ("fases[1].fim", RetificacaoErrorCodes.ProrrogacaoComFasePosteriorIniciada));
    }

    [Fact(DisplayName = "A fase marcada para acontecer no mesmo momento da prorrogada não é posterior a ela; sem a marca é")]
    public void FaseMarcadaNoMesmoMomento_NaoEPosterior()
    {
        FaseCronograma prova = Encerrada(1, "PROVA");
        FaseCronograma analise = Fase(2, "ANALISE", noMesmoMomentoDaAnterior: true);
        FaseCronograma analiseSemMarca = Fase(2, "ANALISE");

        Conferir([prova, analise], [Prorrogada(prova), analise], Iniciadas(prova, analise)).Should().BeEmpty();
        Recusas(Conferir([prova, analiseSemMarca], [Prorrogada(prova), analiseSemMarca], Iniciadas(prova, analiseSemMarca))).Should().Equal(
            ("fases[0].fim", RetificacaoErrorCodes.ProrrogacaoComFasePosteriorIniciada));
    }

    [Fact(DisplayName = "No instante exato do fim publicado o período ainda corre: prorrogar é aceito, mesmo com fase posterior iniciada por sobreposição")]
    public void NoInstanteDoFim_ProrrogacaoAceita()
    {
        FaseCronograma prova = Fase(1, "PROVA", (Passado, Instante));
        FaseCronograma seguinte = Fase(2, "RESULTADO");

        Conferir([prova, seguinte], [ComJanela(prova, prova.Inicio, Instante.AddDays(3)), seguinte], Iniciadas(prova, seguinte)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Com o instante estritamente antes do fim publicado, prorrogar é aceito, mesmo com fase posterior iniciada")]
    public void AntesDoFim_ProrrogacaoAceitaComPosteriorIniciada()
    {
        FaseCronograma prova = Fase(1, "PROVA", (Passado, Instante.AddDays(1)));
        FaseCronograma seguinte = Fase(2, "RESULTADO");

        Conferir([prova, seguinte], [ComJanela(prova, prova.Inicio, Instante.AddDays(3)), seguinte], Iniciadas(prova, seguinte)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Um microssegundo depois do fim publicado a fase sem definitivo está concluída, e prorrogar com fase posterior iniciada é recusado")]
    public void DepoisDoFim_ProrrogacaoRecusadaComPosteriorIniciada()
    {
        FaseCronograma prova = Fase(1, "PROVA", (Passado, Instante - UmMicrossegundo));
        FaseCronograma seguinte = Fase(2, "RESULTADO");

        Recusas(Conferir([prova, seguinte], [Prorrogada(prova), seguinte], Iniciadas(prova, seguinte))).Should().Equal(
            ("fases[0].fim", RetificacaoErrorCodes.ProrrogacaoComFasePosteriorIniciada));
    }

    [Fact(DisplayName = "A fase com definitivo previsto e ainda pendente, de período terminado e com fase posterior iniciada, é prorrogada: não está concluída")]
    public void FaseComDefinitivoPendente_Prorroga()
    {
        FaseCronograma prova = Encerrada(1, "PROVA", produtos: [Preliminar("RESULTADO_PROVA"), Definitivo("RESULTADO_PROVA")]);
        FaseCronograma seguinte = Fase(2, "RESULTADO");

        Conferir([prova, seguinte], [Prorrogada(prova), seguinte], Iniciadas(prova, seguinte)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase sem definitivo e concluída não antecipa o término: a recusa é de fase concluída alterada")]
    public void FaseConcluidaSemDefinitivo_TerminoAntecipado_Recusa()
    {
        FaseCronograma prova = Encerrada(1, "PROVA");

        Recusas(Conferir([prova], [ComJanela(prova, prova.Inicio, Instante.AddDays(-3))], Iniciadas(prova))).Should().Equal(
            ("fases[0].fim", RetificacaoErrorCodes.FaseConcluidaAlterada));
    }

    [Fact(DisplayName = "A janela de isenção não se reduz, mesmo antes de abrir, e prorrogar antes do término é aceito")]
    public void JanelaDeIsencao_NaoSeReduz()
    {
        FaseCronograma futura = Fase(1, "ISENCAO", (Instante.AddDays(2), Instante.AddDays(9)), coletaIsencao: true);

        Recusas(Conferir([futura], [ComJanela(futura, futura.Inicio, Instante.AddDays(8))], Iniciadas())).Should().Equal(
            ("fases[0].fim", RetificacaoErrorCodes.JanelaDeIsencaoReduzida));
        Conferir([futura], [ComJanela(futura, futura.Inicio, Instante.AddDays(12))], Iniciadas()).Should().BeEmpty();
    }

    [Fact(DisplayName = "O período de isenção iniciado e encerrada não reabre: mudar o término é recusado, com ou sem fase posterior iniciada")]
    public void JanelaDeIsencaoEncerrada_NaoReabre()
    {
        FaseCronograma encerrada = Encerrada(1, "ISENCAO", isencao: true);
        FaseCronograma seguinte = Fase(2, "INSCRICAO");

        Recusas(Conferir([encerrada, seguinte], [Prorrogada(encerrada), seguinte], Iniciadas(encerrada))).Should().Equal(
            ("fases[0].fim", RetificacaoErrorCodes.JanelaDeIsencaoEncerradaReaberta));
        Recusas(Conferir([encerrada, seguinte], [Prorrogada(encerrada), seguinte], Iniciadas(encerrada, seguinte))).Should().Equal(
            ("fases[0].fim", RetificacaoErrorCodes.JanelaDeIsencaoEncerradaReaberta));
    }

    [Fact(DisplayName = "A fronteira da isenção: com o fim um microssegundo antes do instante está encerrada, e no instante exato e depois ainda corre")]
    public void PeriodoDeIsencao_Fronteira()
    {
        FaseCronograma antes = Fase(1, "ISENCAO", (Passado, Instante - UmMicrossegundo), coletaIsencao: true);
        FaseCronograma noFim = Fase(1, "ISENCAO", (Passado, Instante), coletaIsencao: true);
        FaseCronograma depois = Fase(1, "ISENCAO", (Passado, Instante + UmMicrossegundo), coletaIsencao: true);

        Recusas(Conferir([antes], [Prorrogada(antes)], Iniciadas(antes))).Should().Equal(
            ("fases[0].fim", RetificacaoErrorCodes.JanelaDeIsencaoEncerradaReaberta));
        Conferir([noFim], [Prorrogada(noFim)], Iniciadas(noFim)).Should().BeEmpty();
        Conferir([depois], [Prorrogada(depois)], Iniciadas(depois)).Should().BeEmpty();
        Conferir([noFim], [noFim], Iniciadas(noFim)).Should().BeEmpty();
        Recusas(Conferir([noFim], [ComJanela(noFim, noFim.Inicio, Instante.AddDays(-1))], Iniciadas(noFim))).Should().Equal(
            ("fases[0].fim", RetificacaoErrorCodes.JanelaDeIsencaoReduzida));
    }

    [Fact(DisplayName = "O período de isenção nunca iniciado e encerrado se prorroga, com ou sem fase posterior iniciada")]
    public void PeriodoDeIsencaoNuncaIniciada_Prorroga()
    {
        FaseCronograma isencao = Encerrada(1, "ISENCAO", isencao: true);
        FaseCronograma seguinte = Fase(2, "INSCRICAO");

        Conferir([isencao, seguinte], [Prorrogada(isencao), seguinte], Iniciadas()).Should().BeEmpty();
        Conferir([isencao, seguinte], [Prorrogada(isencao), seguinte], Iniciadas(seguinte)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase recusada é apontada pela posição na ordem do documento, e não pela posição na lista como veio")]
    public void FaseRecusada_ApontaAPosicaoNoDocumento()
    {
        ProdutoDaFase definitivo = Definitivo("RESULTADO_PROVA");
        FaseCronograma inscricao = Fase(1, "INSCRICAO", (Passado, Passado.AddDays(5)));
        FaseCronograma prova = Fase(2, "PROVA", (Passado, Passado.AddDays(5)), produtos: [definitivo]);
        FaseCronograma resultado = Fase(3, "RESULTADO");
        FatosDaExecucao fatos = Fatos(Iniciadas(inscricao, prova, resultado), Item(prova, definitivo));

        Recusas(ConferenciaDosPrazosDaRetificacao.Conferir(
            [resultado, ComJanela(prova, Passado, Passado.AddDays(9)), inscricao], [], new AndamentoDasFases([inscricao, prova, resultado], [], fatos))).Should().Equal(
            ("fases[1].fim", RetificacaoErrorCodes.FaseConcluidaAlterada));
    }

    [Fact(DisplayName = "A fase retirada do cronograma novo não é recusada aqui")]
    public void FaseRetirada_NaoERecusadaAqui()
    {
        FaseCronograma prova = EmAndamento();

        Conferir([prova], [], Iniciadas(prova)).Should().BeEmpty();
    }

    [Fact(DisplayName = "O início da etapa que já começou não muda; o da etapa que não começou muda livremente")]
    public void Etapa_InicioDaEtapaIniciada_NaoMuda()
    {
        EtapaProcesso iniciada = EtapaComJanela("Prova objetiva", Passado, 2);
        EtapaProcesso porComecar = EtapaComJanela("Prova discursiva", Instante.AddDays(5), 1);
        FasesIniciadas iniciadas = new([], etapas: [new EtapaIniciada(iniciada.Id, iniciada.Nome, NaVersaoVigente: true)]);
        AndamentoDasFases andamento = new([], [iniciada, porComecar], Fatos(iniciadas));

        ConferenciaDosPrazosDaRetificacao.Conferir([], [CopiaComInicio(iniciada, Passado.AddDays(2)), CopiaComInicio(porComecar, Instante.AddDays(9))], andamento)
            .Select(static r => (r.Field, r.Error.Code)).Should().Equal(
                ("etapas[1].inicio", RetificacaoErrorCodes.InicioDeEtapaIniciadaAlterado));
        ConferenciaDosPrazosDaRetificacao.Conferir([], [CopiaComInicio(iniciada, Passado), porComecar], andamento).Should().BeEmpty();
    }

    private static EtapaProcesso CopiaComInicio(EtapaProcesso etapa, DateTimeOffset inicio)
    {
        EtapaProcesso copia = EtapaProcesso.Reidratar(
            etapa.Id,
            new IdentificacaoDaEtapa(etapa.Nome, etapa.Carater, etapa.TipoEtapa, etapa.Ordem, etapa.FaseCodigo),
            new DeclaracaoDaAvaliacaoDaEtapa(etapa.Peso, etapa.NotaMinima, etapa.NotaMaxima,
                etapa.PassoDoLancamento, etapa.FormaDeAvaliacao, etapa.RegraConsolidacao, etapa.EscalaDeAtendimento), []);
        copia.DefinirJanelaEParecer(inicio, inicio.AddDays(3), false).IsSuccess.Should().BeTrue();
        return copia;
    }
}
