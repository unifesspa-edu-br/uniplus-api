namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;

/// <summary>
/// A regra que quantifica sobre uma lista da fase em que a leitura do documento recusou alguma parte não julga a fase: a lista
/// declarada não é a que o autor escreveu, e o que falta nela pode ser o que a regra exige. As demais fases e o que a regra
/// diz sem ler a lista seguem julgados, e o item do painel fica pendente em vez de aprovado por omissão.
/// </summary>
public sealed class SuspensaoPorParteRecusadaDaFaseTests
{
    private const string ParecerSemResultado = "FaseCronograma.ParecerIndividualSemResultado";

    private static ModeloDeFase ComParecerSemResultado(string codigo, int ordem) =>
        new ModeloDeFase(codigo, ordem) { EmiteParecerIndividual = true }.Janela(ordem, ordem + 1);

    private static string?[] CamposDasRecusas(CronogramaDoProcesso cronograma) =>
        [.. cronograma.RecusasDasDeclaracoes().Select(static recusa => recusa.Field)];

    [Fact(DisplayName = "O parecer individual sem resultado é recusado quando a lista dos produtos foi lida inteira")]
    public void ParecerSemResultado_SemParteRecusada_Recusa()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([ComParecerSemResultado("RESULTADO", 1).Adotada()]);

