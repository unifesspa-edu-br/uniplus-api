namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModeloFormularioHttp;

/// <summary>
/// A gravação do conteúdo do modelo devolve a recusa de cada grupo de opções e de cada limite de data inválido, cada uma
/// no caminho do seu elemento dentro da restrição.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class RecusasDasOpcoesNaGravacaoDoModeloEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public RecusasDasOpcoesNaGravacaoDoModeloEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Opções com um grupo nulo e outro com condição inválida recusam os dois grupos, cada um no seu caminho")]
    public async Task Criar_OpcoesComGrupoNuloEGrupoRecusado_RecusaOsDoisGrupos()
    {
        IReadOnlyList<(string? Campo, string? Codigo)> recusas = await RecusasAsync(OpcoesComGrupoNuloEGrupoRecusado());

        recusas.Should().HaveCount(2);
        recusas[0].Campo.Should().EndWith("restricoes[0].entradas[0]");
        recusas[0].Codigo.Should().Be("uniplus.restricao_valor.opcoes_vazias");
        recusas[1].Campo.Should().EndWith("restricoes[0].entradas[1].quando[0][0].operador");
        recusas[1].Codigo.Should().Be("uniplus.condicao_dnf.operador_invalido");
    }

    [Fact(DisplayName = "Faixa de data com os dois limites inválidos recusa o mínimo e depois o máximo")]
    public async Task Criar_FaixaDeDataComOsDoisLimitesInvalidos_RecusaOsDoisLimites()
    {
        object limiteInvalido = new { ancora = "HOJE" };
        object faixa = new { tipo = "FAIXA_DE_DATA", dataMinima = limiteInvalido, dataMaxima = limiteInvalido };

        IReadOnlyList<(string? Campo, string? Codigo)> recusas = await RecusasAsync(faixa);

        recusas.Should().HaveCount(2);
        recusas[0].Campo.Should().EndWith("restricoes[0].dataMinima");
        recusas[1].Campo.Should().EndWith("restricoes[0].dataMaxima");
        recusas.Should().OnlyContain(static recusa => recusa.Codigo == "uniplus.restricao_valor.limite_de_data_invalido");
    }

    private async Task<IReadOnlyList<(string? Campo, string? Codigo)>> RecusasAsync(object restricao)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, Base, ModeloComRestricao(restricao));

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        using JsonDocument problema = JsonDocument.Parse(corpo);
        return [.. problema.RootElement.GetProperty("errors").EnumerateArray().Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString()))];
    }

    private static object OpcoesComGrupoNuloEGrupoRecusado() => new
    {
        tipo = "OPCOES_PERMITIDAS",
        entradas = new object?[]
        {
            null,
            new { quando = new[] { new[] { new { fato = "QUILOMBOLA", operador = "OPERADOR_QUE_NAO_EXISTE", valor = true } } }, valores = new[] { "REGULAR" } },
        },
    };

    private static object ModeloComRestricao(object restricao) => new
    {
        codigo = CodigoUnico(),
        declaracao = new { serveAFaseDeInscricao = true, obrigatorio = true },
        descritivo = new { nome = "Inscrição com restrição recusada", descricao = (string?)null, tipoProcessoCodigo = (string?)null },
        conteudo = new { titulo = "Inscrição", paginas = new[] { PaginaDeDados() }, itens = new[] { ItemComRestricao(restricao) } },
    };

    private static object PaginaDeDados() =>
        new { codigo = "DADOS", ordem = 0, tipo = "SECAO", bloco = (string?)null, apresentacao = new { titulo = "Dados", descricao = (string?)null, aviso = (string?)null } };

    private static object ItemComRestricao(object restricao) => new
    {
        fatoCodigo = "FORMA_CONCLUSAO_EM",
        ordem = 0,
        paginaCodigo = "DADOS",
        apresentacao = new { rotulo = "Forma de conclusão do ensino médio", tipoDeCampo = "ESCOLHA_UNICA" },
        regras = new { exibicao = (object?)null, obrigatoriedade = new { tipo = "SEMPRE" }, restricoes = new[] { restricao } },
    };
}
