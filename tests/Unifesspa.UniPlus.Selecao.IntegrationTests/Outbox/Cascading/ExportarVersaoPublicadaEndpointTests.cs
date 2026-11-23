namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
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
using Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;
using Unifesspa.UniPlus.Testes.Compartilhado;

[Collection(CascadingCollection.Name)]
public sealed class ExportarVersaoPublicadaEndpointTests(CascadingFixture fixture)
{
    private const string Conta = "exportacao-versao-teste";

    [Theory]
    [InlineData("")]
    [InlineData("?versao=1")]
    public async Task Exportar_VersaoPublicada_DevolveRaizEETag(string consulta)
    {
        (Guid processoId, Guid documentoId) = await PrepararAsync();
        using HttpClient cliente = fixture.Factory.CreateClient();
        using HttpResponseMessage publicacao = await PublicarAsync(cliente, processoId, documentoId);
        publicacao.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using HttpResponseMessage resposta = await ExportarAsync(processoId, consulta);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement corpo = await ClienteDoRascunhoDeTeste.Corpo(resposta);
        corpo.GetProperty("formato").GetString().Should().Be("uniplus.selecao.configuracao");
        corpo.GetProperty("versaoFormato").GetInt32().Should().Be(1);
        corpo.GetProperty("id").GetGuid().Should().Be(processoId);
        corpo.GetProperty("versao").GetInt32().Should().Be(1);
        corpo.GetProperty("publicadoEm").GetDateTimeOffset().Should().NotBe(default);
        resposta.Headers.ETag!.Tag.Should().Be($"\"{await HashAsync(processoId)}\"");
    }

    [Fact]
    public async Task Exportar_NumeroInexistente_Devolve404DaVersao()
    {
        (Guid processoId, _) = await PrepararAsync();
        using HttpResponseMessage resposta = await ExportarAsync(processoId, "?versao=42");
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ClienteDoRascunhoDeTeste.Corpo(resposta)).GetProperty("type").GetString().Should().Contain("versao_nao_encontrada");
    }

    [Fact]
    public async Task Exportar_SemPublicacao_Devolve422()
    {
        (Guid processoId, _) = await PrepararAsync();
        using HttpResponseMessage resposta = await ExportarAsync(processoId, "");
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ClienteDoRascunhoDeTeste.Corpo(resposta)).GetProperty("type").GetString().Should().Contain("vigente_ausente");
    }

    private async Task<(Guid ProcessoId, Guid DocumentoId)> PrepararAsync()
    {
        await TiposDeAtoSeeder.SemearAsync(fixture.Factory.Services);
        ClienteDoRascunhoDeTeste cliente = new(fixture);
        await cliente.ConcederAsync(Conta, UnidadesDeTeste.Ceps.OrigemId);
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(banco, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"Exportação {Guid.CreateVersion7()}", UnidadeAdministradoraOrigemId = UnidadesDeTeste.Ceps.OrigemId });
        return (processo.Id, documento.Id);
    }

    private static async Task<HttpResponseMessage> PublicarAsync(HttpClient cliente, Guid processoId, Guid documentoId)
    {
        object ato = new { orgao = "CEPS", serie = "EDITAL", ano = 2026, dataDoAto = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), assinante = "Diretor", tipoAtoCodigo = "EDITAL_ABERTURA" };
        using HttpRequestMessage pedido = new(HttpMethod.Post, new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative));
        pedido.Content = JsonContent.Create(new { numero = "001/2026", documentoEditalId = documentoId, ato });
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await cliente.SendAsync(pedido);
    }

    private Task<HttpResponseMessage> ExportarAsync(Guid processoId, string consulta) =>
        new ClienteDoRascunhoDeTeste(fixture).EnviarAsync(Conta, HttpMethod.Get, $"/api/selecao/processos-seletivos/{processoId}/configuracao{consulta}");

    private async Task<string> HashAsync(Guid processoId)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await banco.Set<VersaoConfiguracao>().Where(versao => versao.ProcessoSeletivoId == processoId).Select(versao => versao.HashConfiguracao).SingleAsync();
    }
}
