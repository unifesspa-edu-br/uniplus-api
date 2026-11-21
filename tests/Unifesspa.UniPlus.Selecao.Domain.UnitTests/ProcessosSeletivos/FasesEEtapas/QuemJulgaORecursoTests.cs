namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.CenarioDeProcesso;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;

using Nivel = Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas.QuemJulgaORecurso.Nivel;

/// <summary>Quem julga o recurso de cada fase e de cada etapa: a banca da etapa, a da fase em que ela acontece, ou ninguém.</summary>
public sealed class QuemJulgaORecursoTests
{
    private static ModeloDeFase FaseQueAdmiteRecurso(string codigo, params BancaRequerida[] bancas)
    {
        ProdutoDaFase preliminar = ModeloDeFase.Preliminar("RESULTADO");

        return new ModeloDeFase(codigo, 1) { Produtos = [preliminar], RegraRecurso = ModeloDeFase.RecursoAncoradoEm(preliminar.Id), Bancas = bancas };
    }

    private static ModeloDeEtapa EtapaQueAbreRecurso(string nome, string codigoDaFase, params BancaDaEtapa[] bancas) =>
        new(nome)
        {
            FaseCodigo = codigoDaFase,
            Recursos = [RecursoModelo.NaCiencia()],
            EmiteParecerIndividual = true,
            Bancas = bancas,
        };

    [Fact(DisplayName = "O recurso da fase é julgado pela banca de recursos da fase, e sem ela por ninguém")]
    public void NivelDoRecursoDaFase_JulgaSoABancaDaFase()
    {
        QuemJulgaORecurso.NivelDoRecursoDaFase(faseTemBanca: true).Should().Be(Nivel.BancaDeRecursosDaFase);
        QuemJulgaORecurso.NivelDoRecursoDaFase(faseTemBanca: false).Should().Be(Nivel.Ninguem);
    }

    [Fact(DisplayName = "O recurso da etapa é julgado pela banca da etapa; sem ela, pela da fase; sem nenhuma, por ninguém")]
    public void NivelDoRecursoDaEtapa_ONivelMaisProximoDecide()
    {
        QuemJulgaORecurso.NivelDoRecursoDaEtapa(etapaTemBanca: true, faseTemBanca: true).Should().Be(Nivel.BancaDeRecursosDaEtapa);
        QuemJulgaORecurso.NivelDoRecursoDaEtapa(etapaTemBanca: true, faseTemBanca: false).Should().Be(Nivel.BancaDeRecursosDaEtapa);
        QuemJulgaORecurso.NivelDoRecursoDaEtapa(etapaTemBanca: false, faseTemBanca: true).Should().Be(Nivel.BancaDeRecursosDaFase);
        QuemJulgaORecurso.NivelDoRecursoDaEtapa(etapaTemBanca: false, faseTemBanca: false).Should().Be(Nivel.Ninguem);
    }

    [Fact(DisplayName = "As fases que admitem recurso entram com a posição no cronograma e quem as julga; a fase sem recurso não entra")]
    public void DasFases_SoAsQueAdmitemRecurso()
    {
        CronogramaDoProcesso cronograma = Fases(
            new ModeloDeFase("INSCRICAO", 1),
            FaseQueAdmiteRecurso("RESULTADO", BancasDeTeste.DeRecursosDaFase()) with { Ordem = 2 },
            FaseQueAdmiteRecurso("CLASSIFICACAO") with { Ordem = 3 });

        QuemJulgaORecurso.DasFases(cronograma)
            .Select(static recurso => (recurso.Fase.Codigo, recurso.Posicao, recurso.Nivel))
            .Should().Equal(("RESULTADO", 1, Nivel.BancaDeRecursosDaFase), ("CLASSIFICACAO", 2, Nivel.Ninguem));
    }

    [Fact(DisplayName = "A etapa com banca de recursos própria é julgada por ela, mesmo que a fase também tenha a sua")]
    public void DasEtapas_ComBancaPropriaEDeFase_JulgaABancaDaEtapa()
    {
        CronogramaDoProcesso cronograma = Fases(new ModeloDeFase("AVALIACAO", 1) with { Bancas = [BancasDeTeste.DeRecursosDaFase()] });
        EtapasDoProcesso etapas = Etapas(EtapaQueAbreRecurso("Redação", "AVALIACAO", BancasDeTeste.DeRecursosDaEtapa()));

        QuemJulgaORecurso.DasEtapas(cronograma, etapas).Single().Nivel.Should().Be(Nivel.BancaDeRecursosDaEtapa);
    }

    [Fact(DisplayName = "A etapa sem banca de recursos própria é julgada pela banca de recursos da fase em que acontece")]
    public void DasEtapas_SemBancaPropria_JulgaABancaDaFase()
    {
        CronogramaDoProcesso cronograma = Fases(new ModeloDeFase("AVALIACAO", 1) with { Bancas = [BancasDeTeste.DeRecursosDaFase()] });
        EtapasDoProcesso etapas = Etapas(EtapaQueAbreRecurso("Redação", "AVALIACAO"));

        QuemJulgaORecurso.DasEtapas(cronograma, etapas).Single().Nivel.Should().Be(Nivel.BancaDeRecursosDaFase);
    }

    [Fact(DisplayName = "A banca de recursos de outra fase não julga a etapa")]
    public void DasEtapas_BancaDeOutraFase_NaoJulga()
    {
        CronogramaDoProcesso cronograma = Fases(
            new ModeloDeFase("AVALIACAO", 1),
            new ModeloDeFase("RESULTADO", 2) with { Bancas = [BancasDeTeste.DeRecursosDaFase()] });
        EtapasDoProcesso etapas = Etapas(EtapaQueAbreRecurso("Redação", "AVALIACAO"));

        QuemJulgaORecurso.DasEtapas(cronograma, etapas).Single().Nivel.Should().Be(Nivel.Ninguem);
    }

