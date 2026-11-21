namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposBanca;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// O papel, as duas marcas e os tipos de decisão que o tipo de banca julga (UNI-REQ-0139): leitura, gravação, edição e
/// recusas acumuladas, com Wolverine contra Postgres efêmero. Cada teste que cadastra libera antes o código que a semente
/// ocupa.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class TipoBancaAtuacaoEndpointTests
{
    private const string Prefixo = "uniplus.configuracao.tipo_banca.";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoBancaAtuacaoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "POST com papel, marcas e tipos de decisão cria (201), e o GET devolve a atuação com código e rótulo")]
    public async Task Criar_ComAtuacao_LeituraTrazAAtuacao()
    {
        await LiberarAsync("BANCA_CORRECAO_REDACOES");
        using HttpClient client = _fixture.Factory.CreateClient();

        using JsonDocument doc = await ChamadasDeTiposDeBancaDeTeste.CriarELerAsync(client, new
        {
            codigo = "BANCA_CORRECAO_REDACOES",
            nome = "Banca de correção de redações",
            papel = "AVALIA_O_MERITO",
            exigeDuasOuMaisBancas = true,
            tiposDeDecisaoJulgados = new[] { "NOTA_DA_ETAPA", "CLASSIFICACAO" },
        });

        DeveTrazerAAtuacao(doc.RootElement);
    }

    [Fact(DisplayName = "POST sem papel retorna 422 papel_obrigatorio, com o campo papel")]
    public async Task Criar_SemPapel_Retorna422PapelObrigatorio()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await ChamadasDeTiposDeBancaDeTeste.PostAsync(
            client, new { codigo = "BANCA_ENTREVISTA", nome = "Banca de entrevista" });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        ChamadasDeTiposDeBancaDeTeste.ErrosDe(await resposta.Content.ReadAsStringAsync())
            .Should().Equal(("papel", Prefixo + "papel_obrigatorio"));
    }

    [Fact(DisplayName = "POST com papel e tipo de decisão fora do vocabulário acumula as duas violações, sem repetir os valores enviados")]
    public async Task Criar_PapelETipoDeDecisaoForaDoVocabulario_AcumulaSemRepetirOsValores()
    {
        (HttpStatusCode status, string recusa) = await RecusaDoPostAsync(
            "PAPEL_QUE_NAO_EXISTE", ["NOTA_DA_ETAPA", "TIPO_QUE_NAO_EXISTE"]);

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        ChamadasDeTiposDeBancaDeTeste.ErrosDe(recusa).Should().Equal(
            ("papel", Prefixo + "papel_fora_do_vocabulario"),
            ("tiposDeDecisaoJulgados[1]", Prefixo + "tipo_de_decisao_fora_do_vocabulario"));
        recusa.Should().NotContain("PAPEL_QUE_NAO_EXISTE").And.NotContain("TIPO_QUE_NAO_EXISTE");
    }

    [Fact(DisplayName = "POST de papel que não avalia o mérito com tipo de decisão retorna 422 tipos_de_decisao_incoerentes_com_o_papel")]
    public async Task Criar_TipoDeDecisaoEmPapelQueNaoAvalia_Retorna422Incoerente()
    {
        (HttpStatusCode status, string recusa) = await RecusaDoPostAsync("JULGA_A_PRIMEIRA_INSTANCIA", ["NOTA_DA_ETAPA"]);

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        ChamadasDeTiposDeBancaDeTeste.ErrosDe(recusa)
            .Should().Equal(("tiposDeDecisaoJulgados", Prefixo + "tipos_de_decisao_incoerentes_com_o_papel"));
    }

    [Fact(DisplayName = "PUT troca o papel, as marcas e os tipos de decisão, e o GET devolve o que foi gravado")]
    public async Task Atualizar_TrocaAAtuacao_Retorna204EPersiste()
    {
        await LiberarAsync("BANCA_BIOPSICOSSOCIAL");
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CadastrarComPapelDeImportacaoAsync(client);

        HttpResponseMessage resposta = await ChamadasDeTiposDeBancaDeTeste.PutAsync(client, id, AtuacaoDeAvaliacao(id));

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using JsonDocument doc = await ChamadasDeTiposDeBancaDeTeste.LerAsync(client, id);
        DeveTerGravadoAAtuacaoDeAvaliacao(doc.RootElement);
    }

    [Fact(DisplayName = "PUT sem as marcas e sem os tipos de decisão zera o que estava gravado, e o GET devolve as marcas falsas e a lista vazia")]
    public async Task Atualizar_OmitindoOsOpcionais_ZeraAsMarcasEOsTiposDeDecisao()
    {
        await LiberarAsync("BANCA_BIOPSICOSSOCIAL");
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CadastrarComAvaliacaoCompletaAsync(client);

        HttpResponseMessage resposta = await ChamadasDeTiposDeBancaDeTeste.PutAsync(
            client, id, new { id, nome = "Banca de avaliação biopsicossocial", papel = "IMPORTA_CANDIDATOS" });

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using JsonDocument doc = await ChamadasDeTiposDeBancaDeTeste.LerAsync(client, id);
        doc.RootElement.GetProperty("papel").GetProperty("codigo").GetString().Should().Be("IMPORTA_CANDIDATOS");
        doc.RootElement.GetProperty("exigeDuasOuMaisBancas").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("encerraRecursoNaPrimeiraInstancia").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("tiposDeDecisaoJulgados").GetArrayLength().Should().Be(0);
    }

    private static object AtuacaoDeAvaliacao(Guid id) => new
    {
        id,
        nome = "Banca de avaliação biopsicossocial",
        papel = "AVALIA_O_MERITO",
        exigeDuasOuMaisBancas = true,
        tiposDeDecisaoJulgados = new[] { "AVALIACAO_BIOPSICOSSOCIAL" },
    };

    private static void DeveTerGravadoAAtuacaoDeAvaliacao(JsonElement leitura)
    {
        leitura.GetProperty("papel").GetProperty("codigo").GetString().Should().Be("AVALIA_O_MERITO");
        leitura.GetProperty("exigeDuasOuMaisBancas").GetBoolean().Should().BeTrue();
        leitura.GetProperty("tiposDeDecisaoJulgados")[0].GetProperty("codigo").GetString().Should().Be("AVALIACAO_BIOPSICOSSOCIAL");
    }

    private async Task<(HttpStatusCode Status, string Corpo)> RecusaDoPostAsync(string papel, string[] tiposDeDecisao)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage resposta = await ChamadasDeTiposDeBancaDeTeste.PostAsync(client, new
        {
            codigo = "BANCA_ENTREVISTA",
            nome = "Banca de entrevista",
            papel,
            tiposDeDecisaoJulgados = tiposDeDecisao,
        });

        return (resposta.StatusCode, await resposta.Content.ReadAsStringAsync());
    }

    private static async Task<Guid> CadastrarComPapelDeImportacaoAsync(HttpClient client)
    {
        using JsonDocument criado = await ChamadasDeTiposDeBancaDeTeste.CriarELerAsync(
            client, new { codigo = "BANCA_BIOPSICOSSOCIAL", nome = "Banca de avaliação biopsicossocial", papel = "IMPORTA_CANDIDATOS" });

        return Guid.Parse(criado.RootElement.GetProperty("id").GetString()!);
    }

    private static async Task<Guid> CadastrarComAvaliacaoCompletaAsync(HttpClient client)
    {
        using JsonDocument criado = await ChamadasDeTiposDeBancaDeTeste.CriarELerAsync(client, new
        {
            codigo = "BANCA_BIOPSICOSSOCIAL",
            nome = "Banca de avaliação biopsicossocial",
            papel = "AVALIA_O_MERITO",
            exigeDuasOuMaisBancas = true,
            encerraRecursoNaPrimeiraInstancia = true,
            tiposDeDecisaoJulgados = new[] { "AVALIACAO_BIOPSICOSSOCIAL", "NOTA_DA_ETAPA" },
        });

        criado.RootElement.GetProperty("exigeDuasOuMaisBancas").GetBoolean().Should().BeTrue();
        criado.RootElement.GetProperty("encerraRecursoNaPrimeiraInstancia").GetBoolean().Should().BeTrue();
        criado.RootElement.GetProperty("tiposDeDecisaoJulgados").GetArrayLength().Should().Be(2);
        return Guid.Parse(criado.RootElement.GetProperty("id").GetString()!);
    }

    private static void DeveTrazerAAtuacao(JsonElement leitura)
    {
        leitura.GetProperty("papel").GetProperty("codigo").GetString().Should().Be("AVALIA_O_MERITO");
        leitura.GetProperty("papel").GetProperty("rotulo").GetString().Should().Be("Avalia o mérito");
        leitura.GetProperty("exigeDuasOuMaisBancas").GetBoolean().Should().BeTrue();
        leitura.GetProperty("encerraRecursoNaPrimeiraInstancia").GetBoolean().Should().BeFalse();
        leitura.GetProperty("tiposDeDecisaoJulgados").EnumerateArray()
            .Select(static tipo => (tipo.GetProperty("codigo").GetString(), tipo.GetProperty("rotulo").GetString()))
            .Should().Equal(("CLASSIFICACAO", "Classificação"), ("NOTA_DA_ETAPA", "Nota da etapa"));
    }

    private Task LiberarAsync(string codigo) => TiposDeBancaDeTeste.ApagarAsync(_fixture.ConnectionString, codigo);
}
