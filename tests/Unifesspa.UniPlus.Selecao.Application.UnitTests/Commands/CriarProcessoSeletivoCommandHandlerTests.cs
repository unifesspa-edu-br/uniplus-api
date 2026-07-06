namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class CriarProcessoSeletivoCommandHandlerTests
{
    [Fact(DisplayName = "Handle persiste o processo em rascunho e retorna o id (CA-01)")]
    public async Task Handle_PersisteERetornaId()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        CriarProcessoSeletivoCommand command = new("PS 2026 — SiSU", TipoProcesso.SiSU);

        Result<Guid> result = await CriarProcessoSeletivoCommandHandler.Handle(
            command, repository, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await repository.Received(1).AdicionarAsync(
            Arg.Is<ProcessoSeletivo>(p => p.Nome == "PS 2026 — SiSU" && p.Status == StatusProcesso.Rascunho),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
