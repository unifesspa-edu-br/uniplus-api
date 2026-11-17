namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

using static AtoNormativoApoio;

/// <summary>
/// Aviso de numeração duplicada e cadeia de retificação de <c>AtoNormativo</c> por HTTP.
/// </summary>
[Collection(PublicacoesEndpointCollection.Name)]
public sealed class AtoNormativoRetificacaoTests
{
    private readonly PublicacoesEndpointFixture _fixture;
    private readonly AtoNormativoApoio _apoio;

    public AtoNormativoRetificacaoTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new AtoNormativoApoio(fixture);
    }

    [Fact(DisplayName = "Número já usado gera aviso no registro, sem impedir; detalhe recomputa")]
    public async Task Registrar_NumeroDuplicado_Avisa()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();
        string serie = SerieUnica();
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid primeiroId = await PostAtoIdAsync(client, PayloadAto(tipo, serie, "13"));

        using JsonDocument segunda = await PostAtoCriadoAsync(client, PayloadAto(tipo, serie, "13"));

        JsonElement avisos = segunda.RootElement.GetProperty("avisos");
        avisos.GetArrayLength().Should().Be(1);
        avisos[0].GetProperty("codigo").GetString().Should().Be("NumeroDuplicado");
        JsonElement detalhe = await LerAtoAsync(client, segunda.RootElement.GetProperty("atoId").GetGuid());
        detalhe.GetProperty("avisos")[0].GetProperty("atosConflitantes")
            .EnumerateArray().Select(e => e.GetGuid()).Should().Contain(primeiroId);
    }

    [Fact(DisplayName = "POST de retificação com a mesma classe de congelamento retorna 201 e expõe a linhagem")]
    public async Task Registrar_Retificacao_Retorna201ComLinhagem()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync(congela: true);
        string serie = SerieUnica();
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid raizId = await PostAtoIdAsync(client, PayloadAto(tipo, serie, "13"));

        Guid retId = await PostAtoIdAsync(
            client, PayloadRetificacao(tipo, serie, "13", raizId, "corrige o anexo II"));

        JsonElement detalhe = await LerAtoAsync(client, retId);
        detalhe.GetProperty("atoRetificadoId").GetGuid().Should().Be(raizId);
        detalhe.GetProperty("motivoRetificacao").GetString().Should().Be("corrige o anexo II");
    }

    [Fact(DisplayName = "POST de retificação com classe de congelamento divergente retorna 422")]
    public async Task Registrar_RetificacaoCongelaDivergente_Retorna422()
    {
        string tipoCongelante = await _apoio.CriarTipoVigenteAsync(congela: true);
        string tipoNaoCongelante = await _apoio.CriarTipoVigenteAsync(congela: false);
        string serie = SerieUnica();
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid raizId = await PostAtoIdAsync(client, PayloadAto(tipoCongelante, serie, "13"));

        HttpResponseMessage retResp = await PostAtoAsync(
            client, PayloadRetificacao(tipoNaoCongelante, serie, "14", raizId, "corrige"));

        retResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await retResp.Content.ReadAsStringAsync())
            .Should().Contain("uniplus.publicacoes.ato_normativo.classe_congelamento_divergente");
    }

    [Fact(DisplayName = "POST que retifica ato inexistente retorna 422")]
    public async Task Registrar_RetificaInexistente_Retorna422()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resp = await PostAtoAsync(
            client, PayloadRetificacao(tipo, SerieUnica(), "13", Guid.CreateVersion7(), "corrige"));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resp.Content.ReadAsStringAsync())
            .Should().Contain("uniplus.publicacoes.ato_normativo.ato_retificado_nao_encontrado");
    }

    [Fact(DisplayName = "POST que retifica um ato já retificado retorna 409")]
    public async Task Registrar_RetificaAtoJaRetificado_Retorna409()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();
        string serie = SerieUnica();
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid raizId = await PostAtoIdAsync(client, PayloadAto(tipo, serie, "13"));
        await PostAtoIdAsync(client, PayloadRetificacao(tipo, serie, "13", raizId, "primeira"));

        HttpResponseMessage segunda = await PostAtoAsync(
            client, PayloadRetificacao(tipo, serie, "13", raizId, "segunda"));

        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await segunda.Content.ReadAsStringAsync())
            .Should().Contain("uniplus.publicacoes.ato_normativo.ato_ja_retificado");
    }

    [Fact(DisplayName = "POST que retifica a cabeça da cadeia (R2 emenda R1) retorna 201")]
    public async Task Registrar_EmpilhaNaCabeca_Retorna201()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();
        string serie = SerieUnica();
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid raizId = await PostAtoIdAsync(client, PayloadAto(tipo, serie, "13"));
        Guid r1Id = await PostAtoIdAsync(client, PayloadRetificacao(tipo, serie, "13", raizId, "primeira"));

        HttpResponseMessage r2 = await PostAtoAsync(
            client, PayloadRetificacao(tipo, serie, "13", r1Id, "segunda"));

        r2.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "Um aviso (tipo distinto, série e número próprios) retifica um edital quando a classe de congelamento coincide")]
    public async Task Registrar_AvisoRetificaEdital_MesmaClasse_Retorna201()
    {
        string edital = await _apoio.CriarTipoVigenteAsync(congela: true);
        string aviso = await _apoio.CriarTipoVigenteAsync(congela: true);
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid editalId = await PostAtoIdAsync(client, PayloadAto(edital, SerieUnica(), "13"));

        HttpResponseMessage resp = await PostAtoAsync(
            client, PayloadRetificacao(aviso, SerieUnica(), "50", editalId, "prorroga o prazo de inscrições"));

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "Detalhe da 3ª versão de uma cadeia (mesmo número) não avisa duplicata dentro da linhagem")]
    public async Task ObterPorId_CadeiaProfundaMesmoNumero_NaoAvisaLinhagem()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();
        string serie = SerieUnica();
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid a = await PostAtoIdAsync(client, PayloadAto(tipo, serie, "13"));
        Guid b = await PostAtoIdAsync(client, PayloadRetificacao(tipo, serie, "13", a, "segunda versão"));

        Guid c = await PostAtoIdAsync(client, PayloadRetificacao(tipo, serie, "13", b, "terceira versão"));

        (await LerAtoAsync(client, c)).GetProperty("avisos").GetArrayLength().Should().Be(0);
    }
}
