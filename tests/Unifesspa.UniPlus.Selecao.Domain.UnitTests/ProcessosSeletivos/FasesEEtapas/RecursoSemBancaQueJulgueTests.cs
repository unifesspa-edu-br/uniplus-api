namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.CenarioDeProcesso;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.RecursosParaQuemJulga;

/// <summary>
/// A recusa do recurso que nenhuma banca julga em 1ª instância, com o texto de cada caso, e o que deixa o item pendente: a leitura
/// do documento recusou parte do que decide quem julga.
/// </summary>
public sealed class RecursoSemBancaQueJulgueTests
{
    private const string Fase = "AVALIACAO";
    private const string Papel = "Julga o recurso em 1ª instância";

    private static readonly ProcedimentoDeRecursoDeclarado[] DaNota = [Procedimento(TipoMotivoDeDecisao.NotaDaEtapa)];

    private static readonly TipoDeBancaCopiado Entrevista = Avaliadora(TipoDeBanca.Entrevista, false, TipoMotivoDeDecisao.NotaDaEtapa);

    private static IReadOnlyList<FieldError> Recusas(ModeloDeFase fase, params ModeloDeEtapa[] etapas) =>
        QuemJulgaORecurso.SemBanca(Fases(fase), Etapas(etapas));

    [Fact(DisplayName = "A fase com recurso de um tipo de decisão e sem quem o julgue é recusada, com o detalhe da fase")]
    public void FaseComTipoDeDecisao_SemBanca_RecusaComODetalheDaFase()
    {
        ModeloDeFase fase = FaseComRecurso(Fase, [Procedimento(TipoMotivoDeDecisao.HomologacaoDasInscricoes)]);
        ModeloDeEtapa etapa = EtapaNaFase(Fase) with { Bancas = [NaEtapa(Entrevista)] };

        FieldError recusa = Recusas(fase, etapa).Should().ContainSingle().Subject;

        recusa.Field.Should().Be("fases[0].bancasRequeridas");
        recusa.Error.Code.Should().Be("Cronograma.RecursoSemBanca");
        recusa.Error.Message.Should().Be(
            $"A fase '{Fase}' admite recurso de Homologação das inscrições, e nenhuma banca prevista o julga em 1ª instância: a fase não tem banca com o papel '{Papel}', e nem a fase nem as etapas dela têm banca que tenha avaliado esse item. Acrescente uma dessas bancas.");
    }

    [Fact(DisplayName = "A etapa com recurso de um tipo de decisão e sem quem o julgue é recusada, com o detalhe da etapa")]
    public void EtapaComTipoDeDecisao_SemBanca_RecusaComODetalheDaEtapa()
    {
        TipoDeBancaCopiado documental = Avaliadora(TipoDeBanca.AnaliseDocumental, false, TipoMotivoDeDecisao.AnaliseDocumental);
        ModeloDeEtapa etapa = EtapaComRecurso("Redação", Fase, DaNota, false, NaEtapa(documental));

        FieldError recusa = Recusas(new ModeloDeFase(Fase, 1), etapa).Should().ContainSingle().Subject;

        recusa.Field.Should().Be("etapas[0].bancas");
        recusa.Error.Message.Should().Be(
            $"A etapa na posição 1 admite recurso de Nota da etapa, e nenhuma banca prevista o julga em 1ª instância: nem a etapa nem a fase '{Fase}', em que ela acontece, têm banca com o papel '{Papel}', e a etapa não tem banca que tenha avaliado esse item. Acrescente uma dessas bancas.");
    }

    [Fact(DisplayName = "O recurso sem procedimento declarado é recusado, na fase e na etapa, dizendo que só a banca com o papel pode julgá-lo")]
    public void RecursoSemProcedimento_SemBanca_RecusaComODetalheSemProcedimento()
    {
        ModeloDeEtapa etapa = EtapaComRecurso("Redação", Fase, [], false, NaEtapa(Entrevista));

        IReadOnlyList<FieldError> recusas = Recusas(FaseComRecurso(Fase, []), etapa);

        recusas.Select(static recusa => recusa.Error.Message).Should().Equal(
            $"A fase '{Fase}' admite recurso sem procedimento declarado, e só uma banca com o papel '{Papel}' pode julgá-lo. Acrescente essa banca.",
            $"A etapa na posição 1 admite recurso sem procedimento declarado, e só uma banca com o papel '{Papel}' pode julgá-lo. Acrescente essa banca.");
    }

