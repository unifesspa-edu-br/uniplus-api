namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Messaging;

using System.Globalization;
using System.Text;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using Wolverine;
using Wolverine.Runtime;
using Wolverine.Runtime.Serialization;
using Wolverine.Transports;

/// <summary>
/// O contrato de instantes das mensagens na fila durável <c>domain-events</c> (PostgreSQL), com o corpo gravado na
/// tabela da fila como o transporte o grava: o instante chega em microssegundos, e a mensagem sem
/// o instante obrigatório vai para a carta morta sem parar o consumo da seguinte.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class ContratoDeInstantesNaFilaDuravelTests
{
    private const string TabelaDaFila = "wolverine_queues.wolverine_queue_domain_events";
    private static readonly Uri EnderecoDaFila = new("postgresql://domain_events/");
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(30);

    private readonly CascadingFixture _fixture;

    public ContratoDeInstantesNaFilaDuravelTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "O evento com o instante de 7 casas chega ao consumidor com 6")]
    public async Task Evento_InstanteComSeteCasas_ChegaComSeis()
    {
        Guid processoId = Guid.CreateVersion7();

        await EnviarCorpoAsync(Guid.CreateVersion7(), CorpoDoEvento(processoId, "2026-11-12T10:00:00.1234567+00:00"));

        ProcessoPublicadoEvent? recebido = await EsperarEventoAsync(processoId);

        recebido.Should().NotBeNull("a mensagem válida é entregue ao consumidor da fila");
        recebido!.OccurredOn.Should().Be(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero).AddTicks(1_234_560));
    }

    [Fact(DisplayName = "O evento sem o instante vai para a carta morta com a causa e a mensagem seguinte é processada")]
    public async Task Evento_SemInstante_VaiParaACartaMortaEAProximaEProcessada()
    {
        Guid idSemInstante = Guid.CreateVersion7();
        Guid processoSemInstante = Guid.CreateVersion7();
        Guid processoSeguinte = Guid.CreateVersion7();

        await EnviarCorpoAsync(idSemInstante, CorpoDoEvento(processoSemInstante, occurredOn: null));
        await EnviarCorpoAsync(Guid.CreateVersion7(), CorpoDoEvento(processoSeguinte, "2026-11-12T10:00:00+00:00"));

        (await EsperarEventoAsync(processoSeguinte)).Should().NotBeNull("a mensagem válida seguinte é processada");
        await ConferirRecusaAsync(idSemInstante, processoSemInstante);
    }

    private async Task ConferirRecusaAsync(Guid idDoEnvelope, Guid processoId)
    {
        CartaMorta? morta = await EsperarCartaMortaAsync(idDoEnvelope);
        morta.Should().NotBeNull("a mensagem sem o instante obrigatório é registrada na carta morta");
        morta!.Causa.Should().Contain("occurredOn");
        morta.TipoDaExcecao.Should().Be("System.Text.Json.JsonException");
        _fixture.Factory.Services.GetRequiredService<DomainEventCollector>().Snapshot()
            .Should().NotContain(e => e.ProcessoSeletivoId == processoId, "a mensagem recusada não chega ao consumidor");
        ListenerDaFila().Status.Should().Be(ListeningStatus.Accepting);
    }

    private static byte[] CorpoDoEvento(Guid processoId, string? occurredOn)
    {
        string instante = occurredOn is null ? string.Empty : $"\"occurredOn\":\"{occurredOn}\",";
        string json = string.Create(
            CultureInfo.InvariantCulture,
            $"{{\"processoSeletivoId\":\"{processoId}\",\"editalId\":\"{Guid.CreateVersion7()}\",\"snapshotPublicacaoId\":\"{Guid.CreateVersion7()}\",\"hashConfiguracao\":\"{new string('a', 64)}\",\"hashEdital\":\"{new string('b', 64)}\",{instante}\"eventId\":\"{Guid.CreateVersion7()}\"}}");

        return Encoding.UTF8.GetBytes(json);
    }

    private async Task EnviarCorpoAsync(Guid idDoEnvelope, byte[] corpo)
    {
        Envelope envelope = new()
        {
            Id = idDoEnvelope,
            MessageType = typeof(ProcessoPublicadoEvent).FullName,
            ContentType = "application/json",
            Data = corpo,
            Destination = EnderecoDaFila,
        };

        await InserirNaFilaAsync(envelope);
    }

    private async Task InserirNaFilaAsync(Envelope envelope)
    {
        await using NpgsqlConnection banco = new(_fixture.ConnectionString);
        await banco.OpenAsync();
        await using NpgsqlCommand insercao = new(
            $"INSERT INTO {TabelaDaFila} (id, body, message_type, keep_until, \"timestamp\") VALUES (@id, @corpo, @tipo, NULL, clock_timestamp())", banco);
        insercao.Parameters.AddWithValue("id", envelope.Id);
        insercao.Parameters.AddWithValue("corpo", EnvelopeSerializer.Serialize(envelope));
        insercao.Parameters.AddWithValue("tipo", envelope.MessageType!);
        await insercao.ExecuteNonQueryAsync();
    }

    private async Task<ProcessoPublicadoEvent?> EsperarEventoAsync(Guid processoId) =>
        await CascadingScenariosTests.EsperarEventoAsync(
            _fixture.Factory.Services.GetRequiredService<DomainEventCollector>(), processoId, Espera);

    private async Task<CartaMorta?> EsperarCartaMortaAsync(Guid idDoEnvelope)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow + Espera;
        while (DateTimeOffset.UtcNow < limite)
        {
            if (await LerCartaMortaAsync(idDoEnvelope) is { } morta)
            {
                return morta;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        return null;
    }

    private async Task<CartaMorta?> LerCartaMortaAsync(Guid idDoEnvelope)
    {
        await using NpgsqlConnection banco = new(_fixture.ConnectionString);
        await banco.OpenAsync();
        await using NpgsqlCommand consulta = new(
            "SELECT exception_type, exception_message FROM wolverine.wolverine_dead_letters WHERE id = @id", banco);
        consulta.Parameters.AddWithValue("id", idDoEnvelope);
        await using NpgsqlDataReader leitor = await consulta.ExecuteReaderAsync();

        return await leitor.ReadAsync() ? new CartaMorta(leitor.GetString(0), leitor.GetString(1)) : null;
    }

    private IListeningAgent ListenerDaFila() =>
        _fixture.Factory.Services.GetRequiredService<IWolverineRuntime>().Endpoints.FindListeningAgent(EnderecoDaFila)
            ?? throw new InvalidOperationException("O consumidor da fila domain-events não foi encontrado.");

    private sealed record CartaMorta(string TipoDaExcecao, string Causa);
}
