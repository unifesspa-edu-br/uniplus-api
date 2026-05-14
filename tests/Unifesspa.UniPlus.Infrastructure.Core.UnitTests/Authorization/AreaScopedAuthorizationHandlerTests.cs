namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using System.Security.Claims;

using AwesomeAssertions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;

public sealed class AreaScopedAuthorizationHandlerTests
{
    private static readonly AreaCodigo Ceps = AreaCodigo.From("CEPS").Value!;
    private static readonly AreaCodigo Crca = AreaCodigo.From("CRCA").Value!;

    [Fact]
    public async Task AdminDaArea_EditandoEntidadeDaPropriaArea_DeveAutorizar()
    {
        AreaScopedAuthorizationHandler handler = BuildHandler(areasAdministradas: [Ceps], isPlataformaAdmin: false);
        AuthorizationHandlerContext context = BuildContext(new EntidadeAreaScopedFake { Proprietario = Ceps });

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
        context.HasFailed.Should().BeFalse();
    }

    [Fact]
    public async Task AdminDaArea_EditandoEntidadeDeOutraArea_DeveNegar()
    {
        AreaScopedAuthorizationHandler handler = BuildHandler(areasAdministradas: [Ceps], isPlataformaAdmin: false);
        AuthorizationHandlerContext context = BuildContext(new EntidadeAreaScopedFake { Proprietario = Crca });

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task PlataformaAdmin_EditandoEntidadeAreaScoped_DeveAutorizarERegistrarOnBehalfOf()
    {
        ILogger<AreaScopedAuthorizationHandler> logger = CreateRecordingLogger();
        AreaScopedAuthorizationHandler handler = BuildHandler(
            areasAdministradas: [], isPlataformaAdmin: true, logger);
        AuthorizationHandlerContext context = BuildContext(new EntidadeAreaScopedFake { Proprietario = Ceps });

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
        LogCallCount(logger).Should().Be(1, "edição on-behalf-of de item área-scoped emite log operacional");
    }

    [Fact]
    public async Task PlataformaAdmin_EditandoItemGlobal_DeveAutorizarSemOnBehalfOf()
    {
        ILogger<AreaScopedAuthorizationHandler> logger = CreateRecordingLogger();
        AreaScopedAuthorizationHandler handler = BuildHandler(
            areasAdministradas: [], isPlataformaAdmin: true, logger);
        AuthorizationHandlerContext context = BuildContext(new EntidadeAreaScopedFake { Proprietario = null });

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
        LogCallCount(logger).Should().Be(0, "item global não tem área proprietária — não há on-behalf-of");
    }

    [Fact]
    public async Task AdminDaAreaQueTambemEPlataformaAdmin_EditandoEntidadeDaPropriaArea_NaoRegistraOnBehalfOf()
    {
        // Quando o caller é genuinamente admin da área dona, ele edita por
        // direito próprio — não é uma edição on-behalf-of.
        ILogger<AreaScopedAuthorizationHandler> logger = CreateRecordingLogger();
        AreaScopedAuthorizationHandler handler = BuildHandler(
            areasAdministradas: [Ceps], isPlataformaAdmin: true, logger);
        AuthorizationHandlerContext context = BuildContext(new EntidadeAreaScopedFake { Proprietario = Ceps });

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
        LogCallCount(logger).Should().Be(0, "admin da própria área edita por direito próprio, não on-behalf-of");
    }

    [Fact]
    public async Task LeitorDaArea_EditandoEntidadeDaArea_DeveNegar()
    {
        // Roles -leitor não entram em AreasAdministradas, e leitor não é
        // plataforma-admin — logo não pode escrever.
        AreaScopedAuthorizationHandler handler = BuildHandler(areasAdministradas: [], isPlataformaAdmin: false);
        AuthorizationHandlerContext context = BuildContext(new EntidadeAreaScopedFake { Proprietario = Ceps });

        await handler.HandleAsync(context);

        context.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task SemRoleDeArea_EditandoItemGlobal_DeveNegar()
    {
        // Itens globais (Proprietario nulo) só podem ser editados por plataforma-admin.
        AreaScopedAuthorizationHandler handler = BuildHandler(areasAdministradas: [], isPlataformaAdmin: false);
        AuthorizationHandlerContext context = BuildContext(new EntidadeAreaScopedFake { Proprietario = null });

        await handler.HandleAsync(context);

        context.HasFailed.Should().BeTrue();
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private static AreaScopedAuthorizationHandler BuildHandler(
        IReadOnlyCollection<AreaCodigo> areasAdministradas,
        bool isPlataformaAdmin,
        ILogger<AreaScopedAuthorizationHandler>? logger = null)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.AreasAdministradas.Returns(areasAdministradas);
        userContext.IsPlataformaAdmin.Returns(isPlataformaAdmin);
        userContext.UserId.Returns("sub-actor");

        return new AreaScopedAuthorizationHandler(
            userContext,
            logger ?? NullLogger<AreaScopedAuthorizationHandler>.Instance);
    }

    private static AuthorizationHandlerContext BuildContext(IAreaScopedEntity resource)
    {
        RequireAreaProprietarioRequirement requirement = new();
        return new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity()),
            resource);
    }

    private static ILogger<AreaScopedAuthorizationHandler> CreateRecordingLogger()
    {
        ILogger<AreaScopedAuthorizationHandler> logger = Substitute.For<ILogger<AreaScopedAuthorizationHandler>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        return logger;
    }

    private static int LogCallCount(ILogger logger) =>
        logger.ReceivedCalls().Count(call =>
            string.Equals(call.GetMethodInfo().Name, nameof(ILogger.Log), StringComparison.Ordinal));

    private sealed class EntidadeAreaScopedFake : IAreaScopedEntity
    {
        public AreaCodigo? Proprietario { get; init; }
    }
}
