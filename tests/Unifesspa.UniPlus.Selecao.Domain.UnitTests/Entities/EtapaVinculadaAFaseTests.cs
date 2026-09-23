namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Globalization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A etapa declara a fase a que pertence, e a raiz resolve o vínculo. É o que permite
/// qualquer fase subdividir-se — não só a que o cadastro marcava como agrupadora — e o
/// que faz a habilitação com oito etapas ser exprimível.
/// </summary>
public sealed class EtapaVinculadaAFaseTests
{
    private static TipoEtapaSnapshot Tipo() =>
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "ANALISE_DOCUMENTAL", "Análise Documental", admitePontuacao: true, admiteEliminacao: true).Value!;

    private static ProcessoSeletivo Processo() => ProcessoSeletivo.Criar(
        "PS", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    /// <summary>
    /// Fase do cronograma, com ou sem janela. A origem da data acompanha o que foi declarado
    /// porque é ela que decide se a janela é obrigatória: só a fase com os dois extremos pode
    /// ser PROPRIA, e é DELEGADA que torna a janela meia ou ausente exprimível.
    /// </summary>
    private static FaseCronograma Fase(
        int ordem, string codigo, DateTimeOffset? inicio = null, DateTimeOffset? fim = null) =>
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, "CEPS",
            inicio is not null && fim is not null ? OrigemDataFase.Propria : OrigemDataFase.Delegada,
            agrupaEtapas: false, permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: inicio, fim: fim, produtos: [], faseConcluinteCodigo: null,
            emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!;

    private static EtapaProcesso Etapa(string nome, string? faseCodigo) => EtapaProcesso.Criar(
        nome, CaraterEtapa.Classificatoria, Tipo(), peso: 1m, notaMinima: null, ordem: null,
        faseCodigo: faseCodigo).Value!;

    private static DateTimeOffset Instante(string iso) =>
        DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static DateTimeOffset? InstanteOpcional(string? iso) =>
        iso is null ? null : Instante(iso);

    /// <summary>Certame com uma fase AVALIACAO de 1 a 10 de março de 2027.</summary>
    private static ProcessoSeletivo ProcessoComFaseDeMarco()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases(
            [Fase(1, "AVALIACAO", Instante("2027-03-01T00:00:00Z"), Instante("2027-03-10T00:00:00Z"))],
            [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    /// <summary>
    /// Etapa com janela própria, que pode ser meia: declarar só um dos extremos é estado
    /// válido — o outro a etapa herda da fase.
    /// </summary>
    private static EtapaProcesso EtapaComJanela(
        string nome, string? faseCodigo, DateTimeOffset? inicio, DateTimeOffset? fim)
    {
        EtapaProcesso etapa = Etapa(nome, faseCodigo);
        etapa.DefinirJanelaEParecer(inicio, fim, emiteParecerIndividual: false).IsSuccess.Should().BeTrue();
        return etapa;
    }

    // ── A etapa acontece dentro da fase que a abriga ──
    //
    // A matriz abaixo é a mesma que o editor já aplica na tela: dentro passa, bordas
    // coincidentes passam, transbordo de qualquer lado recusa, e o que não foi declarado não
    // é conferido.

    [Theory(DisplayName = "Etapa contida na janela da fase é aceita")]
    [InlineData("2027-03-02T08:00:00Z", "2027-03-09T18:00:00Z")]   // dentro
    [InlineData("2027-03-01T00:00:00Z", "2027-03-10T00:00:00Z")]   // bordas coincidentes
    public void EtapaDentroDaFase_Aceita(string inicio, string fim)
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();

        Result resultado = processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO", Instante(inicio), Instante(fim))],
            PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Etapa que começa antes da fase é recusada, nomeando etapa e fase")]
    public void EtapaComecaAntesDaFase_Recusada()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();

        Result resultado = processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO",
                Instante("2027-02-28T08:00:00Z"), Instante("2027-03-09T18:00:00Z"))],
            PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaComecaAntesDaFase");
        resultado.Error.Message.Should().Contain("Prova objetiva").And.Contain("AVALIACAO");
    }

    [Fact(DisplayName = "Etapa que termina depois da fase é recusada, nomeando etapa e fase")]
    public void EtapaTerminaDepoisDaFase_Recusada()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();

        Result resultado = processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO",
                Instante("2027-03-02T08:00:00Z"), Instante("2027-03-11T18:00:00Z"))],
            PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaTerminaDepoisDaFase");
        resultado.Error.Message.Should().Contain("Prova objetiva").And.Contain("AVALIACAO");
    }

    [Fact(DisplayName = "Etapa que transborda dos dois lados é recusada pelo início, sem somar mensagens")]
    public void EtapaTransbordaDosDoisLados_RecusaPeloInicio()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();

        Result resultado = processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO",
                Instante("2027-02-28T08:00:00Z"), Instante("2027-03-11T18:00:00Z"))],
            PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaComecaAntesDaFase");
    }

    [Fact(DisplayName = "Etapa sem janela não é conferida — em branco ela acontece na janela da fase")]
    public void EtapaSemJanela_NaoEhConferida()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();

        Result resultado = processo.DefinirEtapas(
            [Etapa("Prova objetiva", "AVALIACAO")], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Fase sem janela não contém nada — a etapa datada passa")]
    public void FaseSemJanela_NaoEhConferida()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases([Fase(1, "AVALIACAO")], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO",
                Instante("2027-03-02T08:00:00Z"), Instante("2027-03-09T18:00:00Z"))],
            PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(
            "exigir data da fase por causa da etapa inventaria obrigação que o cadastro não faz");
    }

    [Fact(DisplayName = "Etapa sem fase declarada não é conferida contra fase alguma")]
    public void EtapaSemFaseDeclarada_NaoEhConferida()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();

        Result resultado = processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", faseCodigo: null,
                Instante("2020-01-01T08:00:00Z"), Instante("2020-01-02T18:00:00Z"))],
            PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ── A etapa que declara só um dos extremos ──
    //
    // Meia janela é estado válido: a etapa que só diz quando começa termina com a fase, e a
    // que só diz quando termina começa com ela. O extremo declarado é conferido contra os
    // DOIS extremos da fase, senão uma prova marcada para depois de a fase acabar passa por
    // não transbordar de nenhum lado que a comparação homônima alcance.

    [Theory(DisplayName = "Etapa de meia janela dentro da fase é aceita")]
    [InlineData("2027-03-02T08:00:00Z", null)]   // começa dentro, termina com a fase
    [InlineData(null, "2027-03-09T18:00:00Z")]   // começa com a fase, termina dentro
    public void EtapaDeMeiaJanelaDentroDaFase_Aceita(string? inicio, string? fim)
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();

        Result resultado = processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO", InstanteOpcional(inicio), InstanteOpcional(fim))],
            PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Theory(DisplayName = "Etapa de meia janela que acontece inteira fora da fase é recusada")]
    [InlineData("2027-03-20T08:00:00Z", null)]   // começa depois de a fase acabar
    [InlineData(null, "2027-02-20T18:00:00Z")]   // termina antes de a fase começar
    public void EtapaDeMeiaJanelaForaDaFase_Recusada(string? inicio, string? fim)
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();

        Result resultado = processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO", InstanteOpcional(inicio), InstanteOpcional(fim))],
            PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue(
            "a etapa herda da fase o extremo que não declarou, e herdá-lo aqui produziria uma "
            + "janela que termina antes de começar");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaForaDaJanelaDaFase");
        resultado.Error.Message.Should().Contain("Prova objetiva").And.Contain("AVALIACAO");
    }

    [Fact(DisplayName = "Fase de meia janela confere o extremo que declarou")]
    public void FaseDeMeiaJanela_ConfereOExtremoQueDeclarou()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases(
            [Fase(1, "AVALIACAO", inicio: null, fim: Instante("2027-03-10T00:00:00Z"))],
            [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO", Instante("2027-03-20T08:00:00Z"), fim: null)],
            PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaForaDaJanelaDaFase");
    }

    [Fact(DisplayName = "Cada etapa é conferida contra a fase que ela declara, não contra a primeira do cronograma")]
    public void CadaEtapaEhConferidaContraAPropriaFase()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases(
            [
                Fase(1, "HABILITACAO", Instante("2027-03-01T00:00:00Z"), Instante("2027-03-10T00:00:00Z")),
                Fase(2, "AVALIACAO", Instante("2027-04-01T00:00:00Z"), Instante("2027-04-10T00:00:00Z")),
            ],
            [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // A primeira cabe na fase dela; a segunda cabe na janela da PRIMEIRA fase e não na
        // sua. Conferir toda etapa contra a fase de abertura declararia as duas conformes.
        Result resultado = processo.DefinirEtapas(
            [
                EtapaComJanela("Análise documental", "HABILITACAO",
                    Instante("2027-03-02T08:00:00Z"), Instante("2027-03-09T18:00:00Z")),
                EtapaComJanela("Prova objetiva", "AVALIACAO",
                    Instante("2027-03-05T08:00:00Z"), Instante("2027-03-06T18:00:00Z")),
            ],
            PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaComecaAntesDaFase");
        resultado.Error.Message.Should().Contain("Prova objetiva").And.Contain("AVALIACAO");
    }

    // ── A fase também se move ──
    //
    // A gravação das etapas confere a janela contra o cronograma DAQUELE instante. Quem
    // encolhe a fase depois desfaz a mesma invariante pelo outro lado, e não há gravação de
    // etapa adiante que a reconfira.

    [Fact(DisplayName = "Encolher a fase para fora da etapa já datada é recusado na gravação do cronograma")]
    public void CronogramaQueEncolheAFase_RecusaAEtapaJaDatada()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();
        processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO",
                Instante("2027-03-02T08:00:00Z"), Instante("2027-03-09T18:00:00Z"))],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirCronogramaFases(
            [Fase(1, "AVALIACAO", Instante("2027-03-05T00:00:00Z"), Instante("2027-03-10T00:00:00Z"))],
            [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue(
            "sem esta metade o certame volta a ser publicável com a prova pendurada fora da fase");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaComecaAntesDaFase");
    }

    [Fact(DisplayName = "Remover do cronograma a fase que hospeda a etapa datada é aceito — a etapa sai na poda")]
    public void CronogramaQueRemoveAFase_NaoEhBarradoPelaEtapaQueSaiJunto()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();
        processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO",
                Instante("2027-03-02T08:00:00Z"), Instante("2027-03-09T18:00:00Z"))],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirCronogramaFases(
            [Fase(1, "HABILITACAO", Instante("2027-05-01T00:00:00Z"), Instante("2027-05-10T00:00:00Z"))],
            [], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Should().BeEmpty("a etapa da fase removida sai junto com ela");
    }

    [Fact(DisplayName = "O checklist projeta a janela da etapa na fase — o gate e a exibição dizem a mesma coisa")]
    public void ChecklistProjetaAJanelaDaEtapaNaFase()
    {
        // As duas gravações recusam a incoerência na escrita, mas o EF hidrata etapas e fases
        // direto das linhas: o gate de publicação é a metade que alcança o certame gravado
        // antes de a invariante existir. Sem o item correspondente, a publicação recusaria por
        // uma causa que o checklist declarava verde, e quem lê o painel não teria o que
        // corrigir.
        IReadOnlyList<ItemConformidade> checklist =
            ProcessoConformeFactory.Criar().AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario);

        checklist.Should().ContainSingle(i => i.Codigo == "cronograma_etapa_fora_da_janela_da_fase")
            .Which.Ok.Should().BeTrue("o processo conforme não tem etapa pendurada fora da fase");
    }

    [Fact(DisplayName = "Etapa que declara fase presente no cronograma passa a ser encontrada por aquela fase")]
    public void EtapaComFaseDeclarada_EhEncontradaPelaFase()
    {
        ProcessoSeletivo processo = Processo();
        FaseCronograma habilitacao = Fase(1, "HABILITACAO");
        processo.DefinirCronogramaFases([habilitacao], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas(
            [Etapa("Envio dos documentos pessoais", "HABILITACAO")], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue();

        // A relação tem uma representação só — o código —, e o que se afirma dela é o que se
        // lê dela: a etapa aparece sob a fase que declarou, e não sob nenhuma outra.
        processo.EtapasDaFase("HABILITACAO").Should().ContainSingle()
            .Which.Nome.Should().Be("Envio dos documentos pessoais");
        processo.Etapas.Single().FaseCodigo.Should().Be(habilitacao.Codigo);
    }

    [Fact(DisplayName = "Etapa que declara fase fora do cronograma é recusada, nomeando a fase")]
    public void EtapaComFaseAusente_Recusada()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases([Fase(1, "INSCRICAO")], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas(
            [Etapa("Envio dos documentos pessoais", "HABILITACAO")], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaSemFaseNoCronograma");
        resultado.Error!.Message.Should().Contain("HABILITACAO");
    }

    [Fact(DisplayName = "Qualquer fase subdivide-se: habilitação com várias etapas é aceita")]
    public void FaseNaoAgrupadora_AceitaVariasEtapas()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases([Fase(1, "HABILITACAO")], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas(
            [
                Etapa("Preenchimento do cadastro acadêmico", "HABILITACAO"),
                Etapa("Envio dos documentos pessoais", "HABILITACAO"),
                Etapa("Envio dos comprovantes da cota de renda", "HABILITACAO"),
            ],
            PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue();
        processo.EtapasDaFase("HABILITACAO").Should().HaveCount(3);
        processo.EtapasDaFase("INSCRICAO").Should().BeEmpty();
    }

    [Fact(DisplayName = "Etapas de fases distintas ficam cada uma na sua")]
    public void EtapasDeFasesDistintas_NaoSeMisturam()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases(
            [Fase(1, "AVALIACAO"), Fase(2, "HABILITACAO")], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirEtapas(
            [Etapa("Prova objetiva", "AVALIACAO"), Etapa("Envio dos documentos pessoais", "HABILITACAO")],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.EtapasDaFase("AVALIACAO").Single().Nome.Should().Be("Prova objetiva");
        processo.EtapasDaFase("HABILITACAO").Single().Nome.Should().Be("Envio dos documentos pessoais");
    }

    [Fact(DisplayName = "Fase agrupadora com etapa declarada nela não é recusada por falta de etapa")]
    public void FaseAgrupadora_ComEtapaDeclarada_Aceita()
    {
        ProcessoSeletivo processo = Processo();
        FaseCronograma avaliacao = FaseCronograma.Criar(
            1, Guid.CreateVersion7(), "AVALIACAO", "CEPS", OrigemDataFase.Delegada,
            agrupaEtapas: true, permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: null, fim: null, produtos: [], faseConcluinteCodigo: null,
            emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!;

        // A ordem que o vínculo impõe: a fase entra primeiro, e só então a etapa pode
        // declará-la. É por isso que a guarda eager de "agrupadora sem etapa" saiu da
        // gravação do cronograma — ali ela fecharia um ciclo sem ordem possível.
        processo.DefinirCronogramaFases([avaliacao], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas(
            [Etapa("Prova objetiva", "AVALIACAO")], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue();
        processo.EtapasDaFase("AVALIACAO").Should().HaveCount(1);
    }

    [Fact(DisplayName = "Etapa sem fase declarada continua aceita — é o formato anterior ao vínculo")]
    public void EtapaSemFaseDeclarada_ContinuaAceita()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases([Fase(1, "AVALIACAO")], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas(
            [Etapa("Prova objetiva", faseCodigo: null)], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue();

        // Sem fase declarada não há relação, e é assim que se diz isso agora: o código fica
        // nulo e a etapa não aparece sob fase nenhuma. Antes havia um id zerado convivendo com
        // o código nulo, e "sem fase" e "fase que não existe" tinham a mesma aparência.
        processo.Etapas.Single().FaseCodigo.Should().BeNull();
        processo.EtapasDaFase("AVALIACAO").Should().BeEmpty();
    }
}
