namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class DefinirReferenciaTemporalFatosCommandHandlerTests
{
    private sealed record Mocks(IProcessoSeletivoRepository Repository, IUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);
        return new Mocks(repository, Substitute.For<IUnitOfWork>());
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PS 2026", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

    private static Task<Result> DefinirAsync(Mocks mocks, DefinirReferenciaTemporalFatosCommand command) =>
        DefinirReferenciaTemporalFatosCommandHandler.Handle(command, mocks.Repository, mocks.UnitOfWork, CancellationToken.None);

    [Fact(DisplayName = "Processo inexistente retorna ProcessoSeletivo.NaoEncontrado e não grava")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontradoENaoGrava()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7());

        Result result = await DefinirAsync(mocks, new(Guid.CreateVersion7(), "FIM_INSCRICAO", null, null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Processo já publicado recusa a alteração antes de montar a referência e não grava")]
    public async Task Handle_ProcessoPublicado_RecusaAntesDeMontarAReferenciaENaoGrava()
    {
        ProcessoSeletivo processo = ApoioDaCascataDeRemanejamento.NovoProcessoPublicado();
        ReferenciaTemporalFatos? antes = processo.ReferenciaTemporalFatos;
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result result = await DefinirAsync(mocks, new(processo.Id, "TIPO_FORA_DO_CATALOGO", null, null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        processo.ReferenciaTemporalFatos.Should().Be(antes);
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo nulo remove a referência temporal dos dados do candidato e grava uma vez")]
    public async Task Handle_TipoNulo_RemoveReferenciaEGrava()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimInscricao, null, null).Value!).IsSuccess.Should().BeTrue();
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result result = await DefinirAsync(mocks, new(processo.Id, null, null, null));

        result.IsSuccess.Should().BeTrue();
        processo.ReferenciaTemporalFatos.Should().BeNull();
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo e data válidos definem a referência e gravam uma vez")]
    public async Task Handle_DataEspecificaValida_DefineReferenciaEGrava()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DateOnly data = new(2026, 3, 1);

        Result result = await DefinirAsync(mocks, new(processo.Id, "DATA_ESPECIFICA", data, null));

        result.IsSuccess.Should().BeTrue();
        processo.ReferenciaTemporalFatos!.Tipo.Should().Be(ReferenciaTipo.DataEspecifica);
        processo.ReferenciaTemporalFatos.Data.Should().Be(data);
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Referência incoerente preserva a lista completa de erros de validação e não grava")]
    public async Task Handle_ReferenciaIncoerente_PreservaListaDeErrosENaoGrava()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result result = await DefinirAsync(mocks, new(processo.Id, "DATA_ESPECIFICA", null, Guid.CreateVersion7()));

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("data", "ReferenciaTemporalFatos.DataIncoerenteComTipo"),
            ("faseId", "ReferenciaTemporalFatos.FaseIncoerenteComTipo"));
        processo.ReferenciaTemporalFatos.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Fase que não pertence ao cronograma do processo é recusada pelo domínio e não grava")]
    public async Task Handle_FaseForaDoCronograma_RecusaDoDominioSaiComoFalha()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result result = await DefinirAsync(mocks, new(processo.Id, "INICIO_FASE", null, Guid.CreateVersion7()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ReferenciaTemporalFatos.FaseNaoPertenceAoProcesso");
        result.Error.Message.Should().Be("A fase de referência da data deve ser uma das fases do cronograma do processo.");
        result.Errors.Should().ContainSingle().Which.Field.Should().Be("faseId");
        processo.ReferenciaTemporalFatos.Should().BeNull();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
