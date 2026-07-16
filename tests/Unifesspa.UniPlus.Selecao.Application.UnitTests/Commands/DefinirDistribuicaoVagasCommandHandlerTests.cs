namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirDistribuicaoVagasCommandHandlerTests
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
        IReferenciaReservaDemograficaReader ReferenciaReservaDemograficaReader,
        ISelecaoUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);
        return new Mocks(
            repository,
            Substitute.For<IRegraCatalogoReader>(),
            Substitute.For<IOfertaCursoReader>(),
            Substitute.For<IModalidadeReader>(),
            Substitute.For<IReferenciaReservaDemograficaReader>(),
            Substitute.For<ISelecaoUnitOfWork>());
    }

    private static OfertaCursoView NovaOferta(Guid id) => new(
        id, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
        "CTIC", "Centro de Tecnologia", "CAMPUS", "REGULAR", "PRESENCIAL", "MATUTINO",
        null, null, 50, null, null);

    private static ModalidadeView NovaModalidadeAmpla(Guid id) => new(
        id, "AC", "Ampla concorrência", "AMPLA", "RESIDUAL_DO_VO",
        null, null, null, null, null, [], null, "Lei 12.711/2012");

    private static ModalidadeView NovaModalidadeCotaReservada(Guid id, string codigo) => new(
        id, codigo, null, "COTA_RESERVADA", "DENTRO_DO_VR",
        null, "SEGUE_CASCATA", null, null, null, [], null, "Lei 12.711/2012 art. 3º");

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7());
        DefinirDistribuicaoVagasCommand command = new(
            Guid.CreateVersion7(),
            [new ConfiguracaoDistribuicaoVagasInput(Guid.CreateVersion7(), 50, 1m, "X", "v1", null, [Guid.CreateVersion7()])], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.OfertaCursoReader, mocks.ModalidadeReader,
            mocks.ReferenciaReservaDemograficaReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle institucional (sem referência demográfica) persiste")]
    public async Task Handle_Institucional_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria);
        Guid ofertaCursoId = Guid.CreateVersion7();
        Guid modalidadeId = Guid.CreateVersion7();

        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId));
        mocks.RegraCatalogoReader.ObterAsync(RegraDistribuicaoVagasCodigo.Institucional, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDistribuicaoVagasCodigo.Institucional));
        mocks.ModalidadeReader.ObterPorIdAsync(modalidadeId, Arg.Any<CancellationToken>()).Returns(NovaModalidadeAmpla(modalidadeId));

        DefinirDistribuicaoVagasCommand command = new(
            processo.Id,
            [new ConfiguracaoDistribuicaoVagasInput(
                ofertaCursoId, 60, 1m, RegraDistribuicaoVagasCodigo.Institucional, "v1", null, [modalidadeId])], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.OfertaCursoReader, mocks.ModalidadeReader,
            mocks.ReferenciaReservaDemograficaReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.DistribuicaoVagas.Should().ContainSingle();
        processo.DistribuicaoVagas.Single().VoBase.Should().Be(60);
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle Lei 12.711 resolve a referência demográfica e persiste")]
    public async Task Handle_Lei12711_ResolveReferenciaDemografica_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("SiSU 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);
        Guid ofertaCursoId = Guid.CreateVersion7();
        Guid referenciaId = Guid.CreateVersion7();

        // INV-6: a Lei 12.711 exige as 8 modalidades federais + AC.
        (string Codigo, Guid Id)[] federaisMaisAc =
        [
            .. ModalidadesFederaisLei12711.Codigos.Select(codigo => (codigo, Guid.CreateVersion7())),
            (ModalidadesFederaisLei12711.Ac, Guid.CreateVersion7()),
        ];

        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId));
        mocks.RegraCatalogoReader.ObterAsync(RegraDistribuicaoVagasCodigo.Lei12711, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDistribuicaoVagasCodigo.Lei12711));
        mocks.ReferenciaReservaDemograficaReader.ObterPorIdAsync(referenciaId, Arg.Any<CancellationToken>())
            .Returns(new ReferenciaReservaDemograficaView(referenciaId, "2022", 79m, 1.5m, 8.5m, "Censo 2022"));
        foreach ((string codigo, Guid id) in federaisMaisAc)
        {
            ModalidadeView view = codigo == ModalidadesFederaisLei12711.Ac
                ? NovaModalidadeAmpla(id)
                : NovaModalidadeCotaReservada(id, codigo);
            mocks.ModalidadeReader.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns(view);
        }

        DefinirDistribuicaoVagasCommand command = new(
            processo.Id,
            [new ConfiguracaoDistribuicaoVagasInput(
                ofertaCursoId, 50, 0.5m, RegraDistribuicaoVagasCodigo.Lei12711, "v1", referenciaId,
                [.. federaisMaisAc.Select(f => f.Id)])], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.OfertaCursoReader, mocks.ModalidadeReader,
            mocks.ReferenciaReservaDemograficaReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.DistribuicaoVagas.Single().ReferenciaDemografica.Should().NotBeNull();
        processo.DistribuicaoVagas.Single().ReferenciaDemografica!.CensoReferencia.Should().Be("2022");
        processo.DistribuicaoVagas.Single().Modalidades.Should().HaveCount(9);
    }

    [Fact(DisplayName = "Handle com regra de distribuição inexistente recusa")]
    public async Task Handle_RegraNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(NovaOferta(Guid.CreateVersion7()));
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RegraCatalogo?)null);

        DefinirDistribuicaoVagasCommand command = new(
            processo.Id,
            [new ConfiguracaoDistribuicaoVagasInput(Guid.CreateVersion7(), 50, 1m, "INEXISTENTE", "v1", null, [Guid.CreateVersion7()])], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.OfertaCursoReader, mocks.ModalidadeReader,
            mocks.ReferenciaReservaDemograficaReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.RegraDistribuicaoNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com regra de tipo diferente de regra_distribuicao_vagas recusa")]
    public async Task Handle_RegraTipoInvalido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(NovaOferta(Guid.CreateVersion7()));
        RegraCatalogo regraErrada = RegraCatalogo.Criar(
            "FORMULA-MEDIA-PONDERADA", "v1", TipoRegra.RegraCalculo, Json("{}"), Json("[]"), "base").Value!;
        mocks.RegraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(regraErrada);

        DefinirDistribuicaoVagasCommand command = new(
            processo.Id,
            [new ConfiguracaoDistribuicaoVagasInput(Guid.CreateVersion7(), 50, 1m, "FORMULA-MEDIA-PONDERADA", "v1", null, [Guid.CreateVersion7()])], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.OfertaCursoReader, mocks.ModalidadeReader,
            mocks.ReferenciaReservaDemograficaReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.RegraDistribuicaoTipoInvalido");
    }

    [Fact(DisplayName = "Handle com oferta de curso inexistente recusa")]
    public async Task Handle_OfertaCursoNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((OfertaCursoView?)null);

        DefinirDistribuicaoVagasCommand command = new(
            processo.Id,
            [new ConfiguracaoDistribuicaoVagasInput(Guid.CreateVersion7(), 50, 1m, "X", "v1", null, [Guid.CreateVersion7()])], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.OfertaCursoReader, mocks.ModalidadeReader,
            mocks.ReferenciaReservaDemograficaReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.OfertaCursoNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com modalidade inexistente recusa")]
    public async Task Handle_ModalidadeNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria);
        Guid ofertaCursoId = Guid.CreateVersion7();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId));
        mocks.RegraCatalogoReader.ObterAsync(RegraDistribuicaoVagasCodigo.Institucional, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDistribuicaoVagasCodigo.Institucional));
        mocks.ModalidadeReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ModalidadeView?)null);

        DefinirDistribuicaoVagasCommand command = new(
            processo.Id,
            [new ConfiguracaoDistribuicaoVagasInput(
                ofertaCursoId, 50, 1m, RegraDistribuicaoVagasCodigo.Institucional, "v1", null, [Guid.CreateVersion7()])], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.OfertaCursoReader, mocks.ModalidadeReader,
            mocks.ReferenciaReservaDemograficaReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.ModalidadeNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com referência demográfica inexistente recusa")]
    public async Task Handle_ReferenciaDemograficaNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("SiSU 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);
        Guid ofertaCursoId = Guid.CreateVersion7();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId));
        mocks.RegraCatalogoReader.ObterAsync(RegraDistribuicaoVagasCodigo.Lei12711, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraDistribuicao(RegraDistribuicaoVagasCodigo.Lei12711));
        mocks.ReferenciaReservaDemograficaReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ReferenciaReservaDemograficaView?)null);

        DefinirDistribuicaoVagasCommand command = new(
            processo.Id,
            [new ConfiguracaoDistribuicaoVagasInput(
                ofertaCursoId, 50, 0.5m, RegraDistribuicaoVagasCodigo.Lei12711, "v1", Guid.CreateVersion7(), [Guid.CreateVersion7()])], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirDistribuicaoVagasCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.OfertaCursoReader, mocks.ModalidadeReader,
            mocks.ReferenciaReservaDemograficaReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaNaoEncontrada");
    }
}
