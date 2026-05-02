namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

// Cenário fim-a-fim do fluxo de referência ADR-0005: HTTP request →
// PublicarEditalCommand → handler convention-based produtivo → Edital.Publicar()
// emite EditalPublicadoEvent via AddDomainEvent → handler retorna
// (Result, IEnumerable<object>) com o evento drenado por
// DequeueDomainEvents().Cast<object>() → CaptureCascadingMessages persiste
// envelope na MESMA transação do SaveChanges → listener da queue PG entrega
// ao subscritor (EditalPublicadoSubscriberHandler do teste, que registra no
// coletor; o EditalPublicadoEventHandler produtivo também é invocado pela
// fan-out, executa logging estruturado e não interfere no estado do coletor).
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class PublicarEditalEndpointTests
{
    private readonly CascadingFixture _fixture;

    public PublicarEditalEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "POST /editais/{id}/publicar dispara cascading e entrega EditalPublicadoEvent ao subscritor")]
    public async Task PublicarEdital_FluxoCompleto_DispatchaCascadingMessages()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        DomainEventCollector collector = api.Services.GetRequiredService<DomainEventCollector>();
        collector.Clear();

        Edital edital = await SemearEditalAsync(api);

        HttpResponseMessage response = await client.PostAsync(new Uri($"/api/v1/editais/{edital.Id}/publicar", UriKind.Relative), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        EditalPublicadoEvent? evento = await CascadingScenariosTests.EsperarEventoAsync(
            collector,
            edital.NumeroEdital,
            TimeSpan.FromSeconds(15));

        evento.Should().NotBeNull(
            "o handler produtivo retorna o evento via cascading; o listener PG entrega ao subscritor de teste");
        evento!.EditalId.Should().Be(edital.Id);
        evento.NumeroEdital.Should().Be(edital.NumeroEdital.ToString());

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        Edital? persistido = await db.Editais.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == edital.Id);
        persistido.Should().NotBeNull();
        persistido!.Status.Should().Be(StatusEdital.Publicado);
    }

    [Fact(DisplayName =
        "POST /editais/{id}/publicar retorna 404 quando o edital não existe")]
    public async Task PublicarEdital_QuandoEditalNaoExiste_Retorna404()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        var inexistente = Guid.NewGuid();

        HttpResponseMessage response = await client.PostAsync(new Uri($"/api/v1/editais/{inexistente}/publicar", UriKind.Relative), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        DomainError? erro = await response.Content.ReadFromJsonAsync<DomainError>();
        erro!.Code.Should().Be("Edital.NaoEncontrado");
    }

    [Fact(DisplayName =
        "POST /editais/{id}/publicar é idempotente — segunda chamada retorna 400 com Edital.JaPublicado")]
    public async Task PublicarEdital_QuandoJaPublicado_Retorna400()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        Edital edital = await SemearEditalAsync(api);

        HttpResponseMessage primeira = await client.PostAsync(new Uri($"/api/v1/editais/{edital.Id}/publicar", UriKind.Relative), content: null);
        primeira.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage segunda = await client.PostAsync(new Uri($"/api/v1/editais/{edital.Id}/publicar", UriKind.Relative), content: null);
        segunda.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        DomainError? erro = await segunda.Content.ReadFromJsonAsync<DomainError>();
        erro!.Code.Should().Be("Edital.JaPublicado");
    }

    private static async Task<Edital> SemearEditalAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        // Numero estável derivado de NewGuid — Edital.Numero é int (1..9999),
        // colisão com testes paralelos é evitada pelo CollectionFixture (1
        // fixture por collection, fixture é singleton dentro do test run).
        int numeroSeed = Math.Abs(Guid.NewGuid().GetHashCode() % 9000) + 1;
        Result<NumeroEdital> numeroResult = NumeroEdital.Criar(numero: numeroSeed, ano: 2026);
        numeroResult.IsSuccess.Should().BeTrue();
        Edital edital = Edital.Criar(numeroResult.Value!, "PublicarEditalEndpointTests seed", TipoProcesso.SiSU);
        // O agregado já tem domain events na coleção (do `Criar`/Publicar futuro).
        // Como esta seed bypassa o handler produtivo, drenamos manualmente para
        // não vazar eventos no coletor antes do POST.
        edital.ClearDomainEvents();
        await db.Editais.AddAsync(edital);
        await db.SaveChangesAsync();

        return edital;
    }
}
