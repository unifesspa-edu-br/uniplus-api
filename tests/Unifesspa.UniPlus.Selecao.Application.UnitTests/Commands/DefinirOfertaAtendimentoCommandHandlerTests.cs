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

public sealed class DefinirOfertaAtendimentoCommandHandlerTests
{
    private static (
        IProcessoSeletivoRepository Repository,
        ICondicaoAtendimentoReader CondicaoReader,
        IRecursoAcessibilidadeReader RecursoReader,
        ITipoDeficienciaReader TipoDeficienciaReader,
        ISelecaoUnitOfWork UnitOfWork) NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);
        return (
            repository,
            Substitute.For<ICondicaoAtendimentoReader>(),
            Substitute.For<IRecursoAcessibilidadeReader>(),
            Substitute.For<ITipoDeficienciaReader>(),
            Substitute.For<ISelecaoUnitOfWork>());
    }

    [Fact(DisplayName = "Handle com tipo de deficiência sob condição PcD persiste (CA-06, ADR-0067)")]
    public async Task Handle_TipoDeficienciaSobPcd_Persiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);
        Guid condicaoId = Guid.CreateVersion7();
        Guid tipoDeficienciaId = Guid.CreateVersion7();

        (IProcessoSeletivoRepository Repository, ICondicaoAtendimentoReader CondicaoReader, IRecursoAcessibilidadeReader RecursoReader, ITipoDeficienciaReader TipoDeficienciaReader, ISelecaoUnitOfWork UnitOfWork) mocks = NovosMocks(processo, processo.Id);
        mocks.CondicaoReader.ObterPorIdAsync(condicaoId, Arg.Any<CancellationToken>())
            .Returns(new CondicaoAtendimentoView(condicaoId, "PCD", "Pessoa com deficiência"));
        mocks.TipoDeficienciaReader.ObterPorIdAsync(tipoDeficienciaId, Arg.Any<CancellationToken>())
            .Returns(new TipoDeficienciaView(tipoDeficienciaId, "Deficiência visual"));

        DefinirOfertaAtendimentoCommand command = new(processo.Id, [condicaoId], [], [tipoDeficienciaId], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirOfertaAtendimentoCommandHandler.Handle(
            command, mocks.Repository, mocks.CondicaoReader, mocks.RecursoReader, mocks.TipoDeficienciaReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.OfertaAtendimento.Should().NotBeNull();
        processo.OfertaAtendimento!.TiposDeficiencia.Should().ContainSingle();
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com tipo de deficiência sem condição PcD recusa (CA-06, ADR-0067)")]
    public async Task Handle_TipoDeficienciaSemPcd_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);
        Guid condicaoId = Guid.CreateVersion7();
        Guid tipoDeficienciaId = Guid.CreateVersion7();

        (IProcessoSeletivoRepository Repository, ICondicaoAtendimentoReader CondicaoReader, IRecursoAcessibilidadeReader RecursoReader, ITipoDeficienciaReader TipoDeficienciaReader, ISelecaoUnitOfWork UnitOfWork) mocks = NovosMocks(processo, processo.Id);
        mocks.CondicaoReader.ObterPorIdAsync(condicaoId, Arg.Any<CancellationToken>())
            .Returns(new CondicaoAtendimentoView(condicaoId, "LACTANTE", "Lactante"));
        mocks.TipoDeficienciaReader.ObterPorIdAsync(tipoDeficienciaId, Arg.Any<CancellationToken>())
            .Returns(new TipoDeficienciaView(tipoDeficienciaId, "Deficiência visual"));

        DefinirOfertaAtendimentoCommand command = new(processo.Id, [condicaoId], [], [tipoDeficienciaId], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirOfertaAtendimentoCommandHandler.Handle(
            command, mocks.Repository, mocks.CondicaoReader, mocks.RecursoReader, mocks.TipoDeficienciaReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("OfertaAtendimento.TipoDeficienciaSemCondicaoPcd");
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com condição inexistente recusa sem persistir")]
    public async Task Handle_CondicaoInexistente_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);
        Guid condicaoId = Guid.CreateVersion7();

        (IProcessoSeletivoRepository Repository, ICondicaoAtendimentoReader CondicaoReader, IRecursoAcessibilidadeReader RecursoReader, ITipoDeficienciaReader TipoDeficienciaReader, ISelecaoUnitOfWork UnitOfWork) mocks = NovosMocks(processo, processo.Id);
        mocks.CondicaoReader.ObterPorIdAsync(condicaoId, Arg.Any<CancellationToken>())
            .Returns((CondicaoAtendimentoView?)null);

        DefinirOfertaAtendimentoCommand command = new(processo.Id, [condicaoId], [], [], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await DefinirOfertaAtendimentoCommandHandler.Handle(
            command, mocks.Repository, mocks.CondicaoReader, mocks.RecursoReader, mocks.TipoDeficienciaReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("OfertaAtendimento.CondicaoNaoEncontrada");
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
