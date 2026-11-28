namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net.Http.Headers;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>O pedido de criação do processo e a pessoa que o envia, para os testes da criação pelo ponto de entrada.</summary>
internal static class ApoioDaCriacaoDoProcesso
{
    internal const string Rota = "/api/selecao/processos-seletivos";
    internal const string Leandro = "leandro.viana";

    internal static string Corpo(Guid tipoProcessoId, Guid unidadeId, string nome = "PS criação") => $$"""
        {
          "nome": "{{nome}} {{Guid.CreateVersion7():N}}",
          "tipoProcessoOrigemId": "{{tipoProcessoId}}",
          "origemCandidatos": "INSCRICAO_PROPRIA",
          "unidadeAdministradoraOrigemId": "{{unidadeId}}",
          "localidadeCodigoIbge": "1504208",
          "localidadeNome": "Marabá",
          "localidadeUf": "PA",
          "identificadorLegivel": "{{IdentificadoresDeTeste.NovoValor()}}"
        }
        """;

    internal static async Task<HttpResponseMessage> PostAsync(HttpClient client, string corpo, string? chave = null)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(Rota, UriKind.Relative))
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", chave ?? Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }

    // O token só identifica a pessoa; sem papel algum, quem lhe dá a permissão é a concessão gravada.
    internal static HttpClient ClienteDe(CascadingFixture fixture, bool semJti = false)
    {
        HttpClient client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Leandro);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "Admin Test");
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "admin@unifesspa.edu.br");
        if (semJti)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.SemJtiHeader, "1");
        }

        return client;
    }

    internal static async Task ConcederPermissaoNaUnidadeAsync(CascadingFixture fixture, Guid unidadeId)
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(fixture.Factory.Services, PermissaoSelecao.ProcessosConfigurar.Codigo);
        await ConcessoesDeTeste.ConcederAsync(fixture.Factory.Services, Leandro, perfil, EscopoDaConcessao.DaUnidade(unidadeId));
    }

    internal static async Task<(Guid UnidadeId, Guid TipoProcessoId)> SemearAsync(CascadingFixture fixture)
    {
        Guid unidadeId = await SemearUnidadeAsync(fixture);
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext config = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Guid tipoProcessoId = await config.TiposProcesso
            .Where(tipo => tipo.Codigo == "SISU" && tipo.Ativo)
            .Select(tipo => tipo.Id)
            .SingleAsync();
        return (unidadeId, tipoProcessoId);
    }

    private static async Task<Guid> SemearUnidadeAsync(CascadingFixture fixture)
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8];
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext org = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Unidade unidade = Unidade.Criar(
            "Centro de Processos Seletivos — criação",
            new IdentificadoresDaUnidade($"ceps-criacao-{sufixo}", $"CEPSN{sufixo}", $"CEPSN-{sufixo}", null),
            new CidadeDaUnidade("1504208", "Marabá", "PA"),
            new VigenciaDaUnidade(new DateOnly(2020, 1, 1), null),
            null, TipoUnidade.Centro, true).Value!;
        org.Unidades.Add(unidade);
        await org.SaveChangesAsync();
        return unidade.Id;
    }
}