    [Fact(DisplayName = "As recusas saem com as das fases antes das das etapas, e o mesmo texto no mesmo lugar sai uma vez")]
    public void Recusas_FasesAntesDasEtapas_SemRepetirOMesmoTexto()
    {
        Guid a = Guid.CreateVersion7();
        ProcedimentoDeRecursoDeclarado[] doisAlvos =
        [
            Procedimento(TipoMotivoDeDecisao.NotaDaEtapa, AlvoDoProcedimentoDeRecurso.DoItem(a)),
            Procedimento(TipoMotivoDeDecisao.NotaDaEtapa, AlvoDoProcedimentoDeRecurso.DoItem(Guid.CreateVersion7())),
        ];

        IReadOnlyList<FieldError> recusas = Recusas(FaseComRecurso(Fase, doisAlvos), EtapaComRecurso("Redação", Fase, DaNota));

        Causas(recusas).Should().Equal(("fases[0].bancasRequeridas", "Cronograma.RecursoSemBanca"), ("etapas[0].bancas", "Cronograma.RecursoSemBanca"));
    }

    [Fact(DisplayName = "O mesmo tipo de decisão com dois alvos, sem quem o julgue, gera uma só recusa")]
    public void MesmoTipoDeDecisaoComDoisAlvos_UmaSoRecusa()
    {
        ProcedimentoDeRecursoDeclarado[] doisAlvos =
        [
            Procedimento(TipoMotivoDeDecisao.NotaDaEtapa, AlvoDoProcedimentoDeRecurso.DoItem(Guid.CreateVersion7())),
            Procedimento(TipoMotivoDeDecisao.NotaDaEtapa, AlvoDoProcedimentoDeRecurso.DoItem(Guid.CreateVersion7())),
        ];

        IReadOnlyList<FieldError> recusas = Recusas(FaseComRecurso(Fase, doisAlvos));

        recusas.Should().ContainSingle().Which.Error.Message.Should().Contain("admite recurso de Nota da etapa");
    }

    [Fact(DisplayName = "Dois tipos de decisão sem quem os julgue geram duas recusas, na ordem canônica dos tipos")]
    public void DoisTiposDeDecisao_DuasRecusasNaOrdemCanonica()
    {
        ProcedimentoDeRecursoDeclarado[] doisTipos = [Procedimento(TipoMotivoDeDecisao.NotaDaEtapa), Procedimento(TipoMotivoDeDecisao.AnaliseDocumental)];

        IReadOnlyList<FieldError> recusas = Recusas(FaseComRecurso(Fase, doisTipos));

        recusas.Select(static recusa => recusa.Error.Message).Should().SatisfyRespectively(
            static primeira => primeira.Should().Contain("admite recurso de Análise documental"),
            static segunda => segunda.Should().Contain("admite recurso de Nota da etapa"));
    }

    [Fact(DisplayName = "O recurso com banca de recursos na fase, na etapa ou avaliadora do item não é recusado")]
    public void RecursoComQuemJulgue_SemRecusa()
    {
        ModeloDeFase comDedicada = FaseComRecurso(Fase, DaNota, false, NaFase(TiposDeBancaCopiadosDeTeste.DeRecursos()));
        ModeloDeEtapa comAvaliadora = EtapaComRecurso("Redação", Fase, DaNota, false, NaEtapa(Entrevista));

        Recusas(comDedicada, EtapaComRecurso("Prova", Fase, DaNota)).Should().BeEmpty();
        Recusas(new ModeloDeFase(Fase, 1), comAvaliadora).Should().BeEmpty();
    }

    [Theory(DisplayName = "A etapa com a lista das bancas, a dos recursos ou os tipos de decisão com parte recusada fica sem causa")]
    [InlineData("etapas[0].bancas")]
    [InlineData("etapas[0].recursos")]
    public void Etapa_ComParteRecusada_SemCausa(string colecaoRecusada)
    {
        EtapaProcesso etapa = EtapaComRecurso("Redação", Fase, DaNota).Adotada();
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([new ModeloDeFase(Fase, 1).Adotada()]);
        EtapasDoProcesso etapas = EtapasDoProcesso.Das([etapa], ComPartesRecusadas(colecaoRecusada));

        QuemJulgaORecurso.SemBanca(cronograma, etapas).Should().BeEmpty();
        QuemJulgaORecurso.Itens(cronograma, etapas).Single().Indeterminado.Should().BeTrue();
    }

