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
/// A repetição em segundo plano do registro de JSON Schema, com relógio controlado: espera que dobra até o teto, para ao
/// confirmar, no encerramento do host e na falha determinística, e uma repetição não atrasa a outra.
/// </summary>
public sealed class RepetidorDoRegistroDeSchemaTests : IAsyncLifetime, IDisposable
{
    private const string Recurso = "Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry.Resources.DocumentoDeTeste.schema.json";

    private readonly ISchemaRegistryClient _schemaRegistry = Substitute.For<ISchemaRegistryClient>();
    private readonly RelogioObservavel _relogio = new();
    private readonly RepetidorDoRegistroDeSchema _repetidor;

    public RepetidorDoRegistroDeSchemaTests()
    {
        ILeitorDaRegraDeCompatibilidadeDoSubject leitor = Substitute.For<ILeitorDaRegraDeCompatibilidadeDoSubject>();
        leitor.LerRegraDeCompatibilidadeEfetivaAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Compatibility.None);
        RegistradorDeSchema registrador = new(_schemaRegistry, leitor, NullLogger<RegistradorDeSchema>.Instance);
        _repetidor = new RepetidorDoRegistroDeSchema(registrador, _relogio, NullLogger<RepetidorDoRegistroDeSchema>.Instance);
    }

    public Task InitializeAsync() => _repetidor.StartAsync(default);

    public Task DisposeAsync() => _repetidor.StopAsync(default);

    public void Dispose() => _repetidor.Dispose();

    [Fact(DisplayName = "O registro adiado é tentado de novo depois da espera inicial, e a repetição para ao confirmar")]
    public async Task RepeteDepoisDaEsperaInicial_EParaAoConfirmar()
    {
        Registrar("a").Returns(1);

        _repetidor.Agendar(Json("a"));
        await _relogio.AdiantarAsync(RepetidorDoRegistroDeSchema.EsperaInicial - TimeSpan.FromMilliseconds(1));
        Chamadas("a").Should().Be(0, "ainda não passou a espera inicial");

        await Disparar(TimeSpan.FromMilliseconds(1));
        await _relogio.AvancarSemEfeitoAsync(TimeSpan.FromHours(1));

        Chamadas("a").Should().Be(1, "confirmou, então não repete mais");
    }

    [Fact(DisplayName = "A espera dobra a cada falha e nunca passa do teto")]
    public async Task EsperaDobraAteOTeto()
    {
        Registrar("a").Throws(new SocketException((int)SocketError.ConnectionRefused));
        TimeSpan[] esperas = [.. Enumerable.Range(0, 7).Select(i => Doubled(i))];

        _repetidor.Agendar(Json("a"));
        for (int i = 0; i < esperas.Length; i++)
        {
            await _relogio.AdiantarAsync(esperas[i] - TimeSpan.FromMilliseconds(1));
            Chamadas("a").Should().Be(i, $"a tentativa {i + 1} só vem depois de {esperas[i]}");
            await Disparar(TimeSpan.FromMilliseconds(1));
        }

        Chamadas("a").Should().Be(esperas.Length);
        esperas[^1].Should().Be(RepetidorDoRegistroDeSchema.EsperaMaxima, "a sétima espera já está no teto");
    }

    [Fact(DisplayName = "Falha determinística durante a repetição não é repetida")]
    public async Task FalhaDeterministica_NaoRepete()
    {
        Registrar("a").Throws(new SchemaRegistryException("incompatível", System.Net.HttpStatusCode.Conflict, errorCode: 409));

        _repetidor.Agendar(Json("a"));
        await Disparar(RepetidorDoRegistroDeSchema.EsperaInicial);
        await _relogio.AvancarSemEfeitoAsync(RepetidorDoRegistroDeSchema.EsperaMaxima * 3);

        Chamadas("a").Should().Be(1);
    }

    [Fact(DisplayName = "Um registro que continua falhando não atrasa a confirmação dos outros")]
    public async Task UmFalhando_NaoAtrasaOsOutros()
    {
        Registrar("a").Throws(new SocketException((int)SocketError.ConnectionRefused));
        Registrar("b").Returns(2);

        _repetidor.Agendar(Json("a"));
        _repetidor.Agendar(Json("b"));
        await Disparar(RepetidorDoRegistroDeSchema.EsperaInicial, pendentes: 2);

        Chamadas("b").Should().Be(1);
        Chamadas("a").Should().Be(1);
    }

    [Fact(DisplayName = "Encerrar o host interrompe a repetição pendente sem lançar")]
    public async Task EncerrarOHost_InterrompeSemLancar()
    {
        Registrar("a").Throws(new SocketException((int)SocketError.ConnectionRefused));
        _repetidor.Agendar(Json("a"));
        await Disparar(RepetidorDoRegistroDeSchema.EsperaInicial);

        Func<Task> encerrar = () => _repetidor.StopAsync(default);

        await encerrar.Should().NotThrowAsync();
        int antes = Chamadas("a");
        await _relogio.AvancarSemEfeitoAsync(RepetidorDoRegistroDeSchema.EsperaMaxima * 2);
        Chamadas("a").Should().Be(antes, "depois do encerramento nada mais é tentado");
    }

    private static TimeSpan Doubled(int indice) =>
        TimeSpan.FromTicks(Math.Min(RepetidorDoRegistroDeSchema.EsperaInicial.Ticks << indice, RepetidorDoRegistroDeSchema.EsperaMaxima.Ticks));

    private Task Disparar(TimeSpan intervalo, int pendentes = 1) => _relogio.DispararAsync(intervalo, TotalDeChamadas, pendentes);

    private int TotalDeChamadas() =>
        _schemaRegistry.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ISchemaRegistryClient.RegisterSchemaAsync));

    private Task<int> Registrar(string subject) => _schemaRegistry.RegisterSchemaAsync(subject, Arg.Any<SrSchema>(), normalize: true);

    private int Chamadas(string subject) =>
        _schemaRegistry.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ISchemaRegistryClient.RegisterSchemaAsync)
            && (string)c.GetArguments()[0]! == subject);

    private static SchemaRegistration Json(string subject) =>
        new(subject, Recurso, typeof(RepetidorDoRegistroDeSchemaTests).Assembly) { Formato = FormatoDoSchema.JsonSchema };
}
