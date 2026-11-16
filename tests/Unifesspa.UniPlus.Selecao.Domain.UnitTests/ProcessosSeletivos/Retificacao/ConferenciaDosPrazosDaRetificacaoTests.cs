namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static FaseDaRetificacaoDeTeste;

/// <summary>
/// O que a retificação não muda nos prazos (UNI-REQ-0211, UNI-REQ-0106): a fase concluída fica integral; a fase
/// em andamento mantém o início e não antecipa o término; as inscrições nunca antecipam o término, mas podem ser
/// prorrogadas; a janela de isenção não se reduz nem reabre; antes de começar, o prazo muda livremente.
/// </summary>
public sealed class ConferenciaDosPrazosDaRetificacaoTests
{
    private static readonly DateTimeOffset Passado = Instante.AddDays(-20);

    private static IReadOnlyList<FieldError> Conferir(FaseCronograma[] fasesDaBase, FaseCronograma[] fasesNovas, FasesIniciadas iniciadas) =>
        ConferenciaDosPrazosDaRetificacao.Conferir(fasesNovas, [], new AndamentoDasFases(fasesDaBase, [], Fatos(iniciadas)));

    private static IEnumerable<(string? Campo, string Codigo)> Recusas(IReadOnlyList<FieldError> recusas) =>
        recusas.Select(static recusa => (recusa.Field, recusa.Error.Code));

    private static FaseCronograma EmAndamento(string codigo = "PROVA", bool inscricao = false, bool isencao = false) =>
        Fase(1, codigo, (Passado, Instante.AddDays(10)), inscricao, isencao);

    [Fact(DisplayName = "A fase concluída com o prazo mudado é recusada, e a idêntica passa")]
    public void FaseConcluida_PrazoMudado_Recusa()
    {
        FaseCronograma prova = Fase(1, "PROVA", (Passado, Passado.AddDays(5)));
        FaseCronograma resultado = Fase(2, "RESULTADO");
        FasesIniciadas iniciadas = Iniciadas(prova, resultado);

        Conferir([prova, resultado], [prova, resultado], iniciadas).Should().BeEmpty();
        Recusas(Conferir([prova, resultado], [ComJanela(prova, Passado, Passado.AddDays(9)), resultado], iniciadas)).Should().Equal(
            ("cronograma.fases[PROVA].fim", RetificacaoErrorCodes.FaseConcluidaAlterada));
        Recusas(Conferir([prova, resultado], [ComJanela(prova, Passado.AddDays(1), Passado.AddDays(9)), resultado], iniciadas)).Should().Equal(
            ("cronograma.fases[PROVA].inicio", RetificacaoErrorCodes.FaseConcluidaAlterada),
            ("cronograma.fases[PROVA].fim", RetificacaoErrorCodes.FaseConcluidaAlterada));
    }

    [Fact(DisplayName = "O início da fase em andamento não muda, nem adiado nem antecipado")]
    public void FaseEmAndamento_InicioMuda_Recusa()
    {
        FaseCronograma prova = EmAndamento();
        FasesIniciadas iniciadas = Iniciadas(prova);

        foreach (DateTimeOffset novo in new[] { Passado.AddDays(1), Passado.AddDays(-1) })
        {
            Recusas(Conferir([prova], [ComJanela(prova, novo, prova.Fim)], iniciadas)).Should().Equal(
                ("cronograma.fases[PROVA].inicio", RetificacaoErrorCodes.InicioDeFaseIniciadaAlterado));
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
            ("cronograma.fases[PROVA].fim", RetificacaoErrorCodes.TerminoDeFaseEmAndamentoAntecipado));
        Conferir([prova], [ComJanela(prova, prova.Inicio, Instante.AddDays(30))], iniciadas).Should().BeEmpty();
    }

    [Fact(DisplayName = "O início e o término da fase em andamento recusados juntos acumulam, cada um no seu campo")]
    public void FaseEmAndamento_InicioETermino_Acumulam()
    {
        FaseCronograma prova = EmAndamento();

        Recusas(Conferir([prova], [ComJanela(prova, Passado.AddDays(1), Instante.AddDays(9))], Iniciadas(prova))).Should().Equal(
            ("cronograma.fases[PROVA].inicio", RetificacaoErrorCodes.InicioDeFaseIniciadaAlterado),
            ("cronograma.fases[PROVA].fim", RetificacaoErrorCodes.TerminoDeFaseEmAndamentoAntecipado));
    }

