namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

public sealed class CriarUnidadeCommandHandlerTests
{
    private static readonly DateOnly DataInicio = new(2026, 1, 1);

    private static CriarUnidadeCommand CommandValido() => new(
        "Centro de Processos Seletivos",
        null,
        "ceps",
        "CEPS",
        "0001",
        null,
        TipoUnidade.Centro,
        false,
        DataInicio,
        null,
        OrigemUnidade.CriadoNoUniPlus);

    [Fact(DisplayName = "Handle com command válido persiste e invalida cache")]
    public async Task Handle_ComCommandValido_PersisteEInvalidaCache()
    {
        IUnidadeRepository repo = Substitute.For<IUnidadeRepository>();
        IOrganizacaoInstitucionalUnitOfWork uow = Substitute.For<IOrganizacaoInstitucionalUnitOfWork>();
        IUnidadeCacheInvalidator cache = Substitute.For<IUnidadeCacheInvalidator>();
        repo.SlugExisteEntreLivosAsync(Arg.Any<Slug>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        repo.SiglaExisteEntreLivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        repo.CodigoExisteEntreLivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        Result<Guid> resultado = await CriarUnidadeCommandHandler.Handle(
            CommandValido(), repo, uow, cache, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBeEmpty();
        await repo.Received(1).AdicionarAsync(Arg.Any<Unidade>(), Arg.Any<CancellationToken>());
        await uow.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await cache.Received(1).InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com slug duplicado retorna SlugJaExiste e NÃO persiste")]
    public async Task Handle_ComSlugDuplicado_RetornaSlugJaExiste()
    {
        IUnidadeRepository repo = Substitute.For<IUnidadeRepository>();
        IOrganizacaoInstitucionalUnitOfWork uow = Substitute.For<IOrganizacaoInstitucionalUnitOfWork>();
        IUnidadeCacheInvalidator cache = Substitute.For<IUnidadeCacheInvalidator>();
        repo.SlugExisteEntreLivosAsync(Arg.Any<Slug>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        Result<Guid> resultado = await CriarUnidadeCommandHandler.Handle(
            CommandValido(), repo, uow, cache, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SlugJaExiste);
        await repo.DidNotReceive().AdicionarAsync(Arg.Any<Unidade>(), Arg.Any<CancellationToken>());
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await cache.DidNotReceive().InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com sigla duplicada retorna SiglaJaExiste e NÃO persiste")]
    public async Task Handle_ComSiglaDuplicada_RetornaSiglaJaExiste()
    {
        IUnidadeRepository repo = Substitute.For<IUnidadeRepository>();
        IOrganizacaoInstitucionalUnitOfWork uow = Substitute.For<IOrganizacaoInstitucionalUnitOfWork>();
        IUnidadeCacheInvalidator cache = Substitute.For<IUnidadeCacheInvalidator>();
        repo.SlugExisteEntreLivosAsync(Arg.Any<Slug>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        repo.SiglaExisteEntreLivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        Result<Guid> resultado = await CriarUnidadeCommandHandler.Handle(
            CommandValido(), repo, uow, cache, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SiglaJaExiste);
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com slug inválido retorna SlugFormatoInvalido (Slug.From falha)")]
    public async Task Handle_ComSlugInvalido_RetornaSlugFormatoInvalido()
    {
        IUnidadeRepository repo = Substitute.For<IUnidadeRepository>();
        IOrganizacaoInstitucionalUnitOfWork uow = Substitute.For<IOrganizacaoInstitucionalUnitOfWork>();
        IUnidadeCacheInvalidator cache = Substitute.For<IUnidadeCacheInvalidator>();
        CriarUnidadeCommand command = CommandValido() with { Slug = "SLUG-COM-MAIUSCULAS" };

        Result<Guid> resultado = await CriarUnidadeCommandHandler.Handle(
            command, repo, uow, cache, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SlugFormatoInvalido);
        await repo.DidNotReceive().SlugExisteEntreLivosAsync(
            Arg.Any<Slug>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com superior inexistente retorna SuperiorNaoEncontrado")]
    public async Task Handle_ComSuperiorInexistente_RetornaSuperiorNaoEncontrado()
    {
        IUnidadeRepository repo = Substitute.For<IUnidadeRepository>();
        IOrganizacaoInstitucionalUnitOfWork uow = Substitute.For<IOrganizacaoInstitucionalUnitOfWork>();
        IUnidadeCacheInvalidator cache = Substitute.For<IUnidadeCacheInvalidator>();
        repo.SlugExisteEntreLivosAsync(Arg.Any<Slug>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        repo.SiglaExisteEntreLivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        repo.CodigoExisteEntreLivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        Guid superiorId = Guid.NewGuid();
        repo.ObterPorIdAsync(superiorId, Arg.Any<CancellationToken>())
            .Returns((Unidade?)null);

        CriarUnidadeCommand command = CommandValido() with { UnidadeSuperiorId = superiorId };

        Result<Guid> resultado = await CriarUnidadeCommandHandler.Handle(
            command, repo, uow, cache, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SuperiorNaoEncontrado);
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
