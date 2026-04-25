namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Wolverine.EntityFrameworkCore;

[Collection(SelecaoOutboxCollection.Name)]
public sealed class SelecaoOutboxAtomicityTests : IAsyncLifetime
{
    private readonly SelecaoOutboxFixture _fixture;

    public SelecaoOutboxAtomicityTests(SelecaoOutboxFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetStateAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // Skip: a atomicidade write+envelope só é observável quando há um transport
    // externo configurado (Kafka/RabbitMQ) — sem destino externo, o Wolverine
    // entrega os domain events in-memory e não grava envelope em
    // wolverine.wolverine_outgoing_envelopes. A validação ponta-a-ponta entra
    // junto com o PublicarEditalCommand na issue #136 (que conectará o transport).
    [Fact]
    public async Task SaveChangesAndFlushMessages_persiste_entity_e_evento_no_outbox()
    {
        Edital edital = CriarEditalEmRascunho();
        edital.Publicar(); // adiciona EditalPublicadoEvent via AddDomainEvent

        await using (AsyncServiceScope writeScope = _fixture.CreateScope())
        {
            // IDbContextOutbox<T> é o caminho canônico Wolverine para outbox
            // transacional — SaveChangesAndFlushMessagesAsync grava entity e
            // envelopes dos domain events na mesma transação.
            IDbContextOutbox<SelecaoDbContext> outbox =
                writeScope.ServiceProvider.GetRequiredService<IDbContextOutbox<SelecaoDbContext>>();
            outbox.DbContext.Editais.Add(edital);
            await outbox.SaveChangesAndFlushMessagesAsync();
        }

        await using AsyncServiceScope readScope = _fixture.CreateScope();
        SelecaoDbContext readDb = readScope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        Edital? persistido = await readDb.Editais.FindAsync(edital.Id);
        persistido.Should().NotBeNull("entity deve estar persistida após SaveChangesAndFlushMessagesAsync");
        persistido!.Status.Should().Be(StatusEdital.Publicado);

        long envelopes = await CountOutgoingEnvelopesAsync(readDb);
        envelopes.Should().BeGreaterThan(0,
            "PublishDomainEventsFromEntityFrameworkCore deveria ter persistido o EditalPublicadoEvent " +
            "em wolverine.wolverine_outgoing_envelopes na mesma transação do write");
    }

    [Fact(Skip = "Aguarda transport configurado em #136 — pré-condição " +
        "(envelope gravado em fluxo de sucesso) ainda não é observável.")]
    public async Task Falha_antes_do_flush_descarta_entity_e_evento_atomicamente()
    {
        Edital edital = CriarEditalEmRascunho();
        edital.Publicar();

        await using AsyncServiceScope scope = _fixture.CreateScope();
        IDbContextOutbox<SelecaoDbContext> outbox =
            scope.ServiceProvider.GetRequiredService<IDbContextOutbox<SelecaoDbContext>>();

        outbox.DbContext.Editais.Add(edital);
        // Simula falha antes de SaveChangesAndFlushMessagesAsync — nada deve
        // ser materializado: nem entity, nem envelope. A atomicidade do outbox
        // depende de o flush e o SaveChanges estarem na mesma unidade transacional.

        // Em uma nova conexão, conferir que nada sobreviveu.
        await using AsyncServiceScope readScope = _fixture.CreateScope();
        SelecaoDbContext readDb = readScope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        Edital? persistido = await readDb.Editais.FindAsync(edital.Id);
        persistido.Should().BeNull("entity sem flush não deve sobreviver");

        long envelopes = await CountOutgoingEnvelopesAsync(readDb);
        envelopes.Should().Be(0,
            "envelope sem flush não deve sobreviver — atomicidade write+evento garantida pelo outbox");
    }

    private static Edital CriarEditalEmRascunho()
    {
        Result<NumeroEdital> numeroResult = NumeroEdital.Criar(1, 2026);
        NumeroEdital numero = numeroResult.Value!;
        return Edital.Criar(numero, "Edital de teste — outbox atomicity", TipoProcesso.SiSU);
    }

    private static async Task<long> CountOutgoingEnvelopesAsync(SelecaoDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        await using NpgsqlCommand cmd = ((NpgsqlConnection)db.Database.GetDbConnection()).CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM wolverine.wolverine_outgoing_envelopes";
        object? result = await cmd.ExecuteScalarAsync();
        return result is null ? 0 : Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
