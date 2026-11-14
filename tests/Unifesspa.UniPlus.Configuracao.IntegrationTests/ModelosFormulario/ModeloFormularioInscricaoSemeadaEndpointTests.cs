namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Regras.Formularios;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModeloFormularioHttp;

/// <summary>
/// O que a pré-visualização do modelo de inscrição semeado exibe, pergunta e exige conforme as respostas
/// simuladas.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class ModeloFormularioInscricaoSemeadaEndpointTests
{
    private static readonly string[] FatosDasCotas = ["INDIGENA", "QUILOMBOLA", "CONCORRER_PPI", "CONCORRER_Q", "BAIXA_RENDA", "CONCORRER_RENDA"];

    private readonly ConfiguracaoEndpointFixture _fixture;

    public ModeloFormularioInscricaoSemeadaEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "No modelo de inscrição semeado, o tipo de localidade vem nos dados básicos antes do endereço, e o nome da comunidade só aparece para aldeia, comunidade ou quilombo")]
    [InlineData("URBANO", "FALSO")]
    [InlineData("ALDEIA", "VERDADEIRO")]
    public async Task ModeloDeInscricao_TipoDeLocalidadeAntesDoEndereco(string localidade, string comunidadeVisivel)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await ModeloDaMedicinaAsync(client);

        ConferirLocalidadeAntesDoEndereco(await RenderizavelAsync(client, id));

        JsonArray itens = await PreVisualizarAsync(client, id, new Dictionary<string, object> { ["TIPO_ENDERECO"] = localidade });
        Item(itens, "NOME_COMUNIDADE")["visivel"]!.GetValue<string>().Should().Be(comunidadeVisivel);
    }

    [Theory(DisplayName = "No modelo de inscrição semeado, \"Concluí por certificação\" só é oferecido a quem concluiu por certificação, e a opção pelas vagas de escola pública segue o egresso derivado")]
    [InlineData("REGULAR", "PRIVADA_BOLSA_INTEGRAL", false, "FALSO")]
    [InlineData("ENCCEJA", "CERTIFICACAO", true, "VERDADEIRO")]
    public async Task ModeloDeInscricao_OrigemEscolarEmDuasPerguntas(
        string formaDeConclusao, string ondeCursou, bool ofereceCertificacao, string opcaoPelaEscolaPublicaVisivel)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await ModeloDaMedicinaAsync(client);

        JsonArray itens = await PreVisualizarAsync(
            client, id, new Dictionary<string, object> { ["FORMA_CONCLUSAO_EM"] = formaDeConclusao, ["ONDE_CURSOU_EM"] = ondeCursou });

        Textos(itens, "fatoCodigo").Should().NotContain("EGRESSO_ESCOLA_PUBLICA", "o egresso é derivado, não perguntado");
        Item(itens, "ONDE_CURSOU_EM")["opcoes"]!["codigos"]!.AsArray()
            .Select(static c => c!.GetValue<string>()).Contains("CERTIFICACAO").Should().Be(ofereceCertificacao);
        Item(itens, "CONCORRER_EP")["visivel"]!.GetValue<string>().Should().Be(opcaoPelaEscolaPublicaVisivel);
    }

    [Theory(DisplayName = "No modelo de inscrição semeado, quem cursou o ensino médio regular ou na EJA pede o Fator pelo município em que o cursou, mesmo com o endereço atual fora da área")]
    [InlineData("REGULAR")]
    [InlineData("EJA")]
    public async Task ModeloDeInscricao_FatorPeloMunicipioDoEnsinoMedio(string formaDeConclusao)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await ModeloDaMedicinaAsync(client);
        await CadastrarBaseLegalAsync(client, [Municipio(Maraba, "Marabá")]);

        JsonArray itens = await PreVisualizarAsync(client, id, RespostasDoFatorPeloEnsinoMedio(formaDeConclusao));

        ConferirMunicipioDoEnsinoMedioExigido(itens);
        Item(itens, "CAMINHO_FATOR_ATENUACAO")["visivel"]!.GetValue<string>().Should().Be("FALSO", "só quem concluiu por certificação escolhe o caminho");
        Item(itens, "MUNICIPIO_RESIDENCIA_FATOR_ATENUACAO")["visivel"]!.GetValue<string>().Should().Be("FALSO");
    }

    [Theory(DisplayName = "No modelo de inscrição semeado, quem concluiu por certificação e pede o Fator escolhe o caminho, que leva ao município do ensino médio ou ao da residência no período")]
    [InlineData("ENEM", "ENSINO_MEDIO", "VERDADEIRO", "FALSO")]
    [InlineData("ENCCEJA", "RESIDENCIA", "FALSO", "VERDADEIRO")]
    [InlineData("PROFICIENCIA", "RESIDENCIA", "FALSO", "VERDADEIRO")]
    public async Task ModeloDeInscricao_CertificacaoEscolheOCaminhoDoFator(
        string formaDeConclusao, string caminho, string ensinoMedioVisivel, string residenciaVisivel)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await ModeloDaMedicinaAsync(client);

        JsonArray itens = await PreVisualizarAsync(client, id, RespostasDaCertificacaoComFator(formaDeConclusao, caminho));

        ConferirCaminhoDoFatorEscolhido(itens);
        Item(itens, "MUNICIPIO_ENSINO_MEDIO_FATOR_ATENUACAO")["visivel"]!.GetValue<string>().Should().Be(ensinoMedioVisivel);
        Item(itens, "MUNICIPIO_RESIDENCIA_FATOR_ATENUACAO")["visivel"]!.GetValue<string>().Should().Be(residenciaVisivel);
    }

    [Theory(DisplayName = "No modelo de inscrição semeado, as perguntas das cotas da lei só aparecem para quem optou por elas, e a da PPI só para quem é preto, pardo ou indígena com pertencimento")]
    [InlineData("SOMENTE_PUBLICA", true, "BRANCA", false, false, false, "QUILOMBOLA,BAIXA_RENDA")]
    [InlineData("SOMENTE_PUBLICA", true, "AMARELA", false, false, false, "QUILOMBOLA,BAIXA_RENDA")]
    [InlineData("SOMENTE_PUBLICA", true, "PARDA", false, false, false, "QUILOMBOLA,CONCORRER_PPI,BAIXA_RENDA")]
    [InlineData("SOMENTE_PUBLICA", true, "INDIGENA", false, false, false, "INDIGENA,BAIXA_RENDA")]
    [InlineData("SOMENTE_PUBLICA", true, "INDIGENA", true, false, false, "INDIGENA,CONCORRER_PPI,BAIXA_RENDA")]
    [InlineData("SOMENTE_PUBLICA", true, "PRETA", false, true, true, "QUILOMBOLA,CONCORRER_PPI,CONCORRER_Q,BAIXA_RENDA,CONCORRER_RENDA")]
    [InlineData("SOMENTE_PUBLICA", false, "PARDA", true, true, true, "")]
    [InlineData("PRIVADA", false, "INDIGENA", true, true, true, "")]
    public async Task ModeloDeInscricao_ReservaPerguntaAElegibilidadeDeQuemOptou(
        string ondeCursou, bool concorrerEscolaPublica, string corRaca, bool indigena, bool quilombola, bool baixaRenda, string visiveis)
    {
        ArgumentNullException.ThrowIfNull(visiveis);
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await ModeloDaMedicinaAsync(client);
        Dictionary<string, object> respostas = RespostasDasCotas(ondeCursou, concorrerEscolaPublica, corRaca, (indigena, quilombola, baixaRenda));

        ConferirPerguntasVisiveis(await PreVisualizarAsync(client, id, respostas), visiveis);
    }

    private static async Task<Guid> ModeloDaMedicinaAsync(HttpClient client) =>
        await IdDoModeloAsync(client, "INSCRICAO", "PSR_INSCRICAO_SEM_PROVA");

    private static void ConferirLocalidadeAntesDoEndereco(JsonObject formulario)
    {
        List<string> basicos = ItensDaEtapaDasRegras(formulario, ConjuntoBasicoDaInscricao.CodigoDaSecao);
        basicos.IndexOf("TIPO_ENDERECO").Should().BeLessThan(basicos.IndexOf("NOME_COMUNIDADE"));
        basicos.IndexOf("NOME_COMUNIDADE").Should().BeLessThan(basicos.IndexOf("ENDERECO_RESIDENCIAL"), "a localidade é perguntada antes do endereço");
        Textos(formulario["etapas"]!, "codigo").Should().NotContain("ENDERECO");
    }

    private static Dictionary<string, object> RespostasDoFatorPeloEnsinoMedio(string formaDeConclusao) => new()
    {
        // Belém, fora da área do bônus: o Fator não confere o endereço atual.
        ["ENDERECO_RESIDENCIAL"] = new { cep = (string?)null, cidade = new { codigoIbge = "1501402", uf = "PA" } },
        ["FORMA_CONCLUSAO_EM"] = formaDeConclusao,
        ["ONDE_CURSOU_EM"] = "SOMENTE_PUBLICA",
        ["SOLICITA_FATOR_ATENUACAO"] = true,
        ["MUNICIPIO_ENSINO_MEDIO_FATOR_ATENUACAO"] = Maraba,
    };

    private static void ConferirMunicipioDoEnsinoMedioExigido(JsonArray itens)
    {
        JsonNode municipio = Item(itens, "MUNICIPIO_ENSINO_MEDIO_FATOR_ATENUACAO");
        municipio["visivel"]!.GetValue<string>().Should().Be("VERDADEIRO");
        municipio["obrigatorio"]!.GetValue<string>().Should().Be("VERDADEIRO");
        municipio["restricoesVioladas"]!.AsArray().Should().BeEmpty();
        municipio["impedido"]!.GetValue<string>().Should().Be("FALSO");
    }

    private static Dictionary<string, object> RespostasDaCertificacaoComFator(string formaDeConclusao, string caminho) => new()
    {
        ["FORMA_CONCLUSAO_EM"] = formaDeConclusao,
        ["ONDE_CURSOU_EM"] = "CERTIFICACAO",
        ["SOLICITA_FATOR_ATENUACAO"] = true,
        ["CAMINHO_FATOR_ATENUACAO"] = caminho,
    };

    private static void ConferirCaminhoDoFatorEscolhido(JsonArray itens)
    {
        JsonNode escolha = Item(itens, "CAMINHO_FATOR_ATENUACAO");
        escolha["visivel"]!.GetValue<string>().Should().Be("VERDADEIRO");
        escolha["obrigatorio"]!.GetValue<string>().Should().Be("VERDADEIRO");
    }

    private static Dictionary<string, object> RespostasDasCotas(
        string ondeCursou, bool concorrerEscolaPublica, string corRaca, (bool Indigena, bool Quilombola, bool BaixaRenda) declaracoes) => new()
        {
            ["FORMA_CONCLUSAO_EM"] = "REGULAR",
            ["ONDE_CURSOU_EM"] = ondeCursou,
            ["CONCORRER_EP"] = concorrerEscolaPublica,
            ["COR_RACA"] = corRaca,
            ["INDIGENA"] = declaracoes.Indigena,
            ["QUILOMBOLA"] = declaracoes.Quilombola,
            ["BAIXA_RENDA"] = declaracoes.BaixaRenda,
        };

    private static void ConferirPerguntasVisiveis(JsonArray itens, string visiveis)
    {
        HashSet<string> esperados = [.. visiveis.Split(',', StringSplitOptions.RemoveEmptyEntries)];
        foreach (string fato in FatosDasCotas)
        {
            Item(itens, fato)["visivel"]!.GetValue<string>().Should().Be(esperados.Contains(fato) ? "VERDADEIRO" : "FALSO", fato);
        }
    }
}
