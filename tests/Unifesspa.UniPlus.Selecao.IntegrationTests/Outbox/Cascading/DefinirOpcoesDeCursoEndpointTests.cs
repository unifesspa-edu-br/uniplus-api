namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O endpoint <c>PUT /processos-seletivos/{id}/opcoes-de-curso</c>, fim a fim pelo HTTP: o número
/// declarado é gravado, e a invariante contra as ofertas da distribuição chega ao cliente como 422
/// nomeado no campo <c>numero</c>.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class DefinirOpcoesDeCursoEndpointTests
{
    private readonly CascadingFixture _fixture;

    public DefinirOpcoesDeCursoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "1 opção sobre a distribuição de 1 oferta é aceita com 204 e gravada")]
    public async Task UmaOpcaoComUmaOferta_204EGrava()
    {
        (CascadingApiFactory api, Guid processoId) = await SemearRascunhoAsync(nameof(UmaOpcaoComUmaOferta_204EGrava));

        HttpResponseMessage resposta = await PutAsync(api, processoId, new { numero = 1 });

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerNumeroGravadoAsync(api, processoId)).Should().Be(1);
    }

    [Fact(DisplayName = "2 opções sobre a distribuição de 1 oferta são recusadas com 422 no campo numero, e nada é gravado")]
    public async Task DuasOpcoesComUmaOferta_422NoCampoNumero()
    {
        (CascadingApiFactory api, Guid processoId) = await SemearRascunhoAsync(nameof(DuasOpcoesComUmaOferta_422NoCampoNumero));

        HttpResponseMessage resposta = await PutAsync(api, processoId, new { numero = 2 });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain("uniplus.selecao.processo_seletivo.opcoes_de_curso_acima_das_ofertas");
        corpo.Should().Contain("Com 2 opções de curso, cadastre pelo menos dois cursos.");
        corpo.Should().Contain("numero", "a recusa aponta o campo do corpo que o operador corrige");
        (await LerNumeroGravadoAsync(api, processoId)).Should().Be(1, "a recusa não grava nada: fica a declaração da semeadura");
    }

    [Fact(DisplayName = "Número fora de 1 e 2 é recusado com 422 nomeado")]
    public async Task NumeroForaDeUmEDois_422()
    {
        (CascadingApiFactory api, Guid processoId) = await SemearRascunhoAsync(nameof(NumeroForaDeUmEDois_422));

        HttpResponseMessage resposta = await PutAsync(api, processoId, new { numero = 3 });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.selecao.processo_seletivo.opcoes_de_curso_invalido");
    }

    [Fact(DisplayName = "Corpo sem numero é rejeitado com 400, não com 422 de um zero implícito")]
    public async Task SemNumero_400()
    {
        (CascadingApiFactory api, Guid processoId) = await SemearRascunhoAsync(nameof(SemNumero_400));

        HttpResponseMessage resposta = await PutAsync(api, processoId, new { });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LerNumeroGravadoAsync(api, processoId)).Should().Be(1);
    }

    private static async Task<HttpResponseMessage> PutAsync(CascadingApiFactory api, Guid processoId, object corpo)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Put,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/opcoes-de-curso", UriKind.Relative))
        {
            Content = JsonContent.Create(corpo),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await api.CreateClient().SendAsync(request).ConfigureAwait(false);
    }

    private static async Task<int?> LerNumeroGravadoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        return await db.Set<ProcessoSeletivo>().AsNoTracking()
            .Where(p => p.Id == processoId)
            .Select(p => p.NumeroDeOpcoesDeCurso)
            .SingleAsync();
    }

    private async Task<(CascadingApiFactory Api, Guid ProcessoId)> SemearRascunhoAsync(string nome)
    {
        CascadingApiFactory api = _fixture.Factory;
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
        return (api, processo.Id);
    }
}
