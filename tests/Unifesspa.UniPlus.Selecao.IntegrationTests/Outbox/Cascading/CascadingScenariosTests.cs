namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Wolverine;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

// Cenários produtivos do outbox cascading (ADR-0005). Equivalentes de
// produção dos cenários V8/V9 do Spike S10 (ver docs/spikes/158-s10-relatorio.md).
//
// Os dois testes têm dupla marcação `Category=OutboxCapability` +
// `Category=OutboxCascading` — evita ambiguidade entre as duas suítes
// agendadas pela #164 e mantém o filtro `OutboxCapability` correspondendo
// ao conjunto demonstrável de capacidades do outbox produtivo nesta fase.
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class CascadingScenariosTests
{
    private readonly CascadingFixture _fixture;

    public CascadingScenariosTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    // V8 — caminho feliz: cascading entrega EditalPublicadoEvent ao handler
    // local da queue PG após Publicar+SaveChanges. Atomicidade write+evento
    // verificada indiretamente: se a transação não tivesse drenado o envelope
    // junto do SaveChanges, o listener da queue PG não receberia o evento.
    [Fact(DisplayName =
        "V8 — cascading entrega EditalPublicadoEvent ao handler local PG após Publicar+SaveChanges")]
    public async Task V8_Cascading_EntregaEvento_AposPublicarEditalViaCommand()
    {
        CascadingApiFactory api = _fixture.Factory;

        using HttpClient _ = api.CreateClient();

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        DomainEventCollector collector = api.Services.GetRequiredService<DomainEventCollector>();
        collector.Clear();

        Result<NumeroEdital> numeroResult = NumeroEdital.Criar(numero: 80, ano: 2026);
        numeroResult.IsSuccess.Should().BeTrue();
        NumeroEdital numero = numeroResult.Value!;

        var command = new PublicarEditalCascadingCommand(
            numero,
            "V8 — cascading happy path",
            TipoProcesso.SiSU);

        await bus.InvokeAsync(command);

        EditalPublicadoEvent? evento = await EsperarEventoAsync(collector, numero, TimeSpan.FromSeconds(15));

        evento.Should().NotBeNull(
            "cascading messages do handler devem ser persistidas pelo PersistOrSendAsync na transação ativa e entregues pelo listener da queue PG");
        evento!.NumeroEdital.Should().Be(numero.ToString());
        evento.EditalId.Should().NotBeEmpty();

        bool persistido = await db.Editais.AsNoTracking()
            .AnyAsync(e => e.Id == evento.EditalId);
        persistido.Should().BeTrue();
    }

    // V9 — rollback: exceção pós-SaveChanges deve eliminar entidade e
    // envelope. O retorno cascading sequer chega a ser executado (throw
    // antes do return); a IEnvelopeTransaction faz o rollback no catch.
    // Esperado: nenhuma linha em editais com o número usado e nenhum
    // envelope em wolverine.wolverine_outgoing_envelopes referenciando o
    // EditalPublicadoEvent dessa instância.
    [Fact(DisplayName =
        "V9 — rollback cascading: exceção pós-SaveChanges deixa entidade ausente e envelope sem registro")]
    public async Task V9_RollbackCascading_DeixaEntidadeEEnvelopeAusentes()
    {
        CascadingApiFactory api = _fixture.Factory;

        using HttpClient _ = api.CreateClient();

        DomainEventCollector collector = api.Services.GetRequiredService<DomainEventCollector>();
        collector.Clear();

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        Result<NumeroEdital> numeroResult = NumeroEdital.Criar(numero: 89, ano: 2026);
        numeroResult.IsSuccess.Should().BeTrue();
        NumeroEdital numero = numeroResult.Value!;

        var command = new FalharAposSaveChangesCascadingCommand(
            numero,
            "V9 — cascading rollback",
            TipoProcesso.SiSU);

        Func<Task> act = () => bus.InvokeAsync(command);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(FalharAposSaveChangesCascadingHandler.MensagemErro);

        bool entidadePersistida = await db.Editais.AsNoTracking()
            .AnyAsync(e =>
                e.NumeroEdital.Numero == numero.Numero &&
                e.NumeroEdital.Ano == numero.Ano);
        entidadePersistida.Should().BeFalse(
            "rollback da transação Wolverine + EF deve eliminar o INSERT do edital");

        int envelopes = await ContarEnvelopesEditalPublicadoAsync(numero);
        envelopes.Should().Be(0,
            "rollback deve eliminar também o envelope persistido em wolverine_outgoing_envelopes");

        EditalPublicadoEvent? evento = await EsperarEventoAsync(
            collector,
            numero,
            TimeSpan.FromSeconds(3));
        evento.Should().BeNull(
            "sem envelope no outbox, o listener não tem o que entregar — collector permanece vazio para esse numero");
    }

    internal static async Task<EditalPublicadoEvent?> EsperarEventoAsync(
        DomainEventCollector collector,
        NumeroEdital numeroEsperado,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        string esperadoTexto = numeroEsperado.ToString();

        while (DateTimeOffset.UtcNow < deadline)
        {
            EditalPublicadoEvent? candidato = collector.Snapshot()
                .FirstOrDefault(e => string.Equals(e.NumeroEdital, esperadoTexto, StringComparison.Ordinal));

            if (candidato is not null)
            {
                return candidato;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150));
        }

        return null;
    }

    private async Task<int> ContarEnvelopesEditalPublicadoAsync(NumeroEdital numero)
    {
        await using NpgsqlConnection conn = new(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM wolverine.wolverine_outgoing_envelopes
            WHERE message_type LIKE '%EditalPublicadoEvent%'
              AND convert_from(body, 'UTF8') LIKE @padrao;
            """;
        cmd.Parameters.AddWithValue("@padrao", $"%{numero.ToString()}%");

        object? raw = await cmd.ExecuteScalarAsync();
        return raw is null ? 0 : Convert.ToInt32(raw, CultureInfo.InvariantCulture);
    }
}
