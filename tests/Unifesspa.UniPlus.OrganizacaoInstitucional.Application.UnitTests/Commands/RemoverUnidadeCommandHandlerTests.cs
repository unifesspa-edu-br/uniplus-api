namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

public sealed class RemoverUnidadeCommandHandlerTests
{
    private static Unidade CriarUnidade() =>
        Unidade.Criar(
            "Centro de Processos Seletivos",
            null,
            Slug.From("ceps").Value!,
            "CEPS",
            "0001",
            null,
            TipoUnidade.Centro,
            false,
            new DateOnly(2026, 1, 1),
            null,
            OrigemUnidade.CriadoNoUniPlus).Value!;

    [Fact(DisplayName = "Handle com unidade válida e sem subordinadas remove e invalida cache")]
    public async Task Handle_ComUnidadeValida_RemoveEInvalidaCache()
    {
        IUnidadeRepository repo = Substitute.For<IUnidadeRepository>();
        IInstituicaoRepository instituicaoRepo = Substitute.For<IInstituicaoRepository>();
        IOrganizacaoInstitucionalUnitOfWork uow = Substitute.For<IOrganizacaoInstitucionalUnitOfWork>();
        IUnidadeCacheInvalidator cache = Substitute.For<IUnidadeCacheInvalidator>();

        Unidade unidade = CriarUnidade();
        repo.ObterPorIdAsync(unidade.Id, Arg.Any<CancellationToken>()).Returns(unidade);
        repo.PossuiSubordinadasVivasAsync(unidade.Id, Arg.Any<CancellationToken>()).Returns(false);
        instituicaoRepo.ExisteComUnidadeRaizAsync(unidade.Id, Arg.Any<CancellationToken>()).Returns(false);

        Result resultado = await RemoverUnidadeCommandHandler.Handle(
            new RemoverUnidadeCommand(unidade.Id), repo, instituicaoRepo, uow, cache, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        repo.Received(1).Remover(unidade);
        await uow.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await cache.Received(1).InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com unidade inexistente retorna NaoEncontrada e NÃO persiste")]
    public async Task Handle_ComUnidadeInexistente_RetornaNaoEncontrada()
    {
        IUnidadeRepository repo = Substitute.For<IUnidadeRepository>();
        IInstituicaoRepository instituicaoRepo = Substitute.For<IInstituicaoRepository>();
        IOrganizacaoInstitucionalUnitOfWork uow = Substitute.For<IOrganizacaoInstitucionalUnitOfWork>();
        IUnidadeCacheInvalidator cache = Substitute.For<IUnidadeCacheInvalidator>();
        repo.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Unidade?)null);

        Result resultado = await RemoverUnidadeCommandHandler.Handle(
            new RemoverUnidadeCommand(Guid.NewGuid()), repo, instituicaoRepo, uow, cache, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.NaoEncontrada);
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await cache.DidNotReceive().InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com subordinadas vivas retorna RemocaoBloqueadaPorSubordinadas e NÃO persiste")]
    public async Task Handle_ComSubordinadasVivas_RetornaBloqueio()
    {
        IUnidadeRepository repo = Substitute.For<IUnidadeRepository>();
        IInstituicaoRepository instituicaoRepo = Substitute.For<IInstituicaoRepository>();
        IOrganizacaoInstitucionalUnitOfWork uow = Substitute.For<IOrganizacaoInstitucionalUnitOfWork>();
        IUnidadeCacheInvalidator cache = Substitute.For<IUnidadeCacheInvalidator>();

        Unidade unidade = CriarUnidade();
        repo.ObterPorIdAsync(unidade.Id, Arg.Any<CancellationToken>()).Returns(unidade);
        repo.PossuiSubordinadasVivasAsync(unidade.Id, Arg.Any<CancellationToken>()).Returns(true);

        Result resultado = await RemoverUnidadeCommandHandler.Handle(
            new RemoverUnidadeCommand(unidade.Id), repo, instituicaoRepo, uow, cache, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.RemocaoBloqueadaPorSubordinadas);
        repo.DidNotReceive().Remover(Arg.Any<Unidade>());
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com unidade que é raiz de Instituição viva retorna RemocaoBloqueadaPorInstituicao e NÃO persiste")]
    public async Task Handle_ComUnidadeRaizDeInstituicao_RetornaBloqueio()
    {
        IUnidadeRepository repo = Substitute.For<IUnidadeRepository>();
        IInstituicaoRepository instituicaoRepo = Substitute.For<IInstituicaoRepository>();
        IOrganizacaoInstitucionalUnitOfWork uow = Substitute.For<IOrganizacaoInstitucionalUnitOfWork>();
        IUnidadeCacheInvalidator cache = Substitute.For<IUnidadeCacheInvalidator>();

        Unidade unidade = CriarUnidade();
        repo.ObterPorIdAsync(unidade.Id, Arg.Any<CancellationToken>()).Returns(unidade);
        repo.PossuiSubordinadasVivasAsync(unidade.Id, Arg.Any<CancellationToken>()).Returns(false);
        instituicaoRepo.ExisteComUnidadeRaizAsync(unidade.Id, Arg.Any<CancellationToken>()).Returns(true);

        Result resultado = await RemoverUnidadeCommandHandler.Handle(
            new RemoverUnidadeCommand(unidade.Id), repo, instituicaoRepo, uow, cache, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.RemocaoBloqueadaPorInstituicao);
        repo.DidNotReceive().Remover(Arg.Any<Unidade>());
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await cache.DidNotReceive().InvalidarAsync(Arg.Any<CancellationToken>());
    }
}
