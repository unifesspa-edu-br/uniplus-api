namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDosCriteriosDeDesempate;

public sealed class DefinirCriteriosDesempateCommandHandlerTests
{
    private static EtapaProcesso EtapaEntrevista() =>
        EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Entrevista", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    private static CriterioDesempateInput[] UmCriterioComCadaRecusaSemCampo() =>
    [
        CriterioSimples(1, "INEXISTENTE"),
        CriterioSimples(2, CriterioDesempateCodigo.MaiorNotaEtapa),
        CriterioSimples(3, CriterioDesempateCodigo.Idoso),
        new CriterioDesempateInput(4, CriterioDesempateCodigo.PredicadoFato, "v1", null, null, "PROFESSOR_RURAL", null, "true"),
    ];

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        CenarioDosCriteriosDeDesempate cenario = SemProcesso();

        Result result = await cenario.DefinirAsync(Comando(Guid.CreateVersion7()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com DESEMPATE-MAIOR-NOTA-ETAPA resolve parâmetros e persiste")]
    public async Task Handle_MaiorNotaEtapa_Persiste()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ);
        EtapaProcesso etapa = EtapaEntrevista();
        processo.DefinirEtapas([etapa]);
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(CriterioDesempateCodigo.MaiorNotaEtapa);
        CriterioDesempateInput criterio = new(1, CriterioDesempateCodigo.MaiorNotaEtapa, "v1", etapa.Id, null, null, null, null);

        Result result = await cenario.DefinirAsync(Comando(processo.Id, criterio));

        result.IsSuccess.Should().BeTrue();
        processo.CriteriosDesempate.Should().ContainSingle();
        ((ParametrosDesempateMaiorNotaEtapa)processo.CriteriosDesempate.Single().Parametros).EtapaRef.Should().Be(etapa.Id);
        await cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com DESEMPATE-MAIOR-NOTA-ETAPA sem EtapaRef recusa")]
    public async Task Handle_MaiorNotaEtapaSemEtapaRef_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ);
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(CriterioDesempateCodigo.MaiorNotaEtapa);

