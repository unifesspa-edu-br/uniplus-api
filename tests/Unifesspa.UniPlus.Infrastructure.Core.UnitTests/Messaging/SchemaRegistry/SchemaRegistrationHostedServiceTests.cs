namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry;

using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Confluent.SchemaRegistry;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;
using SrSchema = Confluent.SchemaRegistry.Schema;

/// <summary>
/// Cobertura de <see cref="SchemaRegistrationHostedService"/>: registro idempotente,
/// fail-graceful seletivo (transient via HttpRequestError vs deterministic),
/// propagação de exceções não-recuperáveis.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SchemaRegistrationHostedServiceTests
{
    private const string SubjectFoo = "foo-events-value";
    private const string SubjectBar = "bar-events-value";
    private const string ValidSchemaResource =
        "Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry.Resources.TestSchema.avsc";
    private const string MissingResource =
        "Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry.Resources.DoesNotExist.avsc";

    private static readonly System.Reflection.Assembly TestAssembly =
        typeof(SchemaRegistrationHostedServiceTests).Assembly;

    private static SchemaRegistration ValidRegistration(string subject)
        => new(subject, ValidSchemaResource, TestAssembly);

    private static SchemaRegistration MissingResourceRegistration(string subject)
        => new(subject, MissingResource, TestAssembly);

    private static SchemaRegistrationHostedService CreateSut(
        ISchemaRegistryClient client,
        params SchemaRegistration[] registrations)
    {
        return new SchemaRegistrationHostedService(
            client,
            registrations,
            NullLogger<SchemaRegistrationHostedService>.Instance);
    }

    [Fact(DisplayName = "Sem registrations — StartAsync retorna sem erro e sem chamar o cliente")]
    public async Task SemRegistrations_DeveLogarENaoLancar()
    {
        ISchemaRegistryClient client = Substitute.For<ISchemaRegistryClient>();
        SchemaRegistrationHostedService sut = CreateSut(client);

        Func<Task> act = () => sut.StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        await client.DidNotReceiveWithAnyArgs()
            .RegisterSchemaAsync(default!, default(SrSchema)!, default);
    }

    [Fact(DisplayName = "1 registration happy path — RegisterSchemaAsync chamado")]
    public async Task UmaRegistration_HappyPath_DeveRegistrar()
    {
        ISchemaRegistryClient client = Substitute.For<ISchemaRegistryClient>();
        client.RegisterSchemaAsync(SubjectFoo, Arg.Any<SrSchema>(), normalize: true)
            .Returns(Task.FromResult(42));

        SchemaRegistrationHostedService sut = CreateSut(client, ValidRegistration(SubjectFoo));

        await sut.StartAsync(CancellationToken.None);

        await client.Received(1).RegisterSchemaAsync(SubjectFoo, Arg.Any<SrSchema>(), normalize: true);
    }

    [Fact(DisplayName = "N registrations — todas iteradas")]
    public async Task NRegistrations_DeveIterarTodas()
    {
        ISchemaRegistryClient client = Substitute.For<ISchemaRegistryClient>();
        client.RegisterSchemaAsync(Arg.Any<string>(), Arg.Any<SrSchema>(), normalize: true)
            .Returns(Task.FromResult(1));

        SchemaRegistrationHostedService sut = CreateSut(
            client,
            ValidRegistration(SubjectFoo),
            ValidRegistration(SubjectBar));

        await sut.StartAsync(CancellationToken.None);

        await client.Received(1).RegisterSchemaAsync(SubjectFoo, Arg.Any<SrSchema>(), normalize: true);
        await client.Received(1).RegisterSchemaAsync(SubjectBar, Arg.Any<SrSchema>(), normalize: true);
    }

    [Theory(DisplayName = "HttpRequestError transient (Connection/NameResolution) é silenciado")]
    [InlineData(HttpRequestError.ConnectionError)]
    [InlineData(HttpRequestError.NameResolutionError)]
    public async Task HttpRequestError_Transient_DeveSerSilenciado(HttpRequestError errorType)
    {
        ISchemaRegistryClient client = Substitute.For<ISchemaRegistryClient>();
        client.RegisterSchemaAsync(Arg.Any<string>(), Arg.Any<SrSchema>(), normalize: true)
            .Throws(new HttpRequestException(errorType, "transient"));

        SchemaRegistrationHostedService sut = CreateSut(client, ValidRegistration(SubjectFoo));

        Func<Task> act = () => sut.StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync(
            because: "transient errors deixam o host subir; Confluent serdes retenta em runtime.");
    }

    [Theory(DisplayName = "HttpRequestError deterministic (TLS/HttpProtocol/etc.) propaga")]
    [InlineData(HttpRequestError.SecureConnectionError)]
    [InlineData(HttpRequestError.HttpProtocolError)]
    [InlineData(HttpRequestError.UserAuthenticationError)]
    [InlineData(HttpRequestError.InvalidResponse)]
    [InlineData(HttpRequestError.ProxyTunnelError)]
    public async Task HttpRequestError_Deterministic_DevePropagar(HttpRequestError errorType)
    {
        ISchemaRegistryClient client = Substitute.For<ISchemaRegistryClient>();
        client.RegisterSchemaAsync(Arg.Any<string>(), Arg.Any<SrSchema>(), normalize: true)
            .Throws(new HttpRequestException(errorType, $"deterministic {errorType}"));

        SchemaRegistrationHostedService sut = CreateSut(client, ValidRegistration(SubjectFoo));

        Func<Task> act = () => sut.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>(
            because: "release com TLS/proxy/cert ruim deve travar StartAsync — não rodar em modo degraded.");
    }

    [Fact(DisplayName = "SchemaRegistryException (auth/conflict/malformed) propaga")]
    public async Task SchemaRegistryException_DevePropagar()
    {
        ISchemaRegistryClient client = Substitute.For<ISchemaRegistryClient>();
        client.RegisterSchemaAsync(Arg.Any<string>(), Arg.Any<SrSchema>(), normalize: true)
            .Throws(new SchemaRegistryException("incompatible", System.Net.HttpStatusCode.Conflict, errorCode: 409));

        SchemaRegistrationHostedService sut = CreateSut(client, ValidRegistration(SubjectFoo));

        Func<Task> act = () => sut.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<SchemaRegistryException>(
            because: "auth/conflict/malformed schema é deterministic — release não deve subir em modo degraded.");
    }

    [Fact(DisplayName = "InvalidOperationException (embedded resource ausente) propaga")]
    public async Task InvalidOperation_ResourceAusente_DevePropagar()
    {
        ISchemaRegistryClient client = Substitute.For<ISchemaRegistryClient>();
        SchemaRegistrationHostedService sut = CreateSut(client, MissingResourceRegistration(SubjectFoo));

        Func<Task> act = () => sut.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "embedded resource ausente é bug de empacotamento — release não pode subir.");
    }

    // Nota: parse sintático Avro NÃO é responsabilidade do hosted service.
    // O hosted service passa o conteúdo string raw para Confluent.SchemaRegistry.Schema
    // sem chamar Schema.Parse. O parse acontece no consumer (classe ISpecificRecord),
    // coberto em EditalPublicadoAvroTests no projeto Selecao.IntegrationTests.

    [Fact(DisplayName = "SocketException é silenciada (transient)")]
    public async Task SocketException_DeveSerSilenciada()
    {
        ISchemaRegistryClient client = Substitute.For<ISchemaRegistryClient>();
        client.RegisterSchemaAsync(Arg.Any<string>(), Arg.Any<SrSchema>(), normalize: true)
            .Throws(new SocketException((int)SocketError.ConnectionRefused));

        SchemaRegistrationHostedService sut = CreateSut(client, ValidRegistration(SubjectFoo));

        Func<Task> act = () => sut.StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "TaskCanceledException com TimeoutException inner é silenciada (timeout HttpClient)")]
    public async Task TaskCanceled_TimeoutInner_DeveSerSilenciada()
    {
        ISchemaRegistryClient client = Substitute.For<ISchemaRegistryClient>();
        TaskCanceledException timeout = new("timed out", new TimeoutException());
        client.RegisterSchemaAsync(Arg.Any<string>(), Arg.Any<SrSchema>(), normalize: true)
            .Throws(timeout);

        SchemaRegistrationHostedService sut = CreateSut(client, ValidRegistration(SubjectFoo));

        Func<Task> act = () => sut.StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "TaskCanceledException com cancellation real (não-default) propaga")]
    public async Task TaskCanceled_CancellationReal_DevePropagar()
    {
        ISchemaRegistryClient client = Substitute.For<ISchemaRegistryClient>();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        TaskCanceledException canceled = new("operation canceled", innerException: null, cts.Token);
        client.RegisterSchemaAsync(Arg.Any<string>(), Arg.Any<SrSchema>(), normalize: true)
            .Throws(canceled);

        SchemaRegistrationHostedService sut = CreateSut(client, ValidRegistration(SubjectFoo));

        Func<Task> act = () => sut.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<TaskCanceledException>(
            because: "cancellation real é semanticamente diferente de timeout — caller pediu interrupção.");
    }
}