    [Fact(DisplayName = "O término das inscrições nunca é antecipado, nem antes de abrir; prorrogar vale, mesmo com o prazo vencido")]
    public void Inscricao_TerminoNuncaAntecipado()
    {
        FaseCronograma aberta = Fase(1, "INSCRICAO", (Passado, Instante.AddDays(10)), coletaInscricao: true);
        FaseCronograma vencida = Fase(1, "INSCRICAO", (Passado, Instante.AddDays(-1)), coletaInscricao: true);
        FaseCronograma futura = Fase(1, "INSCRICAO", (Instante.AddDays(5), Instante.AddDays(15)), coletaInscricao: true);
        FaseCronograma homologacao = Fase(2, "HOMOLOGACAO");

        Recusas(Conferir([aberta], [ComJanela(aberta, aberta.Inicio, Instante.AddDays(9))], Iniciadas(aberta))).Should().Equal(
            ("cronograma.fases[INSCRICAO].fim", RetificacaoErrorCodes.TerminoDaInscricaoAntecipado));
        Recusas(Conferir([futura], [ComJanela(futura, futura.Inicio, Instante.AddDays(14))], Iniciadas())).Should().Equal(
            ("cronograma.fases[INSCRICAO].fim", RetificacaoErrorCodes.TerminoDaInscricaoAntecipado));
        Conferir([vencida, homologacao], [ComJanela(vencida, vencida.Inicio, Instante.AddDays(3)), homologacao], Iniciadas(vencida)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A inscrição vencida, com a homologação já iniciada, está concluída e não aceita prorrogação")]
    public void Inscricao_ConcluidaPelaFaseSeguinte_NaoProrroga()
    {
        FaseCronograma vencida = Fase(1, "INSCRICAO", (Passado, Instante.AddDays(-1)), coletaInscricao: true);
        FaseCronograma homologacao = Fase(2, "HOMOLOGACAO");

        Recusas(Conferir([vencida, homologacao], [ComJanela(vencida, vencida.Inicio, Instante.AddDays(3)), homologacao], Iniciadas(vencida, homologacao))).Should().Equal(
            ("cronograma.fases[INSCRICAO].fim", RetificacaoErrorCodes.FaseConcluidaAlterada));
    }

    [Fact(DisplayName = "Adiar o início das inscrições antes de abrir é aceito: não é redução")]
    public void Inscricao_AdiarInicioAntesDeAbrir_Aceita()
    {
        FaseCronograma futura = Fase(1, "INSCRICAO", (Instante.AddDays(5), Instante.AddDays(15)), coletaInscricao: true);

        Conferir([futura], [ComJanela(futura, Instante.AddDays(8), futura.Fim)], Iniciadas()).Should().BeEmpty();
    }

    [Fact(DisplayName = "A janela de isenção não se reduz, mesmo antes de abrir, e prorrogar antes do término é aceito")]
    public void JanelaDeIsencao_NaoSeReduz()
    {
        FaseCronograma futura = Fase(1, "ISENCAO", (Instante.AddDays(2), Instante.AddDays(9)), coletaIsencao: true);

        Recusas(Conferir([futura], [ComJanela(futura, futura.Inicio, Instante.AddDays(8))], Iniciadas())).Should().Equal(
            ("cronograma.fases[ISENCAO].fim", RetificacaoErrorCodes.JanelaDeIsencaoReduzida));
        Conferir([futura], [ComJanela(futura, futura.Inicio, Instante.AddDays(12))], Iniciadas()).Should().BeEmpty();
    }

    [Fact(DisplayName = "A janela de isenção encerrada não reabre: mudar o término é recusado, inclusive com o término no instante")]
    public void JanelaDeIsencaoEncerrada_NaoReabre()
    {
        FaseCronograma encerrada = Fase(1, "ISENCAO", (Passado, Instante.AddDays(-1)), coletaIsencao: true);
        FaseCronograma noInstante = Fase(1, "ISENCAO", (Passado, Instante), coletaIsencao: true);
        FaseCronograma seguinte = Fase(2, "INSCRICAO");

        Recusas(Conferir([encerrada, seguinte], [ComJanela(encerrada, encerrada.Inicio, Instante.AddDays(3)), seguinte], Iniciadas(encerrada))).Should().Equal(
            ("cronograma.fases[ISENCAO].fim", RetificacaoErrorCodes.JanelaDeIsencaoEncerradaReaberta));
        Recusas(Conferir([noInstante, seguinte], [ComJanela(noInstante, noInstante.Inicio, Instante.AddDays(1)), seguinte], Iniciadas(noInstante))).Should().Equal(
            ("cronograma.fases[ISENCAO].fim", RetificacaoErrorCodes.JanelaDeIsencaoEncerradaReaberta));
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
        EtapaProcesso iniciada = EtapaComJanela("Prova objetiva", Passado);
        EtapaProcesso porComecar = EtapaComJanela("Prova discursiva", Instante.AddDays(5));
        FasesIniciadas iniciadas = new([], etapas: [new EtapaIniciada(iniciada.Id, iniciada.Nome, NaVersaoVigente: true)]);
        AndamentoDasFases andamento = new([], [iniciada, porComecar], Fatos(iniciadas));

        ConferenciaDosPrazosDaRetificacao.Conferir([], [CopiaComInicio(iniciada, Passado.AddDays(2)), CopiaComInicio(porComecar, Instante.AddDays(9))], andamento)
            .Select(static r => (r.Field, r.Error.Code)).Should().Equal(
                ("cronograma.etapas[Prova objetiva].inicio", RetificacaoErrorCodes.InicioDeEtapaIniciadaAlterado));
        ConferenciaDosPrazosDaRetificacao.Conferir([], [CopiaComInicio(iniciada, Passado), porComecar], andamento).Should().BeEmpty();
    }

    // A retificação preserva o Id da etapa; a cópia reproduz a proposta com o mesmo Id e o início trocado.
    private static EtapaProcesso CopiaComInicio(EtapaProcesso etapa, DateTimeOffset inicio)
    {
        EtapaProcesso copia = EtapaComJanela(etapa.Nome, inicio);
        typeof(EtapaProcesso).BaseType!.GetProperty(nameof(EtapaProcesso.Id))!.SetValue(copia, etapa.Id);

        return copia;
    }
}
