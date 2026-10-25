namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento.AcervoPublico;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

/// <summary>
/// O envio do documento a publicar é do administrador e usa a credencial geral do armazenamento. Nesta fábrica a
/// credencial do candidato (<c>ArquivosDoCandidato:AccessKey</c>) não existe no MinIO: se o fluxo do administrador
/// a usasse, o armazenamento recusaria a chamada.
/// </summary>
[Collection(AcervoPublicoE2ECollection.Name)]
public sealed class EnvioDoDocumentoDoAdministradorE2ETests(AcervoPublicoE2EFixture fixture)
{
    [Fact(DisplayName = "Iniciar o envio do documento a publicar usa a credencial geral, não a do candidato")]
    public async Task IniciarEnvio_NaoUsaACredencialDoCandidato()
    {
        AcervoPublicoApiFactory api = fixture.Factory;
        Guid processoId;
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
                db, $"PS envio do administrador {Guid.CreateVersion7()}");
            processoId = processo.Id;
        }

        using HttpClient client = api.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri($"/api/selecao/processos-seletivos/{processoId}/documentos-a-publicar", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                finalidade = FinalidadeDoDocumento.DocumentoDoEdital,
                nomeArquivo = "edital.pdf",
                tipoDeMidia = TiposDeMidiaDeDocumento.Pdf,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));

        using HttpResponseMessage resposta = await client.SendAsync(request);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
    }
}
