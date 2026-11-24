namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento.AcervoPublico;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
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
        Guid processoId = await SemearProcessoAsync(fixture.Factory);
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpRequestMessage pedido = PedidoDeInicioDoEnvio(processoId);

        using HttpResponseMessage resposta = await client.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
    }

    private static async Task<Guid> SemearProcessoAsync(AcervoPublicoApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"PS envio do administrador {Guid.CreateVersion7()}" });

        return processo.Id;
    }

    private static HttpRequestMessage PedidoDeInicioDoEnvio(Guid processoId)
    {
        HttpRequestMessage request = PublicarAtoAvulsoEndpointTests.Autenticado(
            HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/documentos-a-publicar");
        request.Content = JsonContent.Create(new
        {
            finalidade = FinalidadeDoDocumento.DocumentoDoEdital.Codigo(),
            nomeArquivo = "edital.pdf",
            tipoDeMidia = TiposDeMidiaDeDocumento.Pdf,
        });
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));

        return request;
    }
}
