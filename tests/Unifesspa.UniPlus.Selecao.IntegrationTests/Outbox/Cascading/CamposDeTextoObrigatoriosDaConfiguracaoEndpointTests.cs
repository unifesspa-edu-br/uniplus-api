namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net.Http.Headers;
using System.Text;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// O texto obrigatório omitido ou nulo na classificação, nas regras de derivação, nas opções declaradas e na aplicação de
/// modelo de formulário chega ao domínio e é recusado com 422, no campo do pedido, junto das demais recusas do mesmo pedido.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CamposDeTextoObrigatoriosDaConfiguracaoEndpointTests(CascadingFixture fixture)
{
    private const string Raiz = "/api/selecao/processos-seletivos/";
    private const string Obrigatorio = "uniplus.validacao.obrigatorio";

    [Theory(DisplayName = "A classificação sem a regra de cálculo, omitida, nula, vazia ou só com espaços, é recusada no campo, junto da ordem de alocação ausente")]
    [InlineData("")]
    [InlineData(""" "regraCalculoCodigo": null, "regraCalculoVersao": null, """)]
    [InlineData(""" "regraCalculoCodigo": "", "regraCalculoVersao": "", """)]
    [InlineData(""" "regraCalculoCodigo": "   ", "regraCalculoVersao": "   ", """)]
    public async Task Classificacao_SemAsRegras_RecusaOsCampos(string regraDeCalculo)
    {
        string corpo = $$"""{ {{regraDeCalculo}} "regrasEliminacao": [], "baseadoEmEnem": false }""";

        using HttpResponseMessage resposta = await EnviarAsync(HttpMethod.Put, "classificacao", corpo);

        await RecusaDeCampoObrigatorioNoDominio.ConferirRecusasAsync(
            resposta,
            ("regraCalculoCodigo", Obrigatorio),
            ("regraCalculoVersao", Obrigatorio),
            ("regraOrdemAlocacaoCodigo", Obrigatorio),
            ("regraOrdemAlocacaoVersao", Obrigatorio));
    }

    [Theory(DisplayName = "A configuração de derivação sem o código do fato, omitido, nulo, vazio ou só com espaços, é recusada no campo com o código próprio")]
    [InlineData("""[{ "regras": [{ "ordem": 0, "contribui": "AC" }] }]""")]
    [InlineData("""[{ "codigoFato": null, "regras": [{ "ordem": 0, "contribui": "AC" }] }]""")]
    [InlineData("""[{ "codigoFato": "", "regras": [{ "ordem": 0, "contribui": "AC" }] }]""")]
    [InlineData("""[{ "codigoFato": "   ", "regras": [{ "ordem": 0, "contribui": "AC" }] }]""")]
    public async Task Derivacao_SemOCodigoDoFato_RecusaOCampo(string corpo)
    {
        using HttpResponseMessage resposta = await EnviarAsync(HttpMethod.Put, "regras-derivacao", corpo);

        await RecusaDeCampoObrigatorioNoDominio.ConferirRecusasAsync(
            resposta, ("configuracoes[0].codigoFato", "uniplus.selecao.configuracao_derivacao_fato.codigo_fato_obrigatorio"));
    }

    [Theory(DisplayName = "A opção sem código e sem rótulo, omitidos, nulos ou vazios, é recusada nos dois campos com os códigos próprios")]
    [InlineData("""{ "opcoes": [{ }] }""")]
    [InlineData("""{ "opcoes": [{ "codigo": null, "rotulo": null }] }""")]
    [InlineData("""{ "opcoes": [{ "codigo": "", "rotulo": "" }] }""")]
    public async Task Opcao_SemCodigoNemRotulo_RecusaOsCampos(string corpo)
    {
        using HttpResponseMessage resposta = await EnviarAsync(HttpMethod.Put, "fatos/CATEGORIA_RENDA/opcoes", corpo);

        await RecusaDeCampoObrigatorioNoDominio.ConferirRecusasAsync(
            resposta,
            ("opcoes[0].codigo", "uniplus.selecao.opcao_declarada_fato.codigo_invalido"),
            ("opcoes[0].rotulo", "uniplus.selecao.opcao_declarada_fato.rotulo_invalido"));
    }

    [Theory(DisplayName = "A aplicação do modelo sem o código do formulário, omitido, nulo, vazio ou só com espaços, é recusada junto do modelo inexistente")]
    [InlineData("""{ "modeloId": "00000000-0000-7000-8000-000000000001" }""")]
    [InlineData("""{ "modeloId": "00000000-0000-7000-8000-000000000001", "formularioCodigo": null }""")]
    [InlineData("""{ "modeloId": "00000000-0000-7000-8000-000000000001", "formularioCodigo": "" }""")]
    [InlineData("""{ "modeloId": "00000000-0000-7000-8000-000000000001", "formularioCodigo": "   " }""")]
    public async Task AplicarModelo_SemOCodigoDoFormulario_AcumulaComOModelo(string corpo)
    {
        using HttpResponseMessage resposta = await EnviarAsync(
            HttpMethod.Post, "formularios/aplicacoes-de-modelo", corpo, "/api/selecao/admin/processos-seletivos/");

        await RecusaDeCampoObrigatorioNoDominio.ConferirRecusasAsync(
            resposta,
            ("formularioCodigo", "uniplus.selecao.formulario_processo.codigo_obrigatorio"),
            ("modeloId", "uniplus.selecao.aplicacao_de_modelo.modelo_inexistente"));
    }

    [Theory(DisplayName = "A distribuição sem a regra de distribuição, omitida, nula, vazia ou só com espaços, é recusada no campo da configuração")]
    [InlineData("")]
    [InlineData(""" "regraDistribuicaoCodigo": null, "regraDistribuicaoVersao": null, """)]
    [InlineData(""" "regraDistribuicaoCodigo": "", "regraDistribuicaoVersao": "", """)]
    [InlineData(""" "regraDistribuicaoCodigo": "   ", "regraDistribuicaoVersao": "   ", """)]
    public async Task Distribuicao_SemARegra_RecusaOsCampos(string regra)
    {
        string corpo = $$"""[{ "ofertaCursoId": "00000000-0000-7000-8000-000000000002", "vagasOfertadasBase": 10, {{regra}} "modalidadeIds": [], "quadro": [] }]""";

        using HttpResponseMessage resposta = await EnviarAsync(HttpMethod.Put, "distribuicao-vagas", corpo);

        await RecusaDeCampoObrigatorioNoDominio.ConferirRecusasAsync(
            resposta,
            ("distribuicaoVagas[0].regraDistribuicaoCodigo", Obrigatorio),
            ("distribuicaoVagas[0].regraDistribuicaoVersao", Obrigatorio));
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string recurso, string corpo, string raiz = Raiz)
    {
        Guid processoId = await SemearAsync();
        using HttpRequestMessage request = new(metodo, new Uri($"{raiz}{processoId}/{recurso}", UriKind.Relative))
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));

        using HttpClient client = fixture.Factory.CreateClient();
        return await client.SendAsync(request);
    }

    private async Task<Guid> SemearAsync()
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (Domain.Entities.ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"Texto obrigatório {Guid.CreateVersion7()}" });
        return processo.Id;
    }
}
