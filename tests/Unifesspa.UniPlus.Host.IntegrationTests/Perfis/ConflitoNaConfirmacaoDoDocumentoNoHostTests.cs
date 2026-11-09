namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// A confirmação do documento não tem chave de idempotência: o conflito de concorrência não é capturado e chega ao
/// middleware de exceções, que responde 409 com o código comum de concorrência (ADR-0119).
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class ConflitoNaConfirmacaoDoDocumentoNoHostTests : AprovacaoDaVersaoNoHostTestBase, IDisposable
{
    private readonly ConflitoNaGravacaoDoHost _conflito = new();
    private readonly WebApplicationFactory<HostAssemblyMarker> _host;

    public ConflitoNaConfirmacaoDoDocumentoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
        _host = Fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(_conflito.Instalar));
    }

    [Fact(DisplayName = "O conflito de concorrência na confirmação do documento responde 409 de concorrência e não grava")]
    public async Task Confirmacao_ConflitoDeConcorrencia_Responde409()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        Envio envio = await CriarPerfilAsync(diogo, Consultar);
        string etag = await EnviarPdfAsync(new Uri(envio.UrlDeEnvio, UriKind.Absolute));
        _conflito.Armar(envio.DocumentoId);

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Post, $"{RotaPerfis}/{envio.PerfilId}/versoes/{envio.VersaoId}/documento/confirmacao", new { etag });

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict, corpo);
        corpo.Should().Contain("uniplus.concorrencia.conflito");
        (await SituacaoNoBancoAsync(envio.DocumentoId)).Should().Be("aguardando_envio");
    }

    public void Dispose() => _host.Dispose();

    private protected override HttpClient NovoCliente() => _host.CreateClient();
}