    [Fact(DisplayName = "A fase da etapa com a lista das bancas recusada deixa o recurso da etapa sem causa")]
    public void RecursoDaEtapa_FaseComBancasRecusadas_SemCausa()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([new ModeloDeFase(Fase, 1).Adotada()], ComPartesRecusadas("fases[0].bancasRequeridas"));
        EtapasDoProcesso etapas = EtapasDoProcesso.Das([EtapaComRecurso("Redação", Fase, DaNota).Adotada()]);

        QuemJulgaORecurso.SemBanca(cronograma, etapas).Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase recusada na leitura deixa o recurso da etapa dela sem causa")]
    public void RecursoDaEtapa_FaseRecusadaNaLeitura_SemCausa()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([], ComPartesRecusadas("fases"));
        EtapasDoProcesso etapas = EtapasDoProcesso.Das([EtapaComRecurso("Redação", Fase, DaNota).Adotada()]);

        QuemJulgaORecurso.SemBanca(cronograma, etapas).Should().BeEmpty();
    }

    [Fact(DisplayName = "A lista dos procedimentos da fase recusada deixa o recurso da fase sem causa")]
    public void RecursoDaFase_ProcedimentosRecusados_SemCausa()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(
            [FaseComRecurso(Fase, DaNota).Adotada()], ComPartesRecusadas("fases[0].regraRecurso.procedimentos"));

        QuemJulgaORecurso.SemBanca(cronograma, EtapasDoProcesso.Das([])).Should().BeEmpty();
    }

    [Fact(DisplayName = "Os tipos de decisão da fase recusados deixam o recurso da fase sem causa")]
    public void RecursoDaFase_TiposDeDecisaoRecusados_SemCausa()
    {
        FaseDeclarada fase = (FaseComRecurso(Fase, DaNota) with { TiposDeDecisao = ["CODIGO_QUE_NAO_EXISTE"] }).Declarar();

        fase.TiposDeDecisaoRecusados.Should().BeTrue();
        QuemJulgaORecurso.SemBanca(new CronogramaDoProcesso([fase]), Etapas()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem alvo, qualquer etapa da fase com a lista das bancas recusada deixa o recurso da fase sem causa")]
    public void RecursoDaFaseSemAlvo_QualquerEtapaComBancasRecusadas_SemCausa()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([FaseComRecurso(Fase, DaNota).Adotada()]);
        EtapasDoProcesso etapas = EtapasDoProcesso.Das([EtapaNaFase(Fase).Adotada(), EtapaNaFase(Fase, "Outra").Adotada()], ComPartesRecusadas("etapas[1].bancas"));

        QuemJulgaORecurso.SemBanca(cronograma, etapas).Should().BeEmpty();
    }

    [Fact(DisplayName = "Com alvo em item de avaliação, só a etapa que contém o alvo com bancas recusadas deixa o recurso da fase sem causa")]
    public void RecursoDaFaseComAlvoEmItem_SoAEtapaDoAlvoImporta()
    {
        EtapaProcesso[] etapas =
        [
            (EtapaNaFase(Fase, "Com alvo") with { Itens = [new ItemDeAvaliacaoDeclarado(null, "Item", 10m)] }).Adotada(),
            EtapaNaFase(Fase, "Outra").Adotada(),
        ];

        ProcedimentoDeRecursoDeclarado[] doItem = [Procedimento(TipoMotivoDeDecisao.NotaDaEtapa, AlvoDoProcedimentoDeRecurso.DoItem(etapas[0].ItensDeAvaliacao.Single().Id))];
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([FaseComRecurso(Fase, doItem).Adotada()]);

        QuemJulgaORecurso.SemBanca(cronograma, EtapasDoProcesso.Das(etapas, ComPartesRecusadas("etapas[0].bancas"))).Should().BeEmpty();
        QuemJulgaORecurso.SemBanca(cronograma, EtapasDoProcesso.Das(etapas, ComPartesRecusadas("etapas[1].bancas"))).Should().ContainSingle();
    }
}
