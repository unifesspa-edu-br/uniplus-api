namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Net;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModeloFormularioHttp;
using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModelosParaTeste;

/// <summary>
/// O tipo de processo do modelo é guardado na forma em que o cadastro grava o código (NFC): o processo
/// compara o código do modelo com o do tipo congelado de forma ordinal, e um código equivalente gravado
/// decomposto não casaria.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class ModeloFormularioTipoDeProcessoNormalizadoTests
{
    private const string TiposProcesso = "/api/configuracao/admin/tipos-processo";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public ModeloFormularioTipoDeProcessoNormalizadoTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O modelo cadastrado com o código do tipo decomposto guarda o código na forma do cadastro")]
    public async Task Criar_TipoDeProcessoDecomposto_GuardaOCodigoNormalizado()
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        using HttpClient client = _fixture.Factory.CreateClient();
        await CadastrarAsync(client, TiposProcesso, new { codigo = $"PÓS_{sufixo}", nome = "Pós-graduação", descricao = (string?)null });

        Guid id = await CriarAsync(client, ComTipoDeProcesso(Habilitacao(CodigoUnico()), $"PÓS_{sufixo}"));

        JsonObject lido = await ObterAsync(client, id);
        lido["descritivo"]!["tipoProcessoCodigo"]!.GetValue<string>().Should().Be($"PÓS_{sufixo}");
        (await StatusAsync(client, HttpMethod.Get, $"{Base}/{id}")).Should().Be(HttpStatusCode.OK);
    }
}
