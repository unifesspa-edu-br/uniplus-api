namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
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

    private static RegraCatalogo RegraDistribuicao(string codigo) => RegraCatalogo.Criar(
        codigo, "v1", TipoRegra.RegraDistribuicaoVagas, Json("{}"), Json("[]"), "base legal").Value!;

    private sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        IRegraCatalogoReader RegraCatalogoReader,
        IOfertaCursoReader OfertaCursoReader,
        IModalidadeReader ModalidadeReader,
        IReferenciaReservaDemograficaReader ReferenciaReservaDemograficaReader);

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
        id, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
        "CTIC", "Centro de Tecnologia", "CAMPUS", "REGULAR", "PRESENCIAL", "EXTENSIVO",
        "REGULAR", ["MATUTINO"], null, null, 100, null, null);

    private static ModalidadeView NovaModalidadeAmpla(Guid id) => new(
        id, "AC", "Ampla concorrência", "AMPLA", "RESIDUAL_DO_VO",
        null, null, null, null, null, [], null, "Lei 12.711/2012");

    private static ModalidadeView NovaModalidadeCotaReservada(Guid id, string codigo) => new(
        id, codigo, null, "COTA_RESERVADA", "DENTRO_DO_VR",
        null, "SEGUE_CASCATA", null, null, null, [], null, "Lei 12.711/2012 art. 3º");

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
            query, mocks.Repository, mocks.RegraCatalogoReader, mocks.OfertaCursoReader, mocks.ModalidadeReader,
            mocks.ReferenciaReservaDemograficaReader, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await mocks.Repository.Received(1).ExisteAsync(processoId, Arg.Any<CancellationToken>());
        await mocks.OfertaCursoReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await mocks.RegraCatalogoReader.DidNotReceiveWithAnyArgs().ObterAsync(default!, default!, default);
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
            query, mocks.Repository, mocks.RegraCatalogoReader, mocks.OfertaCursoReader, mocks.ModalidadeReader,
            mocks.ReferenciaReservaDemograficaReader, CancellationToken.None);

        result.IsFailure.Should().BeTrue(
            "a simulação percorre o mesmo resolvedor da escrita — quem chama a API direto não pode contornar o teto");
        result.Errors.Should().Contain(e => e.Error.Code == "ConfiguracaoDistribuicaoVagas.VoBaseAcimaDasVagasAutorizadas");
    }

    [Fact(DisplayName = "Handle institucional devolve o quadro igual ao declarado, sem persistir")]
    public async Task Handle_Institucional_DevolveQuadroIgualAoDeclarado()
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
                ofertaCursoId, 60, 1m, RegraDistribuicaoVagasCodigo.Institucional, "v1", null, null, null,
                [modalidadeId], [new QuantidadeVagaInput(modalidadeId, 60)])]);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await SimularDistribuicaoVagasQueryHandler.Handle(
            query, mocks.Repository, mocks.RegraCatalogoReader, mocks.OfertaCursoReader, mocks.ModalidadeReader,
            mocks.ReferenciaReservaDemograficaReader, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ConfiguracaoDistribuicaoVagasDto dto = result.Value!.Should().ContainSingle().Subject;
        dto.VoBase.Should().Be(60);
        dto.Quadro.Should().ContainSingle(v => v.Quantidade == 60);
        dto.TotalPublicado.Should().Be(60);
        await mocks.Repository.DidNotReceiveWithAnyArgs().ObterParaMutacaoAsync(default, default);
    }

    [Fact(DisplayName = "Handle Lei 12.711 devolve o quadro calculado pela fórmula do art. 10, sem persistir")]
    public async Task Handle_Lei12711_DevolveQuadroCalculado()
    {
        Guid processoId = Guid.CreateVersion7();
        Guid ofertaCursoId = Guid.CreateVersion7();
        Guid referenciaId = Guid.CreateVersion7();

        (string Codigo, Guid Id)[] federaisMaisAc =
        [
            .. ModalidadesFederaisLei12711.Codigos.Select(codigo => (codigo, Guid.CreateVersion7())),
            (ModalidadesFederaisLei12711.Ac, Guid.CreateVersion7()),
        ];

        Mocks mocks = NovosMocks(processoExiste: true, processoId);
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId));
        mocks.RegraCatalogoReader.ObterAsync(RegraDistribuicaoVagasCodigo.Lei12711, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDistribuicaoVagasCodigo.Lei12711));
        mocks.RegraCatalogoReader.ObterAsync("RECONCILIACAO-VAGAS-ART11-PU", "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCatalogo.Criar(
                "RECONCILIACAO-VAGAS-ART11-PU", "v1", TipoRegra.RegraAjusteDistribuicaoVagas, Json("{}"), Json("[]"), "art. 11 §único").Value!);
        // PPI 50%, Quilombola 10%, PcD 10% — mesmos percentuais do caso "sem retiradas
        // nem suplementos" documentado na issue #1282 (VO=40, PR=0,5 -> VR=20).
        mocks.ReferenciaReservaDemograficaReader.ObterPorIdAsync(referenciaId, Arg.Any<CancellationToken>())
            .Returns(new ReferenciaReservaDemograficaView(referenciaId, "2022", 50m, 10m, 10m, "Censo 2022"));
        foreach ((string codigo, Guid id) in federaisMaisAc)
        {
            ModalidadeView view = codigo == ModalidadesFederaisLei12711.Ac
                ? NovaModalidadeAmpla(id)
                : NovaModalidadeCotaReservada(id, codigo);
            mocks.ModalidadeReader.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns(view);
        }

        SimularDistribuicaoVagasQuery query = new(
            processoId,
            [new ConfiguracaoDistribuicaoVagasInput(
                ofertaCursoId, 40, 0.5m, RegraDistribuicaoVagasCodigo.Lei12711, "v1",
                "RECONCILIACAO-VAGAS-ART11-PU", "v1", referenciaId,
                [.. federaisMaisAc.Select(f => f.Id)], [])]);

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> result = await SimularDistribuicaoVagasQueryHandler.Handle(
            query, mocks.Repository, mocks.RegraCatalogoReader, mocks.OfertaCursoReader, mocks.ModalidadeReader,
            mocks.ReferenciaReservaDemograficaReader, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ConfiguracaoDistribuicaoVagasDto dto = result.Value!.Should().ContainSingle().Subject;

        Dictionary<string, int> quadroPorCodigo = dto.Quadro.ToDictionary(v => v.ModalidadeCodigo, v => v.Quantidade);
        quadroPorCodigo[ModalidadesFederaisLei12711.LbPpi].Should().Be(5);
        quadroPorCodigo[ModalidadesFederaisLei12711.LbQ].Should().Be(1);
        quadroPorCodigo[ModalidadesFederaisLei12711.LbPcd].Should().Be(1);
        quadroPorCodigo[ModalidadesFederaisLei12711.LbEp].Should().Be(3);
        quadroPorCodigo[ModalidadesFederaisLei12711.LiPpi].Should().Be(5);
        quadroPorCodigo[ModalidadesFederaisLei12711.LiQ].Should().Be(1);
        quadroPorCodigo[ModalidadesFederaisLei12711.LiPcd].Should().Be(1);
        quadroPorCodigo[ModalidadesFederaisLei12711.LiEp].Should().Be(3);
        quadroPorCodigo[ModalidadesFederaisLei12711.Ac].Should().Be(20);

        dto.VrNominal.Should().Be(20);
        dto.VrFinal.Should().Be(20);
        dto.Estouro.Should().Be(0);
        dto.CapadoEmVo.Should().BeFalse();
        dto.TotalPublicado.Should().Be(40);

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
            query, mocks.Repository, mocks.RegraCatalogoReader, mocks.OfertaCursoReader, mocks.ModalidadeReader,
            mocks.ReferenciaReservaDemograficaReader, CancellationToken.None);

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
            query, mocks.Repository, mocks.RegraCatalogoReader, mocks.OfertaCursoReader, mocks.ModalidadeReader,
            mocks.ReferenciaReservaDemograficaReader, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["distribuicaoVagas[0].modalidadeIds"]);
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
            query, mocks.Repository, mocks.RegraCatalogoReader, mocks.OfertaCursoReader, mocks.ModalidadeReader,
            mocks.ReferenciaReservaDemograficaReader, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(["ConfiguracaoDistribuicaoVagas.ModalidadesVazias"]);
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["distribuicaoVagas[0].modalidadeIds"]);
        await mocks.OfertaCursoReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await mocks.RegraCatalogoReader.DidNotReceiveWithAnyArgs().ObterAsync(default!, default!, default);
    }
}
