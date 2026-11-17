namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class CriarUnidadeCommandHandlerTests
{
    private readonly CadastroDeUnidadeSimulado _cadastro = new();

    private Task<Result<Guid>> CriarAsync(CriarUnidadeCommand command) =>
        CriarUnidadeCommandHandler.Handle(command, _cadastro.Conferencia, _cadastro.Gravacao, CancellationToken.None);

    [Fact(DisplayName = "Conferência recusada devolve a recusa sem tocar a unidade de trabalho nem o cache")]
    public async Task ConferenciaRecusada_NaoGrava()
    {
        _cadastro.SlugEmUso();

        Result<Guid> resultado = await CriarAsync(CadastroDeUnidadeSimulado.Criacao());

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SlugJaExiste);
        _cadastro.UnitOfWork.ReceivedCalls().Should().BeEmpty();
        _cadastro.Cache.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "Payload inválido devolve todos os erros acumulados")]
    public async Task PayloadInvalido_DevolveOsErrosAcumulados()
    {
        Result<Guid> resultado = await CriarAsync(
            CadastroDeUnidadeSimulado.Criacao() with { Nome = "", Slug = "SLUG-COM-MAIUSCULAS" });

        resultado.Errors.Should().HaveCount(2);
        _cadastro.UnitOfWork.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "Conferência aceita grava a Unidade e devolve o Id dela")]
    public async Task ConferenciaAceita_GravaEDevolveOId()
    {
        Result<Guid> resultado = await CriarAsync(CadastroDeUnidadeSimulado.Criacao());

        resultado.Value.Should().NotBeEmpty();
        await _cadastro.Unidades.Received(1).AdicionarAsync(
            Arg.Is<Unidade>(u => u.Id == resultado.Value), Arg.Any<CancellationToken>());
        await _cadastro.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await _cadastro.Cache.Received(1).InvalidarAsync(Arg.Any<CancellationToken>());
    }
}
