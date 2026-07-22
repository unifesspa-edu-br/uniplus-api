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

public sealed class AtualizarUnidadeCommandHandlerTests
{
    private static readonly DateOnly DataInicio = new(2026, 1, 1);

    private static Unidade UnidadeExistente() => Unidade.Criar(
        "Centro de Processos Seletivos",
        null,
        Slug.From("ceps").Value!,
        "CEPS",
        "0001",
        null,
        TipoUnidade.Centro,
        false,
        DataInicio,
        null,
        OrigemUnidade.CriadoNoUniPlus).Value!;

    // Command que reapresenta os mesmos identificadores da unidade existente —
    // as verificações de unicidade são puladas (não houve mudança).
    private static AtualizarUnidadeCommand CommandSemMudancaDeIdentificador(Guid id) => new(
        id,
        "Centro de Processos Seletivos (novo nome)",
        null,
        "ceps",
        "CEPS",
        "0001",
        null,
        TipoUnidade.Centro,
        false,
        null);

    private static (IUnidadeRepository Repo, IOrganizacaoInstitucionalUnitOfWork Uow, IUnidadeCacheInvalidator Cache) Mocks()
    {
        IUnidadeRepository repo = Substitute.For<IUnidadeRepository>();
        repo.SlugExisteEntreLivosAsync(Arg.Any<Slug>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(false);
        repo.SiglaExisteEntreLivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(false);
        repo.CodigoExisteEntreLivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(false);
        return (repo, Substitute.For<IOrganizacaoInstitucionalUnitOfWork>(), Substitute.For<IUnidadeCacheInvalidator>());
    }

    [Fact(DisplayName = "Handle com unidade inexistente retorna NaoEncontrada e NÃO persiste")]
    public async Task Handle_ComUnidadeInexistente_RetornaNaoEncontrada()
    {
        (IUnidadeRepository repo, IOrganizacaoInstitucionalUnitOfWork uow, IUnidadeCacheInvalidator cache) = Mocks();
        Guid id = Guid.NewGuid();
        repo.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((Unidade?)null);

        Result resultado = await AtualizarUnidadeCommandHandler.Handle(
            CommandSemMudancaDeIdentificador(id), repo, uow, cache, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.NaoEncontrada);
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await cache.DidNotReceive().InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle trocando slug para um já existente entre vivos retorna SlugJaExiste")]
    public async Task Handle_ComSlugDuplicadoExcluindoSelf_RetornaSlugJaExiste()
    {
        (IUnidadeRepository repo, IOrganizacaoInstitucionalUnitOfWork uow, IUnidadeCacheInvalidator cache) = Mocks();
        Unidade existente = UnidadeExistente();
        repo.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        repo.SlugExisteEntreLivosAsync(Arg.Any<Slug>(), existente.Id, Arg.Any<CancellationToken>()).Returns(true);

        AtualizarUnidadeCommand command = CommandSemMudancaDeIdentificador(existente.Id) with { Slug = "crca" };

        Result resultado = await AtualizarUnidadeCommandHandler.Handle(
            command, repo, uow, cache, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SlugJaExiste);
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle trocando sigla para uma já existente entre vivos retorna SiglaJaExiste")]
    public async Task Handle_ComSiglaDuplicadaExcluindoSelf_RetornaSiglaJaExiste()
    {
        (IUnidadeRepository repo, IOrganizacaoInstitucionalUnitOfWork uow, IUnidadeCacheInvalidator cache) = Mocks();
        Unidade existente = UnidadeExistente();
        repo.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        repo.SiglaExisteEntreLivosAsync("CRCA", existente.Id, Arg.Any<CancellationToken>()).Returns(true);

        AtualizarUnidadeCommand command = CommandSemMudancaDeIdentificador(existente.Id) with { Sigla = "CRCA" };

        Result resultado = await AtualizarUnidadeCommandHandler.Handle(
            command, repo, uow, cache, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SiglaJaExiste);
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com superior igual à própria unidade retorna SuperiorFormaCiclo")]
    public async Task Handle_ComSuperiorIgualAoProprio_RetornaSuperiorFormaCiclo()
    {
        (IUnidadeRepository repo, IOrganizacaoInstitucionalUnitOfWork uow, IUnidadeCacheInvalidator cache) = Mocks();
        Unidade existente = UnidadeExistente();
        repo.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        AtualizarUnidadeCommand command =
            CommandSemMudancaDeIdentificador(existente.Id) with { UnidadeSuperiorId = existente.Id };

        Result resultado = await AtualizarUnidadeCommandHandler.Handle(
            command, repo, uow, cache, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SuperiorFormaCiclo);
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com superior inexistente retorna SuperiorNaoEncontrado")]
    public async Task Handle_ComSuperiorInexistente_RetornaSuperiorNaoEncontrado()
    {
        (IUnidadeRepository repo, IOrganizacaoInstitucionalUnitOfWork uow, IUnidadeCacheInvalidator cache) = Mocks();
        Unidade existente = UnidadeExistente();
        Guid superiorId = Guid.NewGuid();
        repo.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        repo.ObterPorIdAsync(superiorId, Arg.Any<CancellationToken>()).Returns((Unidade?)null);

        AtualizarUnidadeCommand command =
            CommandSemMudancaDeIdentificador(existente.Id) with { UnidadeSuperiorId = superiorId };

        Result resultado = await AtualizarUnidadeCommandHandler.Handle(
            command, repo, uow, cache, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SuperiorNaoEncontrado);
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com superior que é descendente retorna SuperiorFormaCiclo (via EhDescendenteAsync)")]
    public async Task Handle_ComSuperiorDescendente_RetornaSuperiorFormaCiclo()
    {
        (IUnidadeRepository repo, IOrganizacaoInstitucionalUnitOfWork uow, IUnidadeCacheInvalidator cache) = Mocks();
        Unidade existente = UnidadeExistente();
        Unidade superior = UnidadeExistente();
        Guid superiorId = Guid.NewGuid();
        repo.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        repo.ObterPorIdAsync(superiorId, Arg.Any<CancellationToken>()).Returns(superior);
        repo.EhDescendenteAsync(superiorId, existente.Id, Arg.Any<CancellationToken>()).Returns(true);

        AtualizarUnidadeCommand command =
            CommandSemMudancaDeIdentificador(existente.Id) with { UnidadeSuperiorId = superiorId };

        Result resultado = await AtualizarUnidadeCommandHandler.Handle(
            command, repo, uow, cache, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SuperiorFormaCiclo);
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle trocando Codigo só na caixa (ABC→abc) detecta mudança e checa unicidade")]
    public async Task Handle_ComCodigoMudandoApenasCaixa_RodaChecagemDeUnicidade()
    {
        (IUnidadeRepository repo, IOrganizacaoInstitucionalUnitOfWork uow, IUnidadeCacheInvalidator cache) = Mocks();
        Unidade existente = Unidade.Criar(
            "Centro de Processos Seletivos",
            null,
            Slug.From("ceps").Value!,
            "CEPS",
            "ABC",
            null,
            TipoUnidade.Centro,
            false,
            DataInicio,
            null,
            OrigemUnidade.CriadoNoUniPlus).Value!;
        repo.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        repo.CodigoExisteEntreLivosAsync("abc", existente.Id, Arg.Any<CancellationToken>()).Returns(true);

        // Mesmos Slug/Sigla (pulam a checagem); só o Codigo muda de "ABC" para "abc".
        AtualizarUnidadeCommand command = CommandSemMudancaDeIdentificador(existente.Id) with { Codigo = "abc" };

        Result resultado = await AtualizarUnidadeCommandHandler.Handle(
            command, repo, uow, cache, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.CodigoJaExiste);
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com command válido persiste e invalida cache")]
    public async Task Handle_ComCommandValido_PersisteEInvalidaCache()
    {
        (IUnidadeRepository repo, IOrganizacaoInstitucionalUnitOfWork uow, IUnidadeCacheInvalidator cache) = Mocks();
        Unidade existente = UnidadeExistente();
        repo.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarUnidadeCommandHandler.Handle(
            CommandSemMudancaDeIdentificador(existente.Id), repo, uow, cache, TimeProvider.System, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await uow.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await cache.Received(1).InvalidarAsync(Arg.Any<CancellationToken>());
    }
}
