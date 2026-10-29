namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem.ProcessoDoEnemPublicadoDeTeste;

/// <summary>
/// O envio ao INEP pela API, sobre um certame publicado de verdade: o arquivo traz só os CPFs das
/// inscrições enviadas da edição, em ordem, e a geração fica registrada sem eles.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class EnvioAoInepEndpointTests
{
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static Publicado? _publicado;

    private readonly CascadingFixture _fixture;

    public EnvioAoInepEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    private async Task<Publicado> PublicadoAsync()
    {
        await Trava.WaitAsync();
        try
        {
            return _publicado ??= await SemearEPublicarAsync(_fixture);
        }
        finally
        {
            Trava.Release();
        }
    }

    private static string Base(Publicado p, string edicao = Edicao) =>
        $"/api/selecao/processos-seletivos/{p.ProcessoId}/etapas/{p.EtapaDoEnemId}/edicoes-do-enem/{edicao}/envios-ao-inep";

    private static HttpRequestMessage Requisicao(HttpMethod metodo, string url, string? papel = "plataforma-admin", object? corpo = null)
    {
        HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        if (papel is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
            request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, papel);
        }

        return request;
    }

    private async Task InscreverAsync(Publicado p, PersonasDeTeste.PessoaFicticia pessoa, SituacaoInscricao situacao, string? edicao)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        await InscricoesDoEnemDeTeste.NovaInscricaoAsync(
            scope.ServiceProvider.GetRequiredService<SelecaoDbContext>(),
            scope.ServiceProvider.GetRequiredService<IUniPlusEncryptionService>(),
            scope.ServiceProvider.GetRequiredService<IUniPlusBlindIndexService>(),
            p.ProcessoId, pessoa, situacao, edicao);
    }

    [Fact(DisplayName = "sem token é 401 e o administrador da plataforma sem concessão é 404")]
    public async Task Gerar_SemAutorizacao_Recusa()
    {
        Publicado p = await PublicadoAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        object corpo = new { leiauteCodigo = "INEP_ENEM_ENVIO", leiauteVersao = "v1" };

        using HttpRequestMessage semToken = Requisicao(HttpMethod.Post, Base(p), papel: null, corpo);
        using HttpRequestMessage semConcessao = Requisicao(HttpMethod.Post, Base(p), papel: "plataforma-admin", corpo);
        semConcessao.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, "diogo.souza");

        (await client.SendAsync(semToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.SendAsync(semConcessao)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "gera o arquivo com os CPFs das inscrições enviadas da edição, em ordem, e registra a geração sem os CPFs")]
    public async Task Gerar_DevolveOArquivoEHistoricoSemCpf()
    {
        Publicado p = await PublicadoAsync();
        IReadOnlyList<PersonasDeTeste.PessoaFicticia> pessoas = PersonasDeTeste.Candidatas;
        await InscreverAsync(p, pessoas[0], SituacaoInscricao.Submetida, Edicao);
        await InscreverAsync(p, pessoas[1], SituacaoInscricao.Submetida, Edicao);
        await InscreverAsync(p, pessoas[2], SituacaoInscricao.Rascunho, Edicao);
        await InscreverAsync(p, pessoas[3], SituacaoInscricao.Cancelada, Edicao);
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpRequestMessage gerar = Requisicao(HttpMethod.Post, Base(p), corpo: new { leiauteCodigo = "INEP_ENEM_ENVIO", leiauteVersao = "v1" });
        HttpResponseMessage resposta = await client.SendAsync(gerar);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        resposta.Content.Headers.ContentDisposition!.FileName.Should().Be("envio-inep-enem-2025.txt");
        resposta.Headers.CacheControl!.NoStore.Should().BeTrue();
        string[] linhas = (await resposta.Content.ReadAsStringAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        linhas.Should().Equal(new[] { pessoas[0].Cpf, pessoas[1].Cpf }.Order(StringComparer.Ordinal));

        using HttpRequestMessage historico = Requisicao(HttpMethod.Get, Base(p));
        HttpResponseMessage lista = await client.SendAsync(historico);
        lista.StatusCode.Should().Be(HttpStatusCode.OK);
        string corpo = await lista.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(corpo);
        doc.RootElement[0].GetProperty("totalDeCpfs").GetInt32().Should().Be(2);
        doc.RootElement[0].GetProperty("leiauteCodigo").GetString().Should().Be("INEP_ENEM_ENVIO");
        doc.RootElement[0].GetProperty("geradoPorNome").GetString().Should().Be("Test User");
        corpo.Should().NotContain(pessoas[0].Cpf).And.NotContain(pessoas[1].Cpf);
    }

    [Fact(DisplayName = "com as inscrições abertas o envio é 422 e nenhuma geração é registrada")]
    public async Task Gerar_InscricoesAbertas_Recusa()
    {
        Publicado p = await SemearEPublicarAsync(_fixture, new DateTimeOffset(2099, 12, 20, 15, 0, 0, TimeSpan.Zero));
        await InscreverAsync(p, PersonasDeTeste.Candidatas[0], SituacaoInscricao.Submetida, Edicao);
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpRequestMessage gerar = Requisicao(HttpMethod.Post, Base(p), corpo: new { leiauteCodigo = "INEP_ENEM_ENVIO", leiauteVersao = "v1" });
        HttpResponseMessage resposta = await client.SendAsync(gerar);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoAsync(resposta)).Should().Be("uniplus.selecao.notas_do_enem.inscricoes_em_andamento");
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("20/12/2099 às 12h00");
        using HttpRequestMessage historico = Requisicao(HttpMethod.Get, Base(p));
        using JsonDocument doc = JsonDocument.Parse(await (await client.SendAsync(historico)).Content.ReadAsStringAsync());
        doc.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "edição não admitida e leiaute de retorno no envio são 422; etapa de outro processo é 404 o mesmo")]
    public async Task Gerar_EscopoInvalido_Recusa()
    {
        Publicado p = await PublicadoAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        object corpo = new { leiauteCodigo = "INEP_ENEM_ENVIO", leiauteVersao = "v1" };

        using HttpRequestMessage edicao = Requisicao(HttpMethod.Post, Base(p, "2024"), corpo: corpo);
        using HttpRequestMessage leiaute = Requisicao(HttpMethod.Post, Base(p), corpo: new { leiauteCodigo = "INEP_ENEM_RETORNO", leiauteVersao = "v1" });
        using HttpRequestMessage etapa = Requisicao(
            HttpMethod.Post, $"/api/selecao/processos-seletivos/{p.ProcessoId}/etapas/{Guid.CreateVersion7()}/edicoes-do-enem/{Edicao}/envios-ao-inep", corpo: corpo);

        HttpResponseMessage naoAdmitida = await client.SendAsync(edicao);
        naoAdmitida.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoAsync(naoAdmitida)).Should().Be("uniplus.selecao.notas_do_enem.edicao_nao_admitida");
        HttpResponseMessage leiauteErrado = await client.SendAsync(leiaute);
        leiauteErrado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoAsync(leiauteErrado)).Should().Be("uniplus.selecao.notas_do_enem.leiaute_indisponivel");
        HttpResponseMessage semEtapa = await client.SendAsync(etapa);
        semEtapa.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodigoAsync(semEtapa)).Should().Be("uniplus.selecao.notas_do_enem.etapa_nao_encontrada");
    }

    private static async Task<string?> CodigoAsync(HttpResponseMessage resposta)
    {
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("code").GetString();
    }
}
