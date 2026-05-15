namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.AreasOrganizacionais;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

public sealed class CriarAreaOrganizacionalCommandHandlerTests
{
    private static CriarAreaOrganizacionalCommand CommandValido() => new(
        "CEPS",
        "Centro de Processos Seletivos",
        TipoAreaOrganizacional.Centro,
        "Unidade responsável pelos processos seletivos da Unifesspa.",
        "0055-organizacao-institucional-bounded-context");

    [Fact(DisplayName = "Handle com command válido persiste e invalida cache")]
    public async Task Handle_ComCommandValido_PersisteEInvalidaCache()
    {
        IAreaOrganizacionalRepository repo = Substitute.For<IAreaOrganizacionalRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IAreaOrganizacionalCacheInvalidator cache = Substitute.For<IAreaOrganizacionalCacheInvalidator>();
        repo.ExistePorCodigoAsync(Arg.Any<AreaCodigo>(), Arg.Any<CancellationToken>())
            .Returns(false);

        Result<Guid> resultado = await CriarAreaOrganizacionalCommandHandler.Handle(
            CommandValido(), repo, uow, cache, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBeEmpty();
        await repo.Received(1).AdicionarAsync(Arg.Any<AreaOrganizacional>(), Arg.Any<CancellationToken>());
        await uow.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await cache.Received(1).InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com código duplicado retorna CodigoJaExiste e NÃO invalida cache")]
    public async Task Handle_ComCodigoDuplicado_NaoChamaRepoNemUow()
    {
        IAreaOrganizacionalRepository repo = Substitute.For<IAreaOrganizacionalRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IAreaOrganizacionalCacheInvalidator cache = Substitute.For<IAreaOrganizacionalCacheInvalidator>();
        repo.ExistePorCodigoAsync(Arg.Any<AreaCodigo>(), Arg.Any<CancellationToken>())
            .Returns(true);

        Result<Guid> resultado = await CriarAreaOrganizacionalCommandHandler.Handle(
            CommandValido(), repo, uow, cache, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AreaOrganizacionalErrorCodes.CodigoJaExiste);
        await repo.DidNotReceive().AdicionarAsync(Arg.Any<AreaOrganizacional>(), Arg.Any<CancellationToken>());
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await cache.DidNotReceive().InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com código inválido (AreaCodigo.From falha) retorna AreaCodigo.Invalido")]
    public async Task Handle_ComCodigoInvalido_RetornaAreaCodigoInvalido()
    {
        IAreaOrganizacionalRepository repo = Substitute.For<IAreaOrganizacionalRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IAreaOrganizacionalCacheInvalidator cache = Substitute.For<IAreaOrganizacionalCacheInvalidator>();

        CriarAreaOrganizacionalCommand command = CommandValido() with { Codigo = "9XX" }; // começa com dígito

        Result<Guid> resultado = await CriarAreaOrganizacionalCommandHandler.Handle(
            command, repo, uow, cache, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AreaCodigo.CodigoErroInvalido);
        await repo.DidNotReceive().ExistePorCodigoAsync(Arg.Any<AreaCodigo>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com AdrReferenceCode vazio retorna AdrReferenceObrigatorio (BDD-3)")]
    public async Task Handle_ComAdrReferenceVazio_RetornaAdrReferenceObrigatorio()
    {
        IAreaOrganizacionalRepository repo = Substitute.For<IAreaOrganizacionalRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IAreaOrganizacionalCacheInvalidator cache = Substitute.For<IAreaOrganizacionalCacheInvalidator>();
        repo.ExistePorCodigoAsync(Arg.Any<AreaCodigo>(), Arg.Any<CancellationToken>())
            .Returns(false);

        CriarAreaOrganizacionalCommand command = CommandValido() with { AdrReferenceCode = string.Empty };

        Result<Guid> resultado = await CriarAreaOrganizacionalCommandHandler.Handle(
            command, repo, uow, cache, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AreaOrganizacionalErrorCodes.AdrReferenceObrigatorio);
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
