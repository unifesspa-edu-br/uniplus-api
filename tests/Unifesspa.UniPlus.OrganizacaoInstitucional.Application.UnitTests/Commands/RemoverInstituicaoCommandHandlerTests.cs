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
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class RemoverInstituicaoCommandHandlerTests
{
    private readonly IInstituicaoRepository _repo = Substitute.For<IInstituicaoRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IInstituicaoCacheInvalidator _cache = Substitute.For<IInstituicaoCacheInvalidator>();

    private Task<Result> RemoverAsync(Guid id) =>
        RemoverInstituicaoCommandHandler.Handle(
            new RemoverInstituicaoCommand(id), _repo, new GravacaoDaInstituicao(_repo, _uow, _cache), CancellationToken.None);

    [Fact(DisplayName = "Handle com Instituição existente a remove, salva e invalida o cache")]
    public async Task Handle_ComInstituicaoExistente_RemoveSalvaEInvalidaCache()
    {
        Instituicao existente = InstituicaoDeTeste.Criar().Value!;
        _repo.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await RemoverAsync(existente.Id);

        resultado.IsSuccess.Should().BeTrue();
        _repo.Received(1).Remover(existente);
        await _uow.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await _cache.Received(1).InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com Instituição inexistente devolve NaoEncontrada sem gravar")]
    public async Task Handle_ComInstituicaoInexistente_DevolveNaoEncontrada()
    {
        _repo.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Instituicao?)null);

        Result resultado = await RemoverAsync(Guid.CreateVersion7());

        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.NaoEncontrada);
        _repo.DidNotReceive().Remover(Arg.Any<Instituicao>());
        await _uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
