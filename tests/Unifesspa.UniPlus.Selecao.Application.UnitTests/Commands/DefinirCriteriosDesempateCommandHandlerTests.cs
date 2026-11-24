namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
        EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Entrevista", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    private static CriterioDesempateInput[] UmCriterioComCadaRecusaSemCampo() =>
    [
        CriterioSimples(1, "INEXISTENTE"),
        CriterioSimples(2, RegraDeDesempate.MaiorNotaNaEtapa.Codigo()),
        CriterioSimples(3, RegraDeDesempate.Idoso.Codigo()),
        CriterioDesempateDeTeste.DaRegra(RegraDeDesempate.CondicaoSobreDado.Codigo()).NaOrdem(4).ComPredicado("PROFESSOR_RURAL", null, "true").Construir(),
    ];

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        CenarioDosCriteriosDeDesempate cenario = SemProcesso();

        Result result = await cenario.DefinirAsync(Comando(Guid.CreateVersion7()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com DESEMPATE_MAIOR_NOTA_ETAPA resolve parâmetros e persiste")]
    public async Task Handle_MaiorNotaEtapa_Persiste()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ);
        EtapaProcesso etapa = EtapaEntrevista();
        processo.DefinirEtapas([etapa]);
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(RegraDeDesempate.MaiorNotaNaEtapa.Codigo());
        CriterioDesempateInput criterio = CriterioDesempateDeTeste.DaRegra(RegraDeDesempate.MaiorNotaNaEtapa.Codigo()).NaEtapa(etapa.Id).Construir();

        Result result = await cenario.DefinirAsync(Comando(processo.Id, criterio));

        result.IsSuccess.Should().BeTrue();
        processo.CriteriosDesempate.Should().ContainSingle();
        ((ParametrosDesempateMaiorNotaEtapa)processo.CriteriosDesempate.Single().Parametros).EtapaRef.Should().Be(etapa.Id);
        await cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com DESEMPATE_MAIOR_NOTA_ETAPA sem EtapaRef recusa")]
    public async Task Handle_MaiorNotaEtapaSemEtapaRef_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ);
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(RegraDeDesempate.MaiorNotaNaEtapa.Codigo());

        Result result = await cenario.DefinirAsync(Comando(processo.Id, CriterioSimples(1, RegraDeDesempate.MaiorNotaNaEtapa.Codigo())));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CriterioDesempate.EtapaRefObrigatorio");
    }

    [Fact(DisplayName = "Handle com DESEMPATE_IDOSO resolve parâmetros e persiste")]
    public async Task Handle_Idoso_Persiste()
    {
        ProcessoSeletivo processo = NovoProcesso("PSE 2026", TiposDeProcessoDeTeste.PSECampo);
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(RegraDeDesempate.Idoso.Codigo());
        CriterioDesempateInput criterio = CriterioDesempateDeTeste.DaRegra(RegraDeDesempate.Idoso.Codigo()).ComIdadeMinima(60).Construir();

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
            ordem % 2 == 0 ? null! : CriterioDesempateDeTeste.DaRegra(string.Empty).NaVersao(string.Empty).NaOrdem(-ordem).Construir())];

        Result result = await cenario.DefinirAsync(Comando(processo.Id, criterios));

        result.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.CriteriosDesempateEmExcesso");
        await cenario.RegraCatalogoReader.DidNotReceiveWithAnyArgs().ListarPorTipoAsync(default, default);
        await cenario.FatoCandidatoReader.DidNotReceiveWithAnyArgs().ListarAsync(default);
    }

    [Fact(DisplayName = "O catálogo é lido antes de travar o processo para a mutação")]
    public async Task Handle_CatalogoLidoAntesDaTrava()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComCriteriosNoCatalogo(RegraDeDesempate.MaiorIdade.Codigo());

        await cenario.DefinirAsync(Comando(processo.Id, CriterioSimples(1, RegraDeDesempate.MaiorIdade.Codigo())));

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
            .ComRegra(RegraDeDesempate.MaiorNotaNaEtapa.Codigo()).ComRegra(RegraDeDesempate.Idoso.Codigo()).ComRegra(RegraDeDesempate.CondicaoSobreDado.Codigo());

        Result result = await cenario.DefinirAsync(Comando(processo.Id, UmCriterioComCadaRecusaSemCampo()));

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("criterios[0].regra.codigo", "CriterioDesempate.RegraNaoEncontrada"),
            ("criterios[1].parametros.etapaRef", "CriterioDesempate.EtapaRefObrigatorio"),
            ("criterios[2].parametros.idadeMinima", "CriterioDesempate.IdadeMinimaObrigatoria"),
            ("criterios[3].parametros.operador", "CriterioDesempate.PredicadoFatoIncompleto"));
    }

    [Fact(DisplayName = "Ordem duplicada entre um critério recusado e um válido também é acusada")]
    public async Task Handle_OrdemDuplicadaComCriterioRecusado_Acusa()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegraInexistente("INEXISTENTE").ComRegra(RegraDeDesempate.MaiorIdade.Codigo());

        Result result = await cenario.DefinirAsync(
            Comando(processo.Id, CriterioSimples(1, RegraDeDesempate.MaiorIdade.Codigo()), CriterioSimples(1, "INEXISTENTE")));

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("criterios[1].regra.codigo", "CriterioDesempate.RegraNaoEncontrada"),
            ("criterios[1].ordem", "ProcessoSeletivo.OrdemDesempateDuplicada"));
    }

    [Fact(DisplayName = "O catálogo de critérios é lido uma vez, e as regras dele se resolvem sem consulta por critério")]
    public async Task Handle_CatalogoLidoUmaVez_ResolveEmMemoria()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        CenarioDosCriteriosDeDesempate cenario = Com(processo)
            .ComCriteriosNoCatalogo(RegraDeDesempate.MaiorNotaPorAreaDoEnem.Codigo(), RegraDeDesempate.MaiorIdade.Codigo());

        Result result = await cenario.DefinirAsync(Comando(
            processo.Id, CriterioDeAreas(1, "REDACAO"), CriterioSimples(2, RegraDeDesempate.MaiorIdade.Codigo()), CriterioDeAreas(3, "MATEMATICA")));

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
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(RegraDeCalculoDaNota.MediaPonderada.Codigo(), TipoRegra.RegraCalculo);

        Result result = await cenario.DefinirAsync(Comando(processo.Id, CriterioSimples(1, RegraDeCalculoDaNota.MediaPonderada.Codigo())));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CriterioDesempate.RegraTipoInvalido");
    }

    [Fact(DisplayName = "ADR-0125: ordem inválida acumula junto com a regra inexistente, no mesmo item e em outro")]
    public async Task Handle_OrdemInvalidaERegraInexistente_Acumula()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ);
        CenarioDosCriteriosDeDesempate cenario = Com(processo);

        Result result = await cenario.DefinirAsync(Comando(
            processo.Id, CriterioSimples(0, RegraDeDesempate.MaiorIdade.Codigo()), CriterioSimples(-1, "INEXISTENTE"), CriterioSimples(2, "INEXISTENTE")));

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("criterios[0].ordem", "CriterioDesempate.OrdemInvalida"),
            ("criterios[1].ordem", "CriterioDesempate.OrdemInvalida"),
            ("criterios[1].regra.codigo", "CriterioDesempate.RegraNaoEncontrada"),
            ("criterios[2].regra.codigo", "CriterioDesempate.RegraNaoEncontrada"),
        ]);
    }

    [Fact(DisplayName = "ADR-0125: ordem inválida acumula entre critérios, com o índice prefixado ao field")]
    public async Task Handle_DoisCriteriosComOrdemInvalida_AcumulaComIndicePrefixado()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ);
        CenarioDosCriteriosDeDesempate cenario = Com(processo);

        Result result = await cenario.DefinirAsync(Comando(
            processo.Id, CriterioSimples(0, RegraDeDesempate.MaiorIdade.Codigo()), CriterioSimples(-1, RegraDeDesempate.MaiorIdade.Codigo())));

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["criterios[0].ordem", "criterios[1].ordem"]);
        await cenario.RegraCatalogoReader.DidNotReceiveWithAnyArgs().ObterAsync(default!, default!, default);
    }

    [Fact(DisplayName = "ADR-0125: a recusa de CriterioDesempate.Criar carrega o índice do item no campo")]
    public async Task Handle_SegundoCriterioComIdadeMinimaInvalida_PrefixaIndiceNoField()
    {
        ProcessoSeletivo processo = NovoProcesso("PSE 2026", TiposDeProcessoDeTeste.PSECampo);
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(RegraDeDesempate.MaiorIdade.Codigo()).ComRegra(RegraDeDesempate.Idoso.Codigo());
        CriterioDesempateInput idosoSemIdade = CriterioDesempateDeTeste.DaRegra(RegraDeDesempate.Idoso.Codigo()).NaOrdem(2).ComIdadeMinima(0).Construir();

        Result result = await cenario.DefinirAsync(Comando(processo.Id, CriterioSimples(1, RegraDeDesempate.MaiorIdade.Codigo()), idosoSemIdade));

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["criterios[1].parametros.idadeMinima"]);
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

    [Fact(DisplayName = "Critério sem a referência da regra chega ao domínio e é recusado em regra.codigo, com o código da regra não encontrada")]
    public async Task Handle_CriterioSemRegra_RecusaNoCodigoDaRegra()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegraInexistente(string.Empty);
        CriterioDesempateInput semRegra = CriterioDesempateDeTeste.DaRegra(RegraDeDesempate.MaiorIdade.Codigo()).SemRegra().Construir();

        Result result = await cenario.DefinirAsync(Comando(processo.Id, semRegra));

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("criterios[0].regra.codigo", "CriterioDesempate.RegraNaoEncontrada"));
    }
}
