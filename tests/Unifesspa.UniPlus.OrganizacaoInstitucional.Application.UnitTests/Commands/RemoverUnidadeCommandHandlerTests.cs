namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class RemoverUnidadeCommandHandlerTests
{
    private readonly CadastroDeUnidadeSimulado _cadastro = new();

    private Task<Result> RemoverAsync(Guid id) =>
        RemoverUnidadeCommandHandler.Handle(
            new RemoverUnidadeCommand(id), _cadastro.Conferencia, _cadastro.Gravacao, CancellationToken.None);

    [Fact(DisplayName = "Conferência recusada devolve a recusa sem remover nem tocar a unidade de trabalho")]
    public async Task ConferenciaRecusada_NaoRemove()
    {
        Result resultado = await RemoverAsync(Guid.NewGuid());

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.NaoEncontrada);
        _cadastro.Unidades.DidNotReceive().Remover(Arg.Any<Unidade>());
        _cadastro.UnitOfWork.ReceivedCalls().Should().BeEmpty();
        _cadastro.Cache.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "Conferência aceita remove, salva, invalida o cache e devolve sucesso")]
    public async Task ConferenciaAceita_RemoveEInvalida()
    {
        Unidade unidade = _cadastro.UnidadeCadastrada();

        Result resultado = await RemoverAsync(unidade.Id);

        resultado.IsSuccess.Should().BeTrue();
        _cadastro.Unidades.Received(1).Remover(unidade);
        await _cadastro.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await _cadastro.Cache.Received(1).InvalidarAsync(Arg.Any<CancellationToken>());
    }
}