    [Fact(DisplayName = "A etapa sem recurso não entra, e a posição é a da lista das etapas")]
    public void DasEtapas_SoComRecurso_ComAPosicaoDaLista()
    {
        CronogramaDoProcesso cronograma = Fases(new ModeloDeFase("AVALIACAO", 1));
        EtapasDoProcesso etapas = Etapas(
            EtapaNaFase("AVALIACAO", "Prova"),
            EtapaQueAbreRecurso("Redação", "AVALIACAO"));

        QuemJulgaORecurso.DasEtapas(cronograma, etapas)
            .Select(static recurso => (recurso.Etapa.Nome, recurso.Posicao))
            .Should().Equal(("Redação", 1));
    }

    [Theory(DisplayName = "A etapa cuja lista das bancas ou dos recursos tem parte recusada tem julgadora indeterminada e não entra")]
    [InlineData("etapas[1].bancas")]
    [InlineData("etapas[1].recursos")]
    public void DasEtapas_ListaRecusadaNaEtapa_JulgadoraIndeterminadaFicaDeFora(string colecaoRecusada)
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([new ModeloDeFase("AVALIACAO", 1).Adotada()]);
        EtapaProcesso[] etapas =
        [
            EtapaQueAbreRecurso("Redação", "AVALIACAO").Adotada(),
            EtapaQueAbreRecurso("Entrevista", "AVALIACAO").Adotada(),
        ];
        EtapasDoProcesso comParteRecusada = EtapasDoProcesso.Das(etapas, ComPartesRecusadas(colecaoRecusada));

        QuemJulgaORecurso.DasEtapas(cronograma, comParteRecusada).Select(static recurso => recurso.Etapa.Nome).Should().Equal("Redação");
        QuemJulgaORecurso.JulgadoraIndeterminada(cronograma, comParteRecusada.Etapas[1]).Should().BeTrue();
        QuemJulgaORecurso.JulgadoraIndeterminada(cronograma, comParteRecusada.Etapas[0]).Should().BeFalse();
    }

    [Fact(DisplayName = "A etapa de fase com a lista das bancas recusada, ou de fase recusada na leitura, tem julgadora indeterminada")]
    public void JulgadoraIndeterminada_FaseComListaRecusadaOuRecusadaNaLeitura()
    {
        EtapasDoProcesso etapas = EtapasDoProcesso.Das([EtapaQueAbreRecurso("Redação", "AVALIACAO").Adotada()]);
        CronogramaDoProcesso comListaRecusada = CronogramaDoProcesso.Do([new ModeloDeFase("AVALIACAO", 1).Adotada()], ComPartesRecusadas("fases[0].bancasRequeridas"));
        CronogramaDoProcesso comFaseRecusada = CronogramaDoProcesso.Do([], ComPartesRecusadas("fases"));
        CronogramaDoProcesso semRecusa = CronogramaDoProcesso.Do([new ModeloDeFase("AVALIACAO", 1).Adotada()]);

        QuemJulgaORecurso.JulgadoraIndeterminada(comListaRecusada, etapas.Etapas[0]).Should().BeTrue();
        QuemJulgaORecurso.JulgadoraIndeterminada(comFaseRecusada, etapas.Etapas[0]).Should().BeTrue();
        QuemJulgaORecurso.JulgadoraIndeterminada(semRecusa, etapas.Etapas[0]).Should().BeFalse();
    }

    [Fact(DisplayName = "O recurso que ninguém julga é recusado como sem banca, a fase antes da etapa, na posição de cada uma")]
    public void SemBanca_RecusaFaseEEtapaSemQuemJulgue()
    {
        CronogramaDoProcesso cronograma = Fases(FaseQueAdmiteRecurso("AVALIACAO"));
        EtapasDoProcesso etapas = Etapas(EtapaQueAbreRecurso("Redação", "AVALIACAO"));

        IReadOnlyList<FieldError> recusas = QuemJulgaORecurso.SemBanca(cronograma, etapas);

        Causas(recusas).Should().Equal(
            ("fases[0].bancasRequeridas", "Cronograma.RecursoSemBanca"),
            ("etapas[0].bancas", "Cronograma.RecursoSemBanca"));
        recusas[0].Error.Message.Should().Be("A fase 'AVALIACAO' admite recurso e não declara a banca de análise de recursos.");
        recusas[1].Error.Message.Should().Be("A etapa 'Redação' abre recurso, e nem ela nem a fase em que acontece declaram a banca de análise de recursos.");
    }

    [Fact(DisplayName = "O recurso julgado por alguma banca, ou de julgadora indeterminada, não é recusado")]
    public void SemBanca_JulgadoOuIndeterminado_SemRecusa()
    {
        CronogramaDoProcesso comBanca = Fases(FaseQueAdmiteRecurso("AVALIACAO", BancasDeTeste.DeRecursosDaFase()));
        CronogramaDoProcesso semBanca = Fases(new ModeloDeFase("AVALIACAO", 1));
        EtapasDoProcesso etapas = Etapas(EtapaQueAbreRecurso("Redação", "AVALIACAO"));

        QuemJulgaORecurso.SemBanca(comBanca, etapas).Should().BeEmpty();
        QuemJulgaORecurso.SemBanca(semBanca, EtapasDoProcesso.Das([EtapaQueAbreRecurso("Redação", "AVALIACAO").Adotada()], ComPartesRecusadas("etapas[0].bancas"))).Should().BeEmpty();
    }
}
