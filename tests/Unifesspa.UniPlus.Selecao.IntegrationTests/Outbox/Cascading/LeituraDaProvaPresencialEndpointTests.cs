namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
public sealed class LeituraDaProvaPresencialEndpointTests(CascadingFixture fixture)
{
    [Fact]
    public async Task Get_DeclaracaoEPublicacao_DevolveMunicipiosNaOrdemComNomesCopiados()
    {
        await TiposDeAtoSeeder.SemearAsync(fixture.Factory.Services);
        (Guid id, Guid documento) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(fixture.Factory, "Leitura da prova");
        await LimparDeclaracaoAsync(id);
        await using WebApplicationFactory<HostAssemblyMarker> host = HostComGeo();
        using HttpClient client = host.CreateClient();
        (await LerAsync(client, id)).ProvaPresencial.Should().BeNull();
        await DeclararAsync(client, id);
        Conferir(await LerAsync(client, id), null, null);
        using HttpResponseMessage publicacao = await PublicacaoPeloEndpointDeTeste.PublicarAsync(client, id, documento);
        publicacao.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicacao.Content.ReadAsStringAsync());
        Conferir(await LerAsync(client, id), "Xinguara", "Marabá");
    }

    private async Task LimparDeclaracaoAsync(Guid id)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.SingleAsync(p => p.Id == id);
        db.Entry(processo).Reference(p => p.ProvaPresencial).CurrentValue = null;
        await db.SaveChangesAsync();
    }

    private WebApplicationFactory<HostAssemblyMarker> HostComGeo() => fixture.Factory.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IConferenciaEnderecoGeo>();
            services.AddSingleton<IConferenciaEnderecoGeo>(new FakeConferenciaEnderecoGeo()
                .ComMunicipio("1508159", "PA", "Xinguara").ComMunicipio("1504208", "PA", "Marabá"));
        }));

    private static async Task DeclararAsync(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = Request(HttpMethod.Put, id);
        request.RequestUri = new Uri($"/api/selecao/processos-seletivos/{id}/prova-presencial", UriKind.Relative);
        request.Content = JsonContent.Create(new
        {
            temProvaPresencial = true,
            municipios = new[]
        {
            new { codigoIbge = "1508159", uf = "PA" }, new { codigoIbge = "1504208", uf = "PA" },
        }
        });
        using HttpResponseMessage resposta = await client.SendAsync(request);
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
    }

    private static async Task<ProcessoSeletivoDto> LerAsync(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = Request(HttpMethod.Get, id);
        request.Headers.Accept.ParseAdd("application/vnd.uniplus.processo-seletivo.v1+json");
        using HttpResponseMessage resposta = await client.SendAsync(request);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await resposta.Content.ReadFromJsonAsync<ProcessoSeletivoDto>(JsonDoContrato.Opcoes))!;
    }

    private static HttpRequestMessage Request(HttpMethod method, Guid id)
    {
        HttpRequestMessage request = new(method, new Uri($"/api/selecao/processos-seletivos/{id}", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return request;
    }

    private static void Conferir(ProcessoSeletivoDto dto, string? primeiro, string? segundo)
    {
        dto.ProvaPresencial.Should().NotBeNull();
        dto.ProvaPresencial!.TemProvaPresencial.Should().BeTrue();
        dto.ProvaPresencial.Municipios.Should().Equal(
            new MunicipioDeProvaDto("1508159", "PA", primeiro), new MunicipioDeProvaDto("1504208", "PA", segundo));
    }
}
