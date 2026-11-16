namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class SimularDistribuicaoVagasQueryHandlerTests
{
    private static JsonElement Json(string raw)
    {
        using JsonDocument document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    private static RegraCatalogo RegraDistribuicao(string codigo, string esquemaArgs = "{}") => RegraCatalogo.Criar(
        codigo, "v1", TipoRegra.RegraDistribuicaoVagas, Json(esquemaArgs), Json("[]"), "base legal");

    private sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        IRegraCatalogoReader RegraCatalogoReader,
        IOfertaCursoReader OfertaCursoReader,
        IModalidadeReader ModalidadeReader,
        IReferenciaReservaDemograficaReader ReferenciaReservaDemograficaReader)
    {
        public ConfiguracaoDistribuicaoVagasResolver Resolvedor =>
            new(RegraCatalogoReader, OfertaCursoReader, ModalidadeReader, ReferenciaReservaDemograficaReader);
    }

    private static Mocks NovosMocks(bool processoExiste, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ExisteAsync(processoId, Arg.Any<CancellationToken>()).Returns(processoExiste);
        return new Mocks(
            repository,
            Substitute.For<IRegraCatalogoReader>(),
            Substitute.For<IOfertaCursoReader>(),
            Substitute.For<IModalidadeReader>(),
            Substitute.For<IReferenciaReservaDemograficaReader>());
    }

    private static OfertaCursoView NovaOferta(Guid id) => new(
        id, "MEDICINA_MARABA", Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
        "CTIC", "Centro de Tecnologia", "CAMPUS", "REGULAR", "PRESENCIAL", "EXTENSIVO",
        "REGULAR", ["MATUTINO"], null, null, 100, null, null, null, "Medicina", "Bacharelado");

    private static ModalidadeView NovaModalidadeAmpla(Guid id) => new(
        id, "AC", "Ampla concorrência", "AMPLA", "RESTANTE_DAS_VAGAS_OFERTADAS",
        null, null, null, null, null, PredicadoDnf.CriarDeCondicoesAgrupadas([]).Value!, null, "Lei 12.711/2012");

    private static ModalidadeView NovaModalidadeCotaReservada(Guid id, string codigo) => new(
        id, codigo, null, "COTA_RESERVADA", "DENTRO_DA_RESERVA_LEGAL",
        null, "SEGUE_CASCATA", null, null, null, PredicadoDnf.CriarDeCondicoesAgrupadas([]).Value!, null, "Lei 12.711/2012 art. 3º");

    private static readonly IReadOnlyDictionary<string, int> QuadroDaFormulaDoArtigo10 = new Dictionary<string, int>
    {
        [ModalidadesFederaisLei12711.LbPpi] = 5,
        [ModalidadesFederaisLei12711.LbQ] = 1,
        [ModalidadesFederaisLei12711.LbPcd] = 1,
        [ModalidadesFederaisLei12711.LbEp] = 3,
        [ModalidadesFederaisLei12711.LiPpi] = 5,
        [ModalidadesFederaisLei12711.LiQ] = 1,
        [ModalidadesFederaisLei12711.LiPcd] = 1,
        [ModalidadesFederaisLei12711.LiEp] = 3,
        [ModalidadesFederaisLei12711.Ac] = 20,
    };

    private static Task<Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>> SimularComCatalogoInstitucionalAsync(
        Mocks mocks, Guid processoId, int vagas)
    {
        Guid ofertaCursoId = Guid.CreateVersion7();
        Guid modalidadeId = Guid.CreateVersion7();
        ConfigurarCatalogoInstitucional(mocks, ofertaCursoId, modalidadeId);
        SimularDistribuicaoVagasQuery query = new(
            processoId,
            [new ConfiguracaoDistribuicaoVagasInput(
                ofertaCursoId, vagas, 1m, RegraDistribuicaoVagasCodigo.Institucional, "v1", null, null, null,
                [modalidadeId], [new QuantidadeVagaInput(modalidadeId, vagas)])]);

        return SimularDistribuicaoVagasQueryHandler.Handle(query, mocks.Repository, mocks.Resolvedor, CancellationToken.None);
    }

    private static void ConfigurarCatalogoInstitucional(Mocks mocks, Guid ofertaCursoId, Guid modalidadeId)
    {
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId));
        mocks.RegraCatalogoReader.ObterAsync(RegraDistribuicaoVagasCodigo.Institucional, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDistribuicaoVagasCodigo.Institucional));
        mocks.ModalidadeReader.ObterPorIdAsync(modalidadeId, Arg.Any<CancellationToken>()).Returns(NovaModalidadeAmpla(modalidadeId));
    }

    private static Task<Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>> SimularComCatalogoDaLei12711Async(
        Mocks mocks, Guid processoId, int vagasBase)
    {
        Guid ofertaCursoId = Guid.CreateVersion7();
        Guid referenciaId = Guid.CreateVersion7();
        Guid[] modalidadeIds = ConfigurarModalidadesFederaisMaisAmpla(mocks);
        ConfigurarRegrasDaLei12711(mocks, ofertaCursoId, referenciaId);
        SimularDistribuicaoVagasQuery query = new(
            processoId,
            [new ConfiguracaoDistribuicaoVagasInput(
                ofertaCursoId, vagasBase, 0.5m, RegraDistribuicaoVagasCodigo.Lei12711, "v1",
                RegraAjusteDistribuicaoVagasCodigo.ReconciliacaoArt11ParagrafoUnico, "v1", referenciaId,
                modalidadeIds, [])]);

        return SimularDistribuicaoVagasQueryHandler.Handle(query, mocks.Repository, mocks.Resolvedor, CancellationToken.None);
    }

    private static void ConfigurarRegrasDaLei12711(Mocks mocks, Guid ofertaCursoId, Guid referenciaId)
    {
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId));
        mocks.RegraCatalogoReader.ObterAsync(RegraDistribuicaoVagasCodigo.Lei12711, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDistribuicaoVagasCodigo.Lei12711));
        mocks.RegraCatalogoReader.ObterAsync(RegraAjusteDistribuicaoVagasCodigo.ReconciliacaoArt11ParagrafoUnico, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCatalogo.Criar(
                RegraAjusteDistribuicaoVagasCodigo.ReconciliacaoArt11ParagrafoUnico, "v1", TipoRegra.RegraAjusteDistribuicaoVagas, Json("{}"), Json("[]"), "art. 11 §único"));
        mocks.ReferenciaReservaDemograficaReader.ObterPorIdAsync(referenciaId, Arg.Any<CancellationToken>())
            .Returns(new ReferenciaReservaDemograficaView(referenciaId, "2022", 50m, 10m, 10m, "Censo 2022"));
    }

    private static Guid[] ConfigurarModalidadesFederaisMaisAmpla(Mocks mocks)
    {
        (string Codigo, Guid Id)[] federaisMaisAc =
        [
            .. ModalidadesFederaisLei12711.Codigos.Select(codigo => (codigo, Guid.CreateVersion7())),
            (ModalidadesFederaisLei12711.Ac, Guid.CreateVersion7()),
        ];
        foreach ((string codigo, Guid id) in federaisMaisAc)
        {
            mocks.ModalidadeReader.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns(NovaModalidadeDaLei12711(codigo, id));
        }

        return [.. federaisMaisAc.Select(f => f.Id)];
    }

    private static ModalidadeView NovaModalidadeDaLei12711(string codigo, Guid id) =>
        codigo == ModalidadesFederaisLei12711.Ac ? NovaModalidadeAmpla(id) : NovaModalidadeCotaReservada(id, codigo);

    // PPI 50%, Quilombola 10% e PcD 10% com VO=40 e PR=0,5 dão VR=20, sem retiradas nem suplementos.
    private static void ConfirmarQuadroDeQuarentaVagasPelaFormulaDoArtigo10(ConfiguracaoDistribuicaoVagasDto dto)
    {
        dto.Quadro.ToDictionary(v => v.ModalidadeCodigo, v => v.Quantidade).Should().Contain(QuadroDaFormulaDoArtigo10);
        dto.RamoDaDistribuicao.Should().Be(RamoDaDistribuicaoDeVagasCodigo.LeiDeCotas);
        dto.ConferenciaDaReservaLegal.Should().Be(new ConferenciaDaReservaLegalDto(
            ReservaLegalNominal: 20, ReservaLegalEfetiva: 20, ExcedenteSobreAReservaLegal: 0, VagasCortadasPorEscassez: 0));
        dto.AjusteDoQuadroFixado.Should().BeNull();
        dto.TotalDeVagasDoEdital.Should().Be(40);
    }

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado sem consultar readers")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Guid processoId = Guid.CreateVersion7();
        // NovosMocks stuba ExisteAsync SÓ para `processoId` — se o handler chamasse
        // ExisteAsync com outro Guid (ou Guid.Empty), o Substitute cairia no default
        // (false) por acaso, mascarando o bug; usar o MESMO Guid nos dois lados prova
        // que o handler de fato repassa query.ProcessoSeletivoId ao repositório.
        Mocks mocks = NovosMocks(processoExiste: false, processoId);
        SimularDistribuicaoVagasQuery query = new(
            processoId,
            [new ConfiguracaoDistribuicaoVagasInput(Guid.CreateVersion7(), 50, 1m, "X", "v1", null, null, null, [Guid.CreateVersion7()], [])]);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await SimularDistribuicaoVagasQueryHandler.Handle(
            query, mocks.Repository, mocks.Resolvedor, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await mocks.Repository.Received(1).ExisteAsync(processoId, Arg.Any<CancellationToken>());
        await mocks.OfertaCursoReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await mocks.RegraCatalogoReader.DidNotReceiveWithAnyArgs().ObterAsync(default!, default!, default);
    }

    [Fact(DisplayName = "Simulação recusa modalidade fora do rol declarado no esquema da regra")]
    public async Task Handle_ModalidadeForaDoRolDaRegra_Recusa()
    {
        // Prova a ligação entre catálogo e agregado: o rol vive no esquema_args da regra, e
        // é o resolvedor que o extrai. Sem esta cobertura, a recusa poderia estar correta no
        // domínio e nunca ser alcançada, porque ninguém teria passado o rol adiante.
        Guid processoId = Guid.CreateVersion7();
        Guid ofertaCursoId = Guid.CreateVersion7();
        Guid modalidadeId = Guid.CreateVersion7();

        Mocks mocks = NovosMocks(processoExiste: true, processoId);
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId));
        mocks.RegraCatalogoReader.ObterAsync(RegraDistribuicaoVagasCodigo.Psiq, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDistribuicaoVagasCodigo.Psiq, """{"modalidades_admitidas":["AC_I","AC_Q"]}"""));
        mocks.ModalidadeReader.ObterPorIdAsync(modalidadeId, Arg.Any<CancellationToken>()).Returns(NovaModalidadeAmpla(modalidadeId));

        SimularDistribuicaoVagasQuery query = new(
            processoId,
            [new ConfiguracaoDistribuicaoVagasInput(
                ofertaCursoId, 10, 1m, RegraDistribuicaoVagasCodigo.Psiq, "v1", null, null, null,
                [modalidadeId], [new QuantidadeVagaInput(modalidadeId, 10)])]);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await SimularDistribuicaoVagasQueryHandler.Handle(
            query, mocks.Repository, mocks.Resolvedor, CancellationToken.None);

        result.IsFailure.Should().BeTrue("AC não está entre as modalidades que o PSIQ admite");
        result.Errors.Should().Contain(e => e.Error.Code == "ConfiguracaoDistribuicaoVagas.ModalidadeNaoAdmitidaPelaRegra");
    }

    [Fact(DisplayName = "Simulação recusa VO_base acima das vagas anuais autorizadas da oferta")]
    public async Task Handle_VoBaseAcimaDoTetoDaOferta_Recusa()
    {
        Guid processoId = Guid.CreateVersion7();
        Guid ofertaCursoId = Guid.CreateVersion7();
        Guid modalidadeId = Guid.CreateVersion7();

        Mocks mocks = NovosMocks(processoExiste: true, processoId);
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId));
        mocks.RegraCatalogoReader.ObterAsync(RegraDistribuicaoVagasCodigo.Institucional, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDistribuicaoVagasCodigo.Institucional));
        mocks.ModalidadeReader.ObterPorIdAsync(modalidadeId, Arg.Any<CancellationToken>()).Returns(NovaModalidadeAmpla(modalidadeId));

        SimularDistribuicaoVagasQuery query = new(
            processoId,
            [new ConfiguracaoDistribuicaoVagasInput(
                ofertaCursoId, 99999, 1m, RegraDistribuicaoVagasCodigo.Institucional, "v1", null, null, null,
                [modalidadeId], [new QuantidadeVagaInput(modalidadeId, 99999)])]);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await SimularDistribuicaoVagasQueryHandler.Handle(
            query, mocks.Repository, mocks.Resolvedor, CancellationToken.None);

        result.IsFailure.Should().BeTrue(
            "a simulação percorre o mesmo resolvedor da escrita — quem chama a API direto não pode contornar o teto");
        result.Errors.Should().Contain(e => e.Error.Code == "ConfiguracaoDistribuicaoVagas.VagasOfertadasBaseAcimaDasVagasAutorizadas");
    }

    [Fact(DisplayName = "Handle institucional devolve o quadro igual ao declarado, sem persistir")]
    public async Task Handle_Institucional_DevolveQuadroIgualAoDeclarado()
    {
        Guid processoId = Guid.CreateVersion7();
        Mocks mocks = NovosMocks(processoExiste: true, processoId);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result =
            await SimularComCatalogoInstitucionalAsync(mocks, processoId, vagas: 60);

        result.IsSuccess.Should().BeTrue();
        ConfiguracaoDistribuicaoVagasDto dto = result.Value!.Should().ContainSingle().Subject;
        dto.RamoDaDistribuicao.Should().Be(RamoDaDistribuicaoDeVagasCodigo.QuadroFixadoNoEdital);
        dto.VagasOfertadasBase.Should().Be(60);
        dto.Quadro.Should().ContainSingle(v => v.Quantidade == 60);
        dto.TotalDeVagasDoEdital.Should().Be(60);
        await mocks.Repository.DidNotReceiveWithAnyArgs().ObterParaMutacaoAsync(default, default);
    }

    [Fact(DisplayName = "Handle Lei 12.711 devolve o quadro calculado pela fórmula do art. 10, sem persistir")]
    public async Task Handle_Lei12711_DevolveQuadroCalculado()
    {
        Guid processoId = Guid.CreateVersion7();
        Mocks mocks = NovosMocks(processoExiste: true, processoId);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result =
            await SimularComCatalogoDaLei12711Async(mocks, processoId, vagasBase: 40);

        result.IsSuccess.Should().BeTrue();
        ConfirmarQuadroDeQuarentaVagasPelaFormulaDoArtigo10(result.Value!.Should().ContainSingle().Subject);

        // O preview não toca no agregado nem no unit of work: a resolução cross-módulo
        // e o cálculo do quadro rodam inteiramente em memória.
        await mocks.Repository.DidNotReceiveWithAnyArgs().ObterParaMutacaoAsync(default, default);
    }

    [Fact(DisplayName = "Handle com regra de distribuição inexistente recusa")]
    public async Task Handle_RegraNaoEncontrada_Recusa()
    {
        Guid processoId = Guid.CreateVersion7();
        Mocks mocks = NovosMocks(processoExiste: true, processoId);
        mocks.OfertaCursoReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(NovaOferta(Guid.CreateVersion7()));
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RegraCatalogo?)null);

        SimularDistribuicaoVagasQuery query = new(
            processoId,
            [new ConfiguracaoDistribuicaoVagasInput(Guid.CreateVersion7(), 50, 1m, "INEXISTENTE", "v1", null, null, null, [Guid.CreateVersion7()], [])]);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await SimularDistribuicaoVagasQueryHandler.Handle(
            query, mocks.Repository, mocks.Resolvedor, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.RegraDistribuicaoNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com modalidade duplicada traduz o field do domínio (modalidades) para o do payload (modalidadeIds)")]
    public async Task Handle_ModalidadeDuplicada_TraduzFieldDoDominioParaODoPayload()
    {
        Guid processoId = Guid.CreateVersion7();
        Guid ofertaCursoId = Guid.CreateVersion7();
        Guid modalidadeId1 = Guid.CreateVersion7();
        Guid modalidadeId2 = Guid.CreateVersion7();

        Mocks mocks = NovosMocks(processoExiste: true, processoId);
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId));
        mocks.RegraCatalogoReader.ObterAsync(RegraDistribuicaoVagasCodigo.Institucional, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDistribuicaoVagasCodigo.Institucional));
        mocks.ModalidadeReader.ObterPorIdAsync(modalidadeId1, Arg.Any<CancellationToken>()).Returns(NovaModalidadeCotaReservada(modalidadeId1, "IND"));
        mocks.ModalidadeReader.ObterPorIdAsync(modalidadeId2, Arg.Any<CancellationToken>()).Returns(NovaModalidadeCotaReservada(modalidadeId2, "IND"));

        SimularDistribuicaoVagasQuery query = new(
            processoId,
            [new ConfiguracaoDistribuicaoVagasInput(
                ofertaCursoId, 60, 1m, RegraDistribuicaoVagasCodigo.Institucional, "v1", null, null, null,
                [modalidadeId1, modalidadeId2], [])]);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await SimularDistribuicaoVagasQueryHandler.Handle(
            query, mocks.Repository, mocks.Resolvedor, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == "ConfiguracaoDistribuicaoVagas.ModalidadeDuplicada");
        result.Errors.Should().OnlyContain(e => e.Field == "distribuicaoVagas[0].modalidadeIds");
    }

    [Fact(DisplayName = "ADR-0125: ModalidadeIds vazio recusa com ModalidadesVazias na 1ª passada, mesmo com oferta/regra inexistentes")]
    public async Task Handle_ModalidadeIdsVazioComOfertaInexistente_RecusaComModalidadesVaziasSemConsultarReaders()
    {
        Guid processoId = Guid.CreateVersion7();
        Mocks mocks = NovosMocks(processoExiste: true, processoId);

        SimularDistribuicaoVagasQuery query = new(
            processoId,
            [new ConfiguracaoDistribuicaoVagasInput(Guid.CreateVersion7(), 50, 1m, "X", "v1", null, null, null, [], [])]);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await SimularDistribuicaoVagasQueryHandler.Handle(
            query, mocks.Repository, mocks.Resolvedor, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(["ConfiguracaoDistribuicaoVagas.ModalidadesVazias"]);
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["distribuicaoVagas[0].modalidadeIds"]);
        await mocks.OfertaCursoReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await mocks.RegraCatalogoReader.DidNotReceiveWithAnyArgs().ObterAsync(default!, default!, default);
    }
}
