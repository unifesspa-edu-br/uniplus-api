namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

public sealed class RemoverInstituicaoCommandHandlerTests
{
    private static Instituicao InstituicaoExistente() =>
        Instituicao.Criar(
            "3990", "Universidade Federal do Sul e Sudeste do Pará", "Unifesspa", "Universidade", "Pública Federal",
            cnpj: null, mantenedora: null, codigoMantenedoraEmec: null, situacao: null, atoCredenciamento: null,
            atoRecredenciamento: null, conceitoInstitucional: null, igc: null, website: null, enderecoSede: null,
            cidadeCodigoIbge: null, cidadeNome: null, cidadeUf: null, cidadeOrigem: null,
            cidadeDisplayAtualizadoEm: null, unidadeRaizId: null).Value!;

    [Fact(DisplayName = "Handle com Instituição existente faz soft-delete e invalida cache (CA-05)")]
    public async Task Handle_ComInstituicaoExistente_RemoveEInvalidaCache()
    {
        IInstituicaoRepository repo = Substitute.For<IInstituicaoRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IInstituicaoCacheInvalidator cache = Substitute.For<IInstituicaoCacheInvalidator>();
        Instituicao existente = InstituicaoExistente();
        repo.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await RemoverInstituicaoCommandHandler.Handle(
            new RemoverInstituicaoCommand(existente.Id), repo, uow, cache, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        repo.Received(1).Remover(existente);
        await uow.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await cache.Received(1).InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com Instituição inexistente retorna NaoEncontrada e NÃO persiste")]
    public async Task Handle_ComInstituicaoInexistente_RetornaNaoEncontrada()
    {
        IInstituicaoRepository repo = Substitute.For<IInstituicaoRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IInstituicaoCacheInvalidator cache = Substitute.For<IInstituicaoCacheInvalidator>();
        repo.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Instituicao?)null);

        Result resultado = await RemoverInstituicaoCommandHandler.Handle(
            new RemoverInstituicaoCommand(Guid.CreateVersion7()), repo, uow, cache, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.NaoEncontrada);
        repo.DidNotReceive().Remover(Arg.Any<Instituicao>());
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
