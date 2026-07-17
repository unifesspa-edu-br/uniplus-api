namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

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

/// <summary>
/// Cobertura do <see cref="DefinirDocumentosExigidosCommandHandler"/> (Story #554,
/// PR-a): a resolução do snapshot-copy de <c>TipoDocumento</c> (Configuração,
/// ADR-0056) e os erros nomeados que uma resolução malsucedida produz.
/// </summary>
public sealed class DefinirDocumentosExigidosCommandHandlerTests
{
    private sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        ITipoDocumentoReader TipoDocumentoReader,
        ISelecaoUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);

        return new Mocks(
            repository,
            Substitute.For<ITipoDocumentoReader>(),
            Substitute.For<ISelecaoUnitOfWork>());
    }

    private static Task<Result<MutacaoAceita>> HandleAsync(Mocks mocks, DefinirDocumentosExigidosCommand command) =>
        DefinirDocumentosExigidosCommandHandler.Handle(
            command,
            mocks.Repository,
            mocks.TipoDocumentoReader,
            mocks.UnitOfWork,
            CancellationToken.None);

    private static TipoDocumentoView TipoDocumentoResultado(Guid id) =>
        new(id, "IDENTIDADE", "Documento de identidade", "PESSOAL");

    private static FaseCronograma FaseQualquer() => FaseCronograma.Criar(
        1, Guid.CreateVersion7(), "INSCRICAO", "CEPS", OrigemDataFase.Delegada,
        agrupaEtapas: false, permiteComplementacao: false, produzResultado: false,
        resultadoDefinitivo: false, coletaInscricao: false, inicio: null, fim: null,
        atoProduzidoCodigo: null, atoProduzidoEfeitoIrreversivel: false,
        bancasRequeridas: [], regraRecurso: null).Value!;

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Guid processoId = Guid.CreateVersion7();
        Mocks mocks = NovosMocks(processo: null, processoId);
        DefinirDocumentosExigidosCommand command = new(processoId, [], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com tipo de documento não encontrado retorna DocumentoExigido.TipoDocumentoNaoEncontrado")]
    public async Task Handle_TipoDocumentoNaoEncontrado_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns((TipoDocumentoView?)null);

        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [item], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.TipoDocumentoNaoEncontrado");
    }

    [Fact(DisplayName = "Handle com item válido define os documentos exigidos e persiste")]
    public async Task Handle_ItemValido_DefineDocumentosExigidosEPersiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [item], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().ContainSingle(d => d.TipoDocumentoCodigo == "IDENTIDADE");
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
