namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using System.Net.Http.Headers;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem.ProcessoDoEnemPublicadoDeTeste;

/// <summary>A importação de um arquivo de retorno do INEP pela API, como administrador da plataforma.</summary>
internal static class ImportacaoDeNotasDeTeste
{
    public static async Task<HttpResponseMessage> ImportarAsync(HttpClient client, Publicado processo, byte[] arquivo)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(processo);

        string rota = $"/api/selecao/processos-seletivos/{processo.ProcessoId}/etapas/{processo.EtapaDoEnemId}/edicoes-do-enem/{Edicao}/lotes-de-notas";
        using MultipartFormDataContent formulario = Formulario(arquivo);
        using HttpRequestMessage pedido = new(HttpMethod.Post, new Uri(rota, UriKind.Relative)) { Content = formulario };
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        return await client.SendAsync(pedido).ConfigureAwait(false);
    }

    private static MultipartFormDataContent Formulario(byte[] arquivo)
    {
        MultipartFormDataContent formulario = new();
        ByteArrayContent conteudo = new(arquivo);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        formulario.Add(conteudo, "arquivo", "retorno.txt");
        formulario.Add(new StringContent("INEP_ENEM_RETORNO"), "leiauteCodigo");
        formulario.Add(new StringContent("v1"), "leiauteVersao");
        return formulario;
    }
}
