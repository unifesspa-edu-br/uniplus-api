namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A leitura de um registro da Seleção que não existe responde 404 no formato de problema (ADR-0023), com o código
/// público do assunto, e não com o corpo em inglês que o MVC escreve para um 404 sem corpo próprio.
/// </summary>
[Collection(MonolitoHostCollection.Name)]
public sealed class RespostasDeNaoEncontradoDaSelecaoTests(MonolitoPostgresFixture fixture)
{
    private const string Prefixo = "/api/selecao";

    [Fact(DisplayName = "Motivo de decisão inexistente responde 404 com o código do motivo não encontrado, sem repetir o identificador enviado")]
    public async Task MotivoDeDecisaoInexistente_Responde404ComCodigo()
    {
        Guid id = Guid.CreateVersion7();

        string corpo = await AfirmarNaoEncontradoAsync(
            $"{Prefixo}/motivos-de-decisao/{id}", "motivo-de-decisao", "uniplus.selecao.motivo_de_decisao.nao_encontrado");

        corpo.Should().NotContain(id.ToString());
    }

    [Fact(DisplayName = "Obrigatoriedade legal inexistente responde 404 com o código da regra não encontrada, sem repetir o identificador enviado")]
    public async Task ObrigatoriedadeLegalInexistente_Responde404ComCodigo()
    {
        Guid id = Guid.CreateVersion7();

        string corpo = await AfirmarNaoEncontradoAsync(
            $"{Prefixo}/obrigatoriedades-legais/{id}", "obrigatoriedade-legal", "uniplus.selecao.obrigatoriedade_legal.nao_encontrada");

        corpo.Should().NotContain(id.ToString());
    }

    [Fact(DisplayName = "Processo seletivo inexistente responde 404 com o código do processo não encontrado")]
    public async Task ProcessoSeletivoInexistente_Responde404ComCodigo() =>
        await AfirmarNaoEncontradoAsync(
            $"{Prefixo}/processos-seletivos/{Guid.CreateVersion7()}", "processo-seletivo", "uniplus.selecao.processo_seletivo.nao_encontrado");

    [Fact(DisplayName = "Conformidade de processo seletivo inexistente responde 404 com o código do processo não encontrado")]
    public async Task ConformidadeDeProcessoSeletivoInexistente_Responde404ComCodigo() =>
        await AfirmarNaoEncontradoAsync(
            $"{Prefixo}/processos-seletivos/{Guid.CreateVersion7()}/conformidade",
            "conformidade-processo-seletivo",
            "uniplus.selecao.processo_seletivo.nao_encontrado");

    private async Task<string> AfirmarNaoEncontradoAsync(string rota, string recurso, string codigoEsperado)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage pedido = PedidoDeLeitura(rota, recurso);

        using HttpResponseMessage resposta = await client.SendAsync(pedido);

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound, corpo);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using JsonDocument documento = JsonDocument.Parse(corpo);
        documento.RootElement.GetProperty("code").GetString().Should().Be(codigoEsperado);
        return corpo;
    }

    private static HttpRequestMessage PedidoDeLeitura(string rota, string recurso)
    {
        HttpRequestMessage pedido = new(HttpMethod.Get, new Uri(rota, UriKind.Relative));
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.Add(TestAuthHandler.UserIdHeader, "pessoa.de.teste");
        pedido.Headers.Add(TestAuthHandler.NameHeader, "Pessoa de teste");
        pedido.Headers.Add(TestAuthHandler.EmailHeader, "pessoa@uniplus-test.local");
        pedido.Headers.Accept.ParseAdd($"application/vnd.uniplus.{recurso}.v1+json");
        return pedido;
    }
}
