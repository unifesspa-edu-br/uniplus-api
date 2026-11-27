namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O servidor de teste em memória não aplica o limite do corpo ao ler um corpo sem tamanho declarado; este teste sobe o
/// servidor Kestrel de verdade, o mesmo da implantação, e envia o corpo em partes, sem o cabeçalho de tamanho.
/// </summary>
[Collection(MonolitoHostCollection.Name)]
public sealed class LimiteDoCorpoEmPartesNoKestrelTests(MonolitoPostgresFixture fixture)
{
    private const string Rota = "/api/configuracao/admin/campi";
    private const string Leandro = "leandro.viana";
    private const string Codigo = "uniplus.requisicao.corpo_acima_do_limite";
    private const string NomeDaPessoaFicticia = "Ana Beatriz Lima Ficticia";
    private const int BytesPorParte = 16 * 1024;

    [Fact(DisplayName = "O corpo em partes acima do limite recebe 413 no formato de erro do contrato, sem repetir o corpo")]
    public async Task CorpoEmPartesAcimaDoLimite_Responde413SemRepetirOCorpo()
    {
        using HttpResponseMessage resposta = await EnviarEmPartesAsync(LimiteDoCorpoDoPedido.Bytes + 1);

        resposta.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        string texto = await resposta.Content.ReadAsStringAsync();
        using JsonDocument problema = JsonDocument.Parse(texto);
        problema.RootElement.GetProperty("code").GetString().Should().Be(Codigo);
        problema.RootElement.GetProperty("status").GetInt32().Should().Be((int)HttpStatusCode.RequestEntityTooLarge);
        texto.Should().NotContain(NomeDaPessoaFicticia).And.NotContain("nome");
    }

    [Fact(DisplayName = "O corpo em partes com exatamente o limite é aceito pelo servidor, e não recebe 413")]
    public async Task CorpoEmPartesNoLimiteExato_NaoRecebe413()
    {
        using HttpResponseMessage resposta = await EnviarEmPartesAsync(LimiteDoCorpoDoPedido.Bytes);

        resposta.StatusCode.Should().NotBe(HttpStatusCode.RequestEntityTooLarge);
    }

    private async Task<HttpResponseMessage> EnviarEmPartesAsync(long bytes)
    {
        await ConcessoesDeTeste.ConcederPermissoesAsync(fixture.Factory.Services, Leandro, PermissaoConfiguracao.CampiManter.Codigo);
        await using MonolitoApiFactory kestrel = new(fixture.ConnectionString, wolverineEnabled: false);
        kestrel.UseKestrel();
        kestrel.StartServer();
        using HttpClient client = kestrel.CreateClient();
        using HttpRequestMessage pedido = new(HttpMethod.Post, new Uri(Rota, UriKind.Relative))
        {
            Content = new CorpoEmPartes(JsonComTamanho(bytes)),
        };
        pedido.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, Leandro);

        return await client.SendAsync(pedido);
    }

    private static byte[] JsonComTamanho(long bytes)
    {
        string inicio = $"{{\"nome\":\"{NomeDaPessoaFicticia}\",\"preenchimento\":\"";
        string fim = "\"}";
        return Encoding.UTF8.GetBytes(inicio + new string('x', (int)bytes - inicio.Length - fim.Length) + fim);
    }

    /// <summary>Corpo sem tamanho conhecido: o cliente o envia em partes, sem o cabeçalho de tamanho.</summary>
    private sealed class CorpoEmPartes(byte[] conteudo) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            for (int inicio = 0; inicio < conteudo.Length; inicio += BytesPorParte)
            {
                await stream.WriteAsync(conteudo.AsMemory(inicio, Math.Min(BytesPorParte, conteudo.Length - inicio)));
                await stream.FlushAsync();
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
