namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Hosting;

using System.Reflection;

using AwesomeAssertions;

using Infrastructure;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Selecao.API.Controllers;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosEdital;

/// <summary>
/// Sentinela contra o TTL de idempotência do endpoint de iniciar upload
/// (achado Codex no PR #790) divergir do TTL real da URL pre-assinada de
/// PUT. Um <c>[RequiresIdempotencyKey]</c> sem override usa o teto de 24h
/// (ADR-0027) — como a URL expira em <see cref="IniciarUploadDocumentoEditalCommandHandler.TtlUploadSegundos"/>
/// (15 min), um replay depois disso devolveria uma URL inutilizável. O
/// override existe justamente para alinhar os dois; este teste trava que
/// alguém mude uma constante sem a outra.
/// </summary>
public sealed class DocumentoEditalIdempotencyTtlSentinelTests : IClassFixture<SelecaoApiFactory>
{
    private readonly SelecaoApiFactory _factory;

    public DocumentoEditalIdempotencyTtlSentinelTests(SelecaoApiFactory factory) => _factory = factory;

    [Fact(DisplayName = "IniciarUpload tem TTL de idempotência igual ao TTL da URL pre-assinada")]
    public void IniciarUpload_TtlIdempotenciaIgualAoTtlDaUrl()
    {
        using HttpClient _ = _factory.CreateClient();
        EndpointDataSource dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();

        MethodInfo metodo = dataSource.Endpoints
            .Select(static endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>())
            .Where(static descriptor => descriptor is not null)
            .Cast<ControllerActionDescriptor>()
            .Where(descriptor => descriptor.ControllerTypeInfo == typeof(DocumentosEditalController)
                && descriptor.MethodInfo.Name == nameof(DocumentosEditalController.IniciarUpload))
            .Select(descriptor => descriptor.MethodInfo)
            .Should().ContainSingle(because: "o endpoint deve estar registrado pelo MVC")
            .Subject;

        RequiresIdempotencyKeyAttribute atributo = metodo
            .GetCustomAttribute<RequiresIdempotencyKeyAttribute>(inherit: true)!;

        atributo.Should().NotBeNull();
        atributo.TtlSeconds.Should().Be(
            IniciarUploadDocumentoEditalCommandHandler.TtlUploadSegundos,
            because: "um replay de idempotência depois da URL expirar devolveria uma URL inutilizável");
    }

    [Fact(DisplayName = "ConfirmarUpload usa o TTL default de idempotência (24h) — resposta não carrega artefato com expiração própria")]
    public void ConfirmarUpload_UsaTtlDefault()
    {
        using HttpClient _ = _factory.CreateClient();
        EndpointDataSource dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();

        MethodInfo metodo = dataSource.Endpoints
            .Select(static endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>())
            .Where(static descriptor => descriptor is not null)
            .Cast<ControllerActionDescriptor>()
            .Where(descriptor => descriptor.ControllerTypeInfo == typeof(DocumentosEditalController)
                && descriptor.MethodInfo.Name == nameof(DocumentosEditalController.ConfirmarUpload))
            .Select(descriptor => descriptor.MethodInfo)
            .Should().ContainSingle(because: "o endpoint deve estar registrado pelo MVC")
            .Subject;

        RequiresIdempotencyKeyAttribute atributo = metodo
            .GetCustomAttribute<RequiresIdempotencyKeyAttribute>(inherit: true)!;

        atributo.Should().NotBeNull();
        atributo.TtlSeconds.Should().Be(-1, because: "sem override, usa IdempotencyOptions.Ttl");
    }
}