        Result result = await cenario.DefinirAsync(Comando(processo.Id, CriterioSimples(1, CriterioDesempateCodigo.MaiorNotaEtapa)));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CriterioDesempate.EtapaRefObrigatorio");
    }

    [Fact(DisplayName = "Handle com DESEMPATE-IDOSO resolve parâmetros e persiste")]
    public async Task Handle_Idoso_Persiste()
    {
        ProcessoSeletivo processo = NovoProcesso("PSE 2026", TiposDeProcessoDeTeste.PSECampo);
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(CriterioDesempateCodigo.Idoso);
        CriterioDesempateInput criterio = new(1, CriterioDesempateCodigo.Idoso, "v1", null, 60, null, null, null);

        Result result = await cenario.DefinirAsync(Comando(processo.Id, criterio));

        result.IsSuccess.Should().BeTrue();
        ((ParametrosDesempateIdoso)processo.CriteriosDesempate.Single().Parametros).IdadeMinima.Should().Be(60);
    }

    [Fact(DisplayName = "Lista vazia remove os critérios sem consultar o catálogo")]
    public async Task Handle_ListaVazia_NaoConsultaOCatalogo()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        CenarioDosCriteriosDeDesempate cenario = Com(processo);

        Result result = await cenario.DefinirAsync(Comando(processo.Id));

        result.IsSuccess.Should().BeTrue();
        await cenario.RegraCatalogoReader.DidNotReceiveWithAnyArgs().ListarPorTipoAsync(default, default);
    }

    [Fact(DisplayName = "Lista acima do teto é recusada pelo agregado com um único erro, sem ler item nem catálogo")]
    public async Task Handle_AcimaDoTeto_RecusaComUmErroSemLerItens()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        CenarioDosCriteriosDeDesempate cenario = Com(processo);
        CriterioDesempateInput[] criterios = [.. Enumerable.Range(1, 10_000).Select(static ordem =>
            ordem % 2 == 0 ? null! : new CriterioDesempateInput(-ordem, string.Empty, string.Empty, null, null, null, null, null))];

        Result result = await cenario.DefinirAsync(Comando(processo.Id, criterios));

        result.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.CriteriosDesempateEmExcesso");
        await cenario.RegraCatalogoReader.DidNotReceiveWithAnyArgs().ListarPorTipoAsync(default, default);
        await cenario.FatoCandidatoReader.DidNotReceiveWithAnyArgs().ListarAsync(default);
    }

    [Fact(DisplayName = "O catálogo é lido antes de travar o processo para a mutação")]
    public async Task Handle_CatalogoLidoAntesDaTrava()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComCriteriosNoCatalogo(CriterioDesempateCodigo.MaiorIdade);

        await cenario.DefinirAsync(Comando(processo.Id, CriterioSimples(1, CriterioDesempateCodigo.MaiorIdade)));

        Received.InOrder(() =>
        {
            _ = cenario.RegraCatalogoReader.ListarPorTipoAsync(TipoRegra.CriterioDesempate, Arg.Any<CancellationToken>());
            _ = cenario.Repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "Recusas que antes saíam sem campo saem no campo do item, em cada critério")]
    public async Task Handle_RecusasSemCampo_SaemNoCampoDoItem()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegraInexistente("INEXISTENTE")
            .ComRegra(CriterioDesempateCodigo.MaiorNotaEtapa).ComRegra(CriterioDesempateCodigo.Idoso).ComRegra(CriterioDesempateCodigo.PredicadoFato);

        Result result = await cenario.DefinirAsync(Comando(processo.Id, UmCriterioComCadaRecusaSemCampo()));

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("criterios[0].regraCodigo", "CriterioDesempate.RegraNaoEncontrada"),
            ("criterios[1].etapaRef", "CriterioDesempate.EtapaRefObrigatorio"),
            ("criterios[2].idadeMinima", "CriterioDesempate.IdadeMinimaObrigatoria"),
            ("criterios[3].operador", "CriterioDesempate.PredicadoFatoIncompleto"));
    }

    [Fact(DisplayName = "Ordem duplicada entre um critério recusado e um válido também é acusada")]
    public async Task Handle_OrdemDuplicadaComCriterioRecusado_Acusa()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegraInexistente("INEXISTENTE").ComRegra(CriterioDesempateCodigo.MaiorIdade);

        Result result = await cenario.DefinirAsync(
            Comando(processo.Id, CriterioSimples(1, CriterioDesempateCodigo.MaiorIdade), CriterioSimples(1, "INEXISTENTE")));

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("criterios[1].regraCodigo", "CriterioDesempate.RegraNaoEncontrada"),
            ("criterios[1].ordem", "ProcessoSeletivo.OrdemDesempateDuplicada"));
    }

    [Fact(DisplayName = "O catálogo de critérios é lido uma vez, e as regras dele se resolvem sem consulta por critério")]
    public async Task Handle_CatalogoLidoUmaVez_ResolveEmMemoria()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        CenarioDosCriteriosDeDesempate cenario = Com(processo)
            .ComCriteriosNoCatalogo(CriterioDesempateCodigo.MaiorNotaAreaEnem, CriterioDesempateCodigo.MaiorIdade);

        Result result = await cenario.DefinirAsync(Comando(
            processo.Id, CriterioDeAreas(1, "REDACAO"), CriterioSimples(2, CriterioDesempateCodigo.MaiorIdade), CriterioDeAreas(3, "MATEMATICA")));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        await cenario.RegraCatalogoReader.Received(1).ListarPorTipoAsync(TipoRegra.CriterioDesempate, Arg.Any<CancellationToken>());
        await cenario.RegraCatalogoReader.DidNotReceiveWithAnyArgs().ObterAsync(default!, default!, default);
    }

    [Fact(DisplayName = "Handle com regra inexistente recusa")]
    public async Task Handle_RegraNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ);
        CenarioDosCriteriosDeDesempate cenario = Com(processo).SemNenhumaRegraNoCatalogo();

        Result result = await cenario.DefinirAsync(Comando(processo.Id, CriterioSimples(1, "INEXISTENTE")));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CriterioDesempate.RegraNaoEncontrada");
    }

    [Fact(DisplayName = "Código fora do catálogo de critérios cai na consulta de reserva: regra de outro tipo recusa")]
    public async Task Handle_RegraTipoInvalido_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ);
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(RegraCalculoCodigo.FormulaMediaPonderada, TipoRegra.RegraCalculo);

        Result result = await cenario.DefinirAsync(Comando(processo.Id, CriterioSimples(1, RegraCalculoCodigo.FormulaMediaPonderada)));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CriterioDesempate.RegraTipoInvalido");
    }

    [Fact(DisplayName = "ADR-0125: ordem inválida acumula junto com a regra inexistente, no mesmo item e em outro")]
    public async Task Handle_OrdemInvalidaERegraInexistente_Acumula()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ);
        CenarioDosCriteriosDeDesempate cenario = Com(processo);

        Result result = await cenario.DefinirAsync(Comando(
            processo.Id, CriterioSimples(0, CriterioDesempateCodigo.MaiorIdade), CriterioSimples(-1, "INEXISTENTE"), CriterioSimples(2, "INEXISTENTE")));

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("criterios[0].ordem", "CriterioDesempate.OrdemInvalida"),
            ("criterios[1].ordem", "CriterioDesempate.OrdemInvalida"),
            ("criterios[1].regraCodigo", "CriterioDesempate.RegraNaoEncontrada"),
            ("criterios[2].regraCodigo", "CriterioDesempate.RegraNaoEncontrada"),
        ]);
    }

    [Fact(DisplayName = "ADR-0125: ordem inválida acumula entre critérios, com o índice prefixado ao field")]
    public async Task Handle_DoisCriteriosComOrdemInvalida_AcumulaComIndicePrefixado()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ);
        CenarioDosCriteriosDeDesempate cenario = Com(processo);

        Result result = await cenario.DefinirAsync(Comando(
            processo.Id, CriterioSimples(0, CriterioDesempateCodigo.MaiorIdade), CriterioSimples(-1, CriterioDesempateCodigo.MaiorIdade)));

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["criterios[0].ordem", "criterios[1].ordem"]);
        await cenario.RegraCatalogoReader.DidNotReceiveWithAnyArgs().ObterAsync(default!, default!, default);
    }

    [Fact(DisplayName = "ADR-0125: a recusa de CriterioDesempate.Criar carrega o índice do item no campo")]
    public async Task Handle_SegundoCriterioComIdadeMinimaInvalida_PrefixaIndiceNoField()
    {
        ProcessoSeletivo processo = NovoProcesso("PSE 2026", TiposDeProcessoDeTeste.PSECampo);
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(CriterioDesempateCodigo.MaiorIdade).ComRegra(CriterioDesempateCodigo.Idoso);
        CriterioDesempateInput idosoSemIdade = new(2, CriterioDesempateCodigo.Idoso, "v1", null, 0, null, null, null);

        Result result = await cenario.DefinirAsync(Comando(processo.Id, CriterioSimples(1, CriterioDesempateCodigo.MaiorIdade), idosoSemIdade));

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["criterios[1].idadeMinima"]);
    }

    [Fact(DisplayName = "Handle com lista vazia remove todos os critérios e persiste")]
    public async Task Handle_ListaVazia_Persiste()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ);
        CenarioDosCriteriosDeDesempate cenario = Com(processo);

        Result result = await cenario.DefinirAsync(Comando(processo.Id));

        result.IsSuccess.Should().BeTrue();
        processo.CriteriosDesempate.Should().BeEmpty();
        await cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
