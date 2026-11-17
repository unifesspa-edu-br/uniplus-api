namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

using static AtoNormativoApoio;

/// <summary>
/// Anexos de <c>AtoNormativo</c> por HTTP: gravação na ordem enviada e recusa nomeada
/// de anexo inválido ou repetido.
/// </summary>
[Collection(PublicacoesEndpointCollection.Name)]
public sealed class AtoNormativoAnexosTests
{
    private static readonly string[] NomesNaOrdemEnviada = ["Anexo II.pdf", "Anexo I.pdf"];

    private readonly PublicacoesEndpointFixture _fixture;
    private readonly AtoNormativoApoio _apoio;

    public AtoNormativoAnexosTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new AtoNormativoApoio(fixture);
    }

    [Fact(DisplayName = "POST com dois anexos os grava, e a leitura devolve nome, tipo de mídia e hash na ordem enviada")]
    public async Task Registrar_ComAnexos_LeituraDevolveNaOrdemEnviada()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();
        Guid entidade = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        Guid atoId = await PostAtoVinculadoComDoisAnexosAsync(client, tipo, entidade);

        JsonElement anexos = (await LerAtoAsync(client, atoId)).GetProperty("anexos");
        NomesDosAnexos(anexos).Should().Equal(NomesNaOrdemEnviada);
        anexos.EnumerateArray().Select(a => a.GetProperty("hash").GetString()).Should().Equal(OutroHash, HashValido);
        anexos[0].GetProperty("tipoMidia").GetString().Should().Be("application/pdf");
        NomesDosAnexos(await LerAnexosDaEntidadeAsync(client, entidade)).Should().Equal(NomesNaOrdemEnviada);
        NomesDosAnexos((await ProcurarNaListagemAsync(client, atoId)).GetProperty("anexos"))
            .Should().Equal(NomesNaOrdemEnviada);
    }

    [Fact(DisplayName = "POST sem anexos devolve a lista de anexos vazia")]
    public async Task Registrar_SemAnexos_DevolveListaVazia()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        Guid atoId = await PostAtoIdAsync(client, PayloadAto(tipo, SerieUnica(), "1"));

        (await LerAtoAsync(client, atoId)).GetProperty("anexos").GetArrayLength().Should().Be(0);
    }

    [Theory(DisplayName = "POST com anexo inválido retorna 422 com o erro nomeado")]
    [InlineData("Anexo.pdf", "application/pdf", "ABC", "hash_invalido")]
    [InlineData("   ", "application/pdf", HashValido, "nome_invalido")]
    [InlineData("Anexo.pdf", "pdf", HashValido, "tipo_midia_invalido")]
    public async Task Registrar_AnexoInvalido_Retorna422(string nome, string tipoMidia, string hash, string erro)
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await PostAtoAsync(
            client, PayloadComAnexos(tipo, new[] { new AnexoEnviado(nome, tipoMidia, hash) }));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync())
            .Should().Contain($"uniplus.publicacoes.anexo_do_ato.{erro}");
    }

    [Fact(DisplayName = "POST com dois anexos de mesmo nome retorna 422 e nada é gravado")]
    public async Task Registrar_AnexosComNomeDuplicado_Retorna422()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();
        string serie = SerieUnica();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await PostAtoAsync(
            client, PayloadComAnexos(tipo, DoisAnexosDeMesmoNome(), serie));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync())
            .Should().Contain("uniplus.publicacoes.anexo_do_ato.nome_duplicado");
        (await _apoio.ExisteAtoNaSerieAsync(serie)).Should().BeFalse();
    }

    private static Task<Guid> PostAtoVinculadoComDoisAnexosAsync(HttpClient client, string tipo, Guid entidade) =>
        PostAtoIdAsync(client, PayloadComAnexos(
            tipo, DoisAnexosDistintos(), vinculos: new[] { new { entidadeTipo = "CHAMADA", entidadeId = entidade } }));

    private static AnexoEnviado[] DoisAnexosDistintos() =>
    [
        new("Anexo II.pdf", "application/pdf", OutroHash),
        new("Anexo I.pdf", "application/pdf", HashValido),
    ];

    private static AnexoEnviado[] DoisAnexosDeMesmoNome() =>
    [
        new("Anexo I.pdf", "application/pdf", HashValido),
        new("Anexo I.pdf", "application/pdf", OutroHash),
    ];

    private static IEnumerable<string?> NomesDosAnexos(JsonElement anexos) =>
        anexos.EnumerateArray().Select(a => a.GetProperty("nome").GetString());

    private static async Task<JsonElement> LerAnexosDaEntidadeAsync(HttpClient client, Guid entidade)
    {
        using JsonDocument lista = JsonDocument.Parse(await client.GetStringAsync(
            new Uri($"/api/publicacoes/entidades/CHAMADA/{entidade}/atos", UriKind.Relative)));
        return lista.RootElement.EnumerateArray().Single().GetProperty("anexos").Clone();
    }

    private sealed record AnexoEnviado(string Nome, string TipoMidia, string Hash);
}
