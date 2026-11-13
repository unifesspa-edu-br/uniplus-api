namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Providencias;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Concessoes;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O apoio dos testes da providência pelo endpoint real: Cecília Moura é a Encarregada de dados que solicita, Diogo Souza
/// e Isabella Figueiredo administram concessões e Leandro Viana é o beneficiário. Cada teste cria um perfil novo, e a
/// concessão alvo é dele.
/// </summary>
public abstract class ProvidenciaEndpointTestBase : ConcessaoEndpointTestBase
{
    private protected const string RotaDasProvidencias = "/api/autorizacao/providencias";
    private protected const string Fundamento = "O servidor deixou a banca do edital 12/2026 e mantém acesso a documentos sensíveis.";

    private protected static readonly Identidade Cecilia = Pessoa("cecilia.moura");

    private protected ProvidenciaEndpointTestBase(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    private protected Task<HttpClient> ClienteDoEncarregadoAsync() => ClienteDeGovernancaAsync(Cecilia, TipoDeGovernanca.EncarregadoDeDados);

    /// <summary>A concessão do beneficiário (Leandro, sem outro informado) num perfil novo, ativa, e a providência aberta por Cecília sobre ela.</summary>
    private protected async Task<(Guid ConcessaoId, Guid ProvidenciaId)> ProvidenciaAbertaAsync(Identidade? beneficiario = null)
    {
        Guid concessaoId = await ConcederAsync(await CriarPerfilComumAsync(), beneficiario ?? Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
        using HttpClient cecilia = await ClienteDoEncarregadoAsync();
        using HttpResponseMessage resposta = await SolicitarAsync(cecilia, concessaoId);

        return (concessaoId, (await CorpoAsync(resposta, HttpStatusCode.Created)).GetProperty("id").GetGuid());
    }

    private protected static Task<HttpResponseMessage> SolicitarAsync(HttpClient cliente, Guid concessaoId, string? tipo = "retirada", string? fundamento = Fundamento) =>
        EnviarAsync(cliente, HttpMethod.Post, RotaDasProvidencias, new { concessaoId, tipo, fundamento });

    private protected static Task<HttpResponseMessage> ResponderAsync(HttpClient cliente, Guid providenciaId, string? motivo = "O acesso segue necessário até o fim do recurso.") =>
        EnviarAsync(cliente, HttpMethod.Post, $"{RotaDasProvidencias}/{providenciaId}/resposta", new { motivo });

    private protected static Task<HttpResponseMessage> AtenderAsync(HttpClient cliente, Guid providenciaId, string? confirmacaoEmail = EmailDoBeneficiario) =>
        EnviarAsync(cliente, HttpMethod.Post, $"{RotaDasProvidencias}/{providenciaId}/atendimento", new { motivo = Motivo, confirmacaoEmail });

    private protected static async Task<JsonElement> CorpoAsync(HttpResponseMessage resposta, HttpStatusCode esperado)
    {
        string texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(esperado, texto);
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    private protected static async Task ConferirRecusaAsync(HttpResponseMessage resposta, HttpStatusCode status, string codigo)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(status, corpo);
        corpo.Should().Contain(codigo);
    }

    private protected static async Task<Guid[]> FilaAsync(HttpClient cliente, string consulta = "")
    {
        using HttpResponseMessage resposta = await cliente.GetAsync(new Uri(RotaDasProvidencias + consulta, UriKind.Relative));
        JsonElement pagina = await CorpoAsync(resposta, HttpStatusCode.OK);
        JsonElement itens = pagina.ValueKind == JsonValueKind.Array ? pagina : pagina.GetProperty("items");
        return [.. itens.EnumerateArray().Select(static i => i.GetProperty("id").GetGuid())];
    }

    private protected async Task<int> ExecutarSqlAsync(string sql, params object[] parametros)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(sql, parametros);
    }
}