        cronograma.RecusasDasDeclaracoes().Select(static recusa => recusa.Error.Code).Should().Equal(ParecerSemResultado);
    }

    [Fact(DisplayName = "O parecer individual sem resultado não é julgado na fase cuja lista dos produtos tem parte recusada, e a outra fase segue julgada")]
    public void ParecerSemResultado_ComParteRecusadaNosProdutos_SuspendeSoNaFaseDaLista()
    {
        FaseCronograma[] fases = [ComParecerSemResultado("RESULTADO", 1).Adotada(), ComParecerSemResultado("RECURSO", 2).Adotada()];

        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(fases, ComPartesRecusadas("fases[1].produtos"));

        CamposDasRecusas(cronograma).Should().Equal("fases[0].emiteParecerIndividual");
        cronograma.RecusasDasDeclaracoes()[0].Error.Message.Should().NotContain("RESULTADO").And.Contain("parecer individual");
    }

    [Fact(DisplayName = "A âncora do recurso num produto que não é preliminar não é julgada quando a lista dos produtos tem parte recusada")]
    public void AncoraDoRecurso_ComParteRecusadaNosProdutos_NaoEJulgada()
    {
        ProdutoDaFase definitivo = ModeloDeFase.Definitivo("RESULTADO_PROVA");
        ModeloDeFase modelo = new ModeloDeFase("RESULTADO", 1) { Produtos = [definitivo], RegraRecurso = ModeloDeFase.RecursoAncoradoEm(definitivo.Id) }.Janela(1, 5);

        CronogramaDoProcesso.Do([modelo.Adotada()]).RecusasDasDeclaracoes().Should().NotBeEmpty();
        CronogramaDoProcesso.Do([modelo.Adotada()], ComPartesRecusadas("fases[0].produtos")).RecusasDasDeclaracoes().Should().BeEmpty();
    }

    [Fact(DisplayName = "O ato repetido entre os produtos que ficaram continua recusado quando a lista tem parte recusada")]
    public void AtoRepetido_ComParteRecusadaNosProdutos_ContinuaRecusado()
    {
        ModeloDeFase repetido = new ModeloDeFase("RESULTADO", 1).Janela(1, 5)
            .Publicando(ModeloDeFase.Preliminar("RESULTADO_PROVA"), ModeloDeFase.Preliminar("RESULTADO_PROVA"));

        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([repetido.Adotada()], ComPartesRecusadas("fases[0].produtos"));

        CamposDasRecusas(cronograma).Should().Equal("fases[0].produtos");
    }

    [Fact(DisplayName = "Na leitura do documento o ato repetido recusa a lista dos produtos inteira, uma vez só, sem posição de produto")]
    public void AtoRepetido_NaLeituraDoDocumento_RecusaAListaInteiraUmaVez()
    {
        ModeloDeFase repetido = new ModeloDeFase("RESULTADO", 1).Janela(1, 5).Publicando(
            ModeloDeFase.Preliminar("ATO_A"), ModeloDeFase.Preliminar("ATO_B"), ModeloDeFase.Preliminar("ATO_B"), ModeloDeFase.Preliminar("ATO_A"));

        CamposDasRecusas(CronogramaDoProcesso.Do([repetido.Adotada()])).Should().Equal("fases[0].produtos");
    }

    [Fact(DisplayName = "As vagas sem fase que produz resultado não são julgadas quando alguma fase tem parte recusada nos produtos, e o item do painel fica pendente")]
    public void VagasSemFaseQueProduzResultado_ComParteRecusadaNosProdutos_SuspendeEPendenta()
    {
        FaseCronograma[] fases = [new ModeloDeFase("INSCRICAO", 1) { ColetaInscricao = true }.Janela(1, 5).Adotada()];
        CronogramaDoProcesso lido = CronogramaDoProcesso.Do(fases);
        CronogramaDoProcesso comParteRecusada = CronogramaDoProcesso.Do(fases, ComPartesRecusadas("fases[0].produtos"));

        lido.ConferirCronogramaExigidoPeloProcesso(OrigemCandidatos.InscricaoPropria, haVagasOfertadas: true).Should().ContainSingle();
        comParteRecusada.ConferirCronogramaExigidoPeloProcesso(OrigemCandidatos.InscricaoPropria, haVagasOfertadas: true).Should().BeEmpty();
        lido.ItemDasVagasOfertadas(haVagasOfertadas: false).Ok.Should().BeTrue();
        comParteRecusada.ItemDasVagasOfertadas(haVagasOfertadas: false).Ok.Should().BeTrue();
        comParteRecusada.ItemDasVagasOfertadas(haVagasOfertadas: true).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "O fechamento do ciclo de recurso não é julgado na fase cuja lista dos produtos tem parte recusada, e a outra fase segue julgada")]
    public void FechamentoDoCiclo_ComParteRecusadaNosProdutos_SuspendeSoNaFaseDaLista()
    {
        FaseCronograma[] fases = [FaseQueAbreOCiclo("ANALISE", 1), FaseQueAbreOCiclo("RECURSO", 2)];

        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(fases, ComPartesRecusadas("fases[0].produtos"));

        CronogramaDoProcesso.Do(fases).ConferirFechamentoDoCiclo().Select(static recusa => recusa.Field).Should()
            .Equal("fases[0].produtos", "fases[1].produtos");
        cronograma.ConferirFechamentoDoCiclo().Select(static recusa => recusa.Field).Should().Equal("fases[1].produtos");
    }

    [Fact(DisplayName = "O item do fechamento do ciclo fica pendente, e não aprovado, quando a lista dos produtos de uma fase tem parte recusada")]
    public void ItemDaConclusaoDoCiclo_ComParteRecusadaNosProdutos_FicaPendente()
    {
        FaseCronograma[] fases = [new ModeloDeFase("INSCRICAO", 1).Janela(1, 5).Adotada()];
        EtapasDoProcesso semEtapas = EtapasDoProcesso.Das([]);

        CicloRecursalDoProcesso.Item(CronogramaDoProcesso.Do(fases), semEtapas).Ok.Should().BeTrue();
        CicloRecursalDoProcesso.Item(CronogramaDoProcesso.Do(fases, ComPartesRecusadas("fases[0].produtos")), semEtapas).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "O recurso sem banca de análise não é julgado na fase cuja lista das bancas tem parte recusada, e o item do painel fica pendente")]
    public void RecursoSemBanca_ComParteRecusadaNasBancas_SuspendeEPendenta()
    {
        FaseCronograma[] fases = [FaseComRecursoSemBanca("RESULTADO", 1), FaseComRecursoSemBanca("RECURSO", 2)];
        CronogramaDoProcesso comParteRecusada = CronogramaDoProcesso.Do(fases, ComPartesRecusadas("fases[0].bancasRequeridas"));
        EtapasDoProcesso semEtapas = EtapasDoProcesso.Das([]);

        ConferenciaDasBancas.ConferirRecursosSemBanca(CronogramaDoProcesso.Do(fases), semEtapas).Select(static recusa => recusa.Field).Should()
            .Equal("fases[0].bancasRequeridas", "fases[1].bancasRequeridas");
        ConferenciaDasBancas.ConferirRecursosSemBanca(comParteRecusada, semEtapas).Select(static recusa => recusa.Field).Should()
            .Equal("fases[1].bancasRequeridas");
        ItemDoRecurso(CronogramaDoProcesso.Do([fases[1]]), semEtapas).Ok.Should().BeFalse();
        ItemDoRecurso(CronogramaDoProcesso.Do([SemRecurso()], ComPartesRecusadas("fases[0].bancasRequeridas")), semEtapas).Ok.Should().BeFalse();
        ItemDoRecurso(CronogramaDoProcesso.Do([SemRecurso()]), semEtapas).Ok.Should().BeTrue();
    }

    private static ItemConformidade ItemDoRecurso(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        ConferenciaDasBancas.Itens(cronograma, etapas, CadastrosDeTeste.Padrao.Corretores).Single(static item => item.Codigo == "cronograma_recurso_com_banca_que_julga");

    private static FaseCronograma SemRecurso() => new ModeloDeFase("INSCRICAO", 1).Janela(1, 5).Adotada();

    private static FaseCronograma FaseQueAbreOCiclo(string codigo, int ordem) =>
        new ModeloDeFase(codigo, ordem).Janela(ordem, ordem + 1).Publicando(ModeloDeFase.Preliminar("RESULTADO_PROVA")).Adotada();

    private static FaseCronograma FaseComRecursoSemBanca(string codigo, int ordem)
    {
        ProdutoDaFase preliminar = ModeloDeFase.Preliminar("RESULTADO_PROVA");

        return new ModeloDeFase(codigo, ordem) { Produtos = [preliminar], RegraRecurso = ModeloDeFase.RecursoAncoradoEm(preliminar.Id) }
            .Janela(ordem, ordem + 1).Adotada();
    }
}
