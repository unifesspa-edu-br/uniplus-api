namespace Unifesspa.UniPlus.Selecao.IntegrationTests.InstantesEmMicrossegundos;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O envio de um arquivo do candidato pelo fluxo real (inscrição, início do envio e confirmação), com um relógio que entrega a
/// 7ª casa decimal: a validade do envio e o instante da próxima publicação saem em microssegundos inteiros.
/// </summary>
[Collection(ColecaoInstantesEmResiduo.Name)]
public sealed class ExpiracaoDoArquivoDoCandidatoComResiduoEndpointTests
{
    private const string Dono = "sub-instantes-em-residuo";

    private readonly InstantesEmResiduoFixture _fixture;

    public ExpiracaoDoArquivoDoCandidatoComResiduoEndpointTests(InstantesEmResiduoFixture fixture) => _fixture = fixture;

    private InstantesEmResiduoApiFactory Fabrica => _fixture.Factory;

    [Fact(DisplayName = "Iniciar o envio do arquivo do candidato devolve e grava a validade em microssegundos inteiros")]
    public async Task IniciarEnvio_ValidadeEmMicrossegundos()
    {
        (Guid processoId, Guid exigenciaId) = await PrepararInscricaoAsync();

        JsonElement envio = await IniciarEnvioNoInstanteComResiduoAsync(processoId, exigenciaId);

        DateTimeOffset esperado = InstantesDeTeste.EmMicrossegundos + TimeSpan.FromSeconds(900);
        envio.GetProperty("expiraEm").GetDateTimeOffset().Should().Be(esperado);
        ArquivoDaApresentacao gravado = await LerArquivoAsync(envio.GetProperty("arquivoId").GetGuid());
        gravado.ExpiraEm.Should().Be(esperado);
    }

    [Fact(DisplayName = "Confirmar o envio grava o instante da próxima publicação em microssegundos inteiros")]
    public async Task ConfirmarEnvio_ProximaPublicacaoEmMicrossegundos()
    {
        (Guid processoId, Guid exigenciaId) = await PrepararInscricaoAsync();
        Guid arquivoId = (await IniciarEnvioNoInstanteComResiduoAsync(processoId, exigenciaId)).GetProperty("arquivoId").GetGuid();

        using HttpResponseMessage confirmacao = await EnviarComoCandidatoAsync(
            HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/inscricao/documentos/envios/{arquivoId}/confirmacao", null, new { etag = "\"etag-de-teste\"" });

        confirmacao.StatusCode.Should().Be(HttpStatusCode.OK, await confirmacao.Content.ReadAsStringAsync());
        ArquivoDaApresentacao gravado = await LerArquivoAsync(arquivoId);
        gravado.ProximaPublicacaoEm.Should().Be(InstantesDeTeste.EmMicrossegundos + PoliticaDeRepublicacao.Padrao.PrazoApos(1));
    }

    private async Task<(Guid ProcessoId, Guid ExigenciaId)> PrepararInscricaoAsync()
    {
        (Guid processoId, Guid exigenciaId) = await ProcessoPublicadoComExigenciaDeDocumento.PublicarAsync(Fabrica);
        using HttpResponseMessage rascunho = await EnviarComoCandidatoAsync(
            HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/inscricao", "inscricao", null);
        rascunho.StatusCode.Should().Be(HttpStatusCode.OK, await rascunho.Content.ReadAsStringAsync());
        return (processoId, exigenciaId);
    }

    private async Task<JsonElement> IniciarEnvioNoInstanteComResiduoAsync(Guid processoId, Guid exigenciaId)
    {
        Fabrica.Relogio.ParaNo(InstantesDeTeste.ComResiduoDeTicks);
        using HttpResponseMessage resposta = await EnviarComoCandidatoAsync(
            HttpMethod.Post,
            $"/api/selecao/processos-seletivos/{processoId}/inscricao/documentos/envios",
            null,
            new { exigenciaId, tipoDeMidia = "application/pdf", tamanhoBytes = 1024 });

        string texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, texto);
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    private async Task<HttpResponseMessage> EnviarComoCandidatoAsync(HttpMethod metodo, string rota, string? recurso, object? corpo = null)
    {
        using HttpClient client = Fabrica.CreateClient();
        using HttpRequestMessage pedido = PedidoDoCandidato(metodo, rota, recurso);
        if (corpo is not null)
        {
            pedido.Content = JsonContent.Create(corpo);
        }

        return await client.SendAsync(pedido);
    }

    private static HttpRequestMessage PedidoDoCandidato(HttpMethod metodo, string rota, string? recurso)
    {
        HttpRequestMessage pedido = new(metodo, new Uri(rota, UriKind.Relative));
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, Dono);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.CpfHeader, PersonasDeTeste.FernandaDrumond.Valor);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.NameHeader, PersonasDeTeste.NomeFernandaDrumond);
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        pedido.Headers.TryAddWithoutValidation("Accept", recurso is null ? "application/json" : $"application/vnd.uniplus.{recurso}.v1+json");
        return pedido;
    }

    private async Task<ArquivoDaApresentacao> LerArquivoAsync(Guid arquivoId)
    {
        await using AsyncServiceScope escopo = Fabrica.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().ArquivosDaApresentacao.AsNoTracking()
            .SingleAsync(a => a.Id == arquivoId);
    }
}
