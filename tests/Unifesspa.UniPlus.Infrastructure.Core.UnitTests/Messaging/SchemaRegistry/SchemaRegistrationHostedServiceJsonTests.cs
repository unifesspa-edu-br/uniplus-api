namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry;

using System.Net.Sockets;

using AwesomeAssertions;

using Confluent.SchemaRegistry;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

using SrSchema = Confluent.SchemaRegistry.Schema;

/// <summary>
/// A subida com JSON Schema: indisponibilidade do Schema Registry não trava o host, entrega o registro à nova tentativa,
/// e o Avro continua sem repetição.
/// </summary>
public sealed class SchemaRegistrationHostedServiceJsonTests : IAsyncLifetime, IDisposable
{
    private const string Prefixo = "Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry.Resources.";

    private readonly ISchemaRegistryClient _schemaRegistry = Substitute.For<ISchemaRegistryClient>();
    private readonly ILeitorDaRegraDeCompatibilidadeDoSubject _leitor = Substitute.For<ILeitorDaRegraDeCompatibilidadeDoSubject>();
    private readonly RelogioObservavel _relogio = new();
    private readonly RegistradorDeSchema _registrador;
    private readonly RepetidorDoRegistroDeSchema _repetidor;

    public SchemaRegistrationHostedServiceJsonTests()
    {
        _leitor.LerRegraDeCompatibilidadeEfetivaAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Compatibility.None);
        _registrador = new RegistradorDeSchema(_schemaRegistry, _leitor, NullLogger<RegistradorDeSchema>.Instance);
        _repetidor = new RepetidorDoRegistroDeSchema(_registrador, _relogio, NullLogger<RepetidorDoRegistroDeSchema>.Instance);
    }

    public Task InitializeAsync() => _repetidor.StartAsync(default);

    public Task DisposeAsync() => _repetidor.StopAsync(default);

    public void Dispose() => _repetidor.Dispose();

    [Fact(DisplayName = "JSON Schema com o Schema Registry fora: a subida não trava nem espera, e o registro é refeito depois")]
    public async Task JsonComRegistryFora_SobeERepete()
    {
        Registrar().Throws(new SocketException((int)SocketError.ConnectionRefused));
        SchemaRegistrationHostedService servico = Servico(Json("doc-v1"));

        await servico.StartAsync(default);
        Chamadas().Should().Be(1, "a subida tentou uma vez e seguiu");

        Registrar().Returns(5);
        await _relogio.DispararAsync(RepetidorDoRegistroDeSchema.EsperaInicial, Chamadas);

        Chamadas().Should().Be(2, "a nova tentativa registrou assim que o Schema Registry voltou");
    }

    [Fact(DisplayName = "Avro com o Schema Registry fora não ganha repetição: continua como era")]
    public async Task AvroComRegistryFora_NaoRepete()
    {
        Registrar().Throws(new SocketException((int)SocketError.ConnectionRefused));
        SchemaRegistration avro = new("evento-value", Prefixo + "TestSchema.avsc", typeof(RepetidorDoRegistroDeSchemaTests).Assembly);

        await Servico(avro).StartAsync(default);
        await _relogio.AvancarSemEfeitoAsync(RepetidorDoRegistroDeSchema.EsperaMaxima * 3);

        Chamadas().Should().Be(1);
    }

    [Fact(DisplayName = "Subject do JSON Schema com regra diferente de NONE trava a subida")]
    public async Task RegraDiferente_TravaASubida()
    {
        _leitor.LerRegraDeCompatibilidadeEfetivaAsync("doc-v1", Arg.Any<CancellationToken>()).Returns(Compatibility.Backward);

        Func<Task> acao = () => Servico(Json("doc-v1")).StartAsync(default);

        await acao.Should().ThrowAsync<InvalidOperationException>();
        Chamadas().Should().Be(0);
    }

    [Fact(DisplayName = "Um JSON Schema indisponível não impede os demais de registrar na subida")]
    public async Task UmIndisponivel_NaoImpedeOsDemais()
    {
        _schemaRegistry.RegisterSchemaAsync("a-v1", Arg.Any<SrSchema>(), normalize: true).Throws(new SocketException((int)SocketError.ConnectionRefused));
        _schemaRegistry.RegisterSchemaAsync("b-v1", Arg.Any<SrSchema>(), normalize: true).Returns(3);

        await Servico(Json("a-v1"), Json("b-v1")).StartAsync(default);

        await _schemaRegistry.Received(1).RegisterSchemaAsync("b-v1", Arg.Any<SrSchema>(), normalize: true);
    }

    private SchemaRegistrationHostedService Servico(params SchemaRegistration[] registros) =>
        new(_registrador, _repetidor, registros, NullLogger<SchemaRegistrationHostedService>.Instance);

    private Task<int> Registrar() => _schemaRegistry.RegisterSchemaAsync(Arg.Any<string>(), Arg.Any<SrSchema>(), normalize: true);

    private int Chamadas() =>
        _schemaRegistry.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ISchemaRegistryClient.RegisterSchemaAsync));

    private static SchemaRegistration Json(string subject) =>
        new(subject, Prefixo + "DocumentoDeTeste.schema.json", typeof(RepetidorDoRegistroDeSchemaTests).Assembly) { Formato = FormatoDoSchema.JsonSchema };
}
