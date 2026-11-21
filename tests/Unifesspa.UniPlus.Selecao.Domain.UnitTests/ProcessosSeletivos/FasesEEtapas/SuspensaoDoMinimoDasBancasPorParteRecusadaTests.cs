namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.CenarioDeProcesso;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;

/// <summary>
/// A etapa cujas bancas, recursos ou bancas da fase tiveram parte recusada na leitura tem julgadora indeterminada: a banca de
/// recursos da fase não a julga para o mínimo de integrantes, e o item do painel do mínimo fica pendente em vez de aprovado por
/// omissão. A etapa de julgadora determinada segue conferida.
/// </summary>
public sealed class SuspensaoDoMinimoDasBancasPorParteRecusadaTests
{
    private const string ItemDoMinimo = "cronograma_banca_com_o_minimo_de_integrantes";

    private static readonly ReferenciaRegra AvaliacaoUnica =
        ReferenciaRegra.De(RegraConsolidacaoCodigo.AvaliacaoUnica, "v1", new string('a', 64));

    private static readonly CorretoresDasEtapas TresCorretores =
        new(new Dictionary<ReferenciaRegra, CorretoresDaRegra> { [AvaliacaoUnica] = CorretoresDaRegra.SemNovoCorretor(3) });

    private static FaseCronograma FaseComBancaDeRecursos(string codigo, int ordem, int integrantes = 2) =>
        (new ModeloDeFase(codigo, ordem) with
        {
            Bancas = [BancaRequerida.Criar(TiposDeBancaCopiadosDeTeste.DeRecursos(Guid.CreateVersion7()), [], BancasDeTeste.Regra(integrantes: integrantes, quorum: 2))],
        }).Adotada();

    private static EtapaProcesso EtapaComRecurso(string nome, int ordem, string codigoDaFase) =>
        (new ModeloDeEtapa(nome) with
        {
            Ordem = ordem,
            FaseCodigo = codigoDaFase,
            Forma = FormaDeAvaliacao.Humana,
            RegraConsolidacao = AvaliacaoUnica,
            Passo = 0.5m,
            EmiteParecerIndividual = true,
            Recursos = [RecursoModelo.NaCiencia()],
        }).Adotada();

    private static IReadOnlyList<FieldError> Minimo(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        ConferenciaDasBancas.ConferirMinimoDeIntegrantes(cronograma, etapas, TresCorretores);

    private static ItemConformidade ItemDoMinimoNoPainel(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        ConferenciaDasBancas.Itens(OrigemCandidatos.InscricaoPropria, cronograma, etapas, TresCorretores).Single(static item => item.Codigo == ItemDoMinimo);

    [Theory(DisplayName = "A banca de recursos da fase não julga a etapa cuja lista das bancas ou dos recursos tem parte recusada: não recusa o mínimo, e o item fica pendente")]
    [InlineData("etapas[0].bancas")]
    [InlineData("etapas[0].recursos")]
    public void EtapaComListaRecusada_NaoEAtribuidaABancaDaFase(string colecaoRecusada)
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([FaseComBancaDeRecursos("AVALIACAO", 1)]);
        EtapaProcesso[] etapas = [EtapaComRecurso("Redação", 1, "AVALIACAO")];

        Minimo(cronograma, EtapasDoProcesso.Das(etapas)).Should().ContainSingle();
        EtapasDoProcesso comParteRecusada = EtapasDoProcesso.Das(etapas, ComPartesRecusadas(colecaoRecusada));

        Minimo(cronograma, comParteRecusada).Should().BeEmpty();
        ItemDoMinimoNoPainel(cronograma, comParteRecusada).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "A etapa de uma fase cuja lista das bancas tem parte recusada não é atribuída à banca de recursos da fase, e o item fica pendente")]
    public void FaseComListaDasBancasRecusada_NaoAtribuiEtapaABancaDaFase()
    {
        FaseCronograma fase = FaseComBancaDeRecursos("AVALIACAO", 1);
        EtapasDoProcesso etapas = EtapasDoProcesso.Das([EtapaComRecurso("Redação", 1, "AVALIACAO")]);
        CronogramaDoProcesso comParteRecusada = CronogramaDoProcesso.Do([fase], ComPartesRecusadas("fases[0].bancasRequeridas"));

        Minimo(comParteRecusada, etapas).Should().BeEmpty();
        ItemDoMinimoNoPainel(comParteRecusada, etapas).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "A etapa de julgadora determinada na mesma fase segue julgada pela banca de recursos da fase, e o item continua pendente")]
    public void EtapaDeterminadaNaMesmaFase_SegueConferida()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([FaseComBancaDeRecursos("AVALIACAO", 1)]);
        EtapaProcesso[] etapas = [EtapaComRecurso("Redação", 1, "AVALIACAO"), EtapaComRecurso("Entrevista", 2, "AVALIACAO")];
        EtapasDoProcesso comParteRecusada = EtapasDoProcesso.Das(etapas, ComPartesRecusadas("etapas[0].bancas"));

        Causas(Minimo(cronograma, comParteRecusada)).Should().Equal(("fases[0].bancasRequeridas[0]", "Banca.IntegrantesAbaixoDoMinimo"));
        ItemDoMinimoNoPainel(cronograma, comParteRecusada).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "A etapa independente, em outra fase, sem parte recusada tem o mínimo da banca da fase dela conferido e recusado")]
    public void EtapaIndependenteEmOutraFase_SegueConferidaERecusada()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([FaseComBancaDeRecursos("AVALIACAO", 1), FaseComBancaDeRecursos("ENTREVISTA", 2)]);
        EtapaProcesso[] etapas = [EtapaComRecurso("Redação", 1, "AVALIACAO"), EtapaComRecurso("Entrevista", 2, "ENTREVISTA")];
        EtapasDoProcesso comParteRecusada = EtapasDoProcesso.Das(etapas, ComPartesRecusadas("etapas[0].recursos"));

        Causas(Minimo(cronograma, comParteRecusada)).Should().Equal(("fases[1].bancasRequeridas[0]", "Banca.IntegrantesAbaixoDoMinimo"));
        ItemDoMinimoNoPainel(cronograma, comParteRecusada).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "A parte recusada em fase sem etapa não deixa pendente o mínimo satisfeito das etapas de outra fase")]
    public void FaseSemEtapaComParteRecusada_NaoSuspendeOMinimoDeOutraFase()
    {
        FaseCronograma semEtapa = FaseComBancaDeRecursos("ENTREVISTA", 2);
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(
            [FaseComBancaDeRecursos("AVALIACAO", 1, integrantes: 3), semEtapa], ComPartesRecusadas("fases[1].bancasRequeridas"));
        EtapasDoProcesso etapas = EtapasDoProcesso.Das([EtapaComRecurso("Redação", 1, "AVALIACAO")]);

        Minimo(cronograma, etapas).Should().BeEmpty();
        ItemDoMinimoNoPainel(cronograma, etapas).Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "Sem parte recusada o item do mínimo segue a recusa do mínimo: pendente com a etapa abaixo do mínimo, aprovado sem etapa julgada")]
    public void SemParteRecusada_ItemSegueARecusaDoMinimo()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([FaseComBancaDeRecursos("AVALIACAO", 1)]);
        EtapasDoProcesso etapas = EtapasDoProcesso.Das([EtapaComRecurso("Redação", 1, "AVALIACAO")]);

        ItemDoMinimoNoPainel(cronograma, etapas).Ok.Should().BeFalse();
        ItemDoMinimoNoPainel(cronograma, EtapasDoProcesso.Das([])).Ok.Should().BeTrue();
    }
}
