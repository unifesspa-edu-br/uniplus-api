namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Net;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Regras.Formularios;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModeloFormularioHttp;
using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModelosParaTeste;

/// <summary>
/// O formulário renderizável do modelo: a apresentação e as regras no mesmo corpo, com as opções
/// que vêm dos cadastros institucionais vivos.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class ModeloFormularioRenderizavelEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public ModeloFormularioRenderizavelEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O renderizável do modelo traz a apresentação e as regras no formato do formulário; modelo inexistente é 404")]
    public async Task ObterRenderizavel_DevolveOFormulario()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Habilitacao(CodigoUnico()));

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}/{id}/renderizavel");

        JsonObject formulario = await LerAsync(resposta);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.uniplus.formulario.v2+json");
        Textos(formulario["fatosColetados"]!, "fatoCodigo").Should().Contain("BAIXA_RENDA");
        DasEtapasDasRegras(formulario, "itens", "fatoCodigo").Should().Contain("BAIXA_RENDA", "as regras vão com a apresentação");
        (await StatusAsync(client, HttpMethod.Get, $"{Base}/{Guid.NewGuid()}/renderizavel")).Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "O renderizável do modelo de habilitação semeado traz o grupo repetível com os campos de cada ocorrência")]
    public async Task ObterRenderizavel_ModeloComGrupo_TrazOGrupo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await IdDoModeloAsync(client, "HABILITACAO", "PSR_MEDICINA_2027_HABILITACAO");

        JsonObject formulario = await RenderizavelAsync(client, id);

        JsonNode grupo = Unico(formulario["grupos"]!, "codigo", "COMPOSICAO_FAMILIAR");
        grupo["incluiCandidato"]!.GetValue<bool>().Should().BeTrue();
        grupo["subitens"]!.AsArray().Should().NotBeEmpty();
        DasEtapasDasRegras(formulario, "grupos", "codigo").Should().Contain("COMPOSICAO_FAMILIAR");
    }

    [Fact(DisplayName = "O renderizável do modelo de inscrição gravado sem o conjunto básico, como o semeado, traz os dados do candidato que o processo terá depois de aplicado")]
    public async Task ObterRenderizavel_ModeloDeInscricaoSemOBasico_TrazOsDadosDoCandidato()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await IdDoModeloAsync(client, "INSCRICAO", "PSR_INSCRICAO_SEM_PROVA");

        JsonObject formulario = await RenderizavelAsync(client, id);

        Textos(formulario["etapas"]!, "codigo").Should().Contain(ConjuntoBasicoDaInscricao.CodigoDaSecao);
        Textos(formulario["fatosColetados"]!, "fatoCodigo").Should().Contain(ConjuntoBasicoDaInscricao.Fatos);
        ConferirOpcaoPreta(formulario);
        ItensDaEtapaDasRegras(formulario, ConjuntoBasicoDaInscricao.CodigoDaSecao).Should().Contain(ConjuntoBasicoDaInscricao.Fatos, "as regras vão com a apresentação");
        ConferirPreVisualizacaoDoBasico(await PreVisualizacaoAsync(client, id, new Dictionary<string, object>()));
    }

    [Fact(DisplayName = "Sem processo, a condição de atendimento e o tipo de deficiência oferecem o cadastro institucional vivo")]
    public async Task ObterRenderizavel_AtendimentoEDeficiencia_OferecemOCadastro()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string condicao = CodigoDe("COND", 10);
        string deficiencia = CodigoDe("DEF", 12);
        await CadastrarAsync(client, "/api/configuracao/admin/condicoes-atendimento", new { codigo = condicao, nome = "Ledor" });
        await CadastrarAsync(client, "/api/configuracao/admin/tipos-deficiencia", new { codigo = deficiencia, nome = $"Deficiência {deficiencia}", descricao = "Deficiência relacionada à visão" });

        JsonObject formulario = await RenderizavelAsync(client, await IdDoModeloAsync(client, "INSCRICAO", "PSR_INSCRICAO_COM_PROVA"));

        Opcoes(formulario, "CONDICAO_ATENDIMENTO").Should().Contain(condicao);
        Opcoes(formulario, "TIPO_DEFICIENCIA").Should().Contain(deficiencia);
    }

    [Fact(DisplayName = "Sem processo, o recurso de acessibilidade oferece o cadastro institucional vivo, pelo código")]
    public async Task ObterRenderizavel_RecursoDeAcessibilidade_OfereceOCadastro()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string recurso = CodigoDe("REC", 12);
        await CadastrarAsync(client, "/api/configuracao/admin/recursos-acessibilidade", new { codigo = recurso, nome = $"Sala para amamentação {recurso}" });
        Guid id = await CriarAsync(client, InscricaoComRecursoDeAcessibilidade(CodigoUnico()));

        JsonObject formulario = await RenderizavelAsync(client, id);

        JsonNode opcao = OpcaoDoFato(formulario, "RECURSO_ACESSIBILIDADE", recurso);
        opcao["descricao"]!.GetValue<string>().Should().Be($"Sala para amamentação {recurso}");
    }

    [Fact(DisplayName = "Sem processo, o município da área do bônus oferece os municípios das bases legais cadastradas, sem repetir")]
    public async Task ObterRenderizavel_MunicipioDoBonus_OfereceAsBasesLegais()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        await CadastrarBaseLegalAsync(client, [Municipio(Maraba, "Marabá"), Municipio("1500131", "Abel Figueiredo")]);
        await CadastrarBaseLegalAsync(client, [Municipio(Maraba, "Marabá")]);
        Guid id = await IdDoModeloAsync(client, "INSCRICAO", "PSR_INSCRICAO_SEM_PROVA");

        JsonObject formulario = await RenderizavelAsync(client, id);

        Opcoes(formulario, "MUNICIPIO_RESIDENCIA_FATOR_ATENUACAO").Should()
            .Contain(["1500131", "1504208"]).And.OnlyHaveUniqueItems("o município de duas bases aparece uma vez");
    }

    private static void ConferirOpcaoPreta(JsonObject formulario)
    {
        JsonNode preta = OpcaoDoFato(formulario, "COR_RACA", "PRETA");
        preta["descricao"]!.GetValue<string>().Should().Be("Preta");
        preta["orientacao"]!.GetValue<string>().Should().Contain("heteroidentificação", "a opção leva a orientação abaixo do nome curto");
    }

    private static void ConferirPreVisualizacaoDoBasico(JsonObject preVisualizacao)
    {
        Textos(preVisualizacao["secoes"]!, "codigo")
            .Should().Contain(ConjuntoBasicoDaInscricao.CodigoDaSecao, "a pré-visualização avalia o mesmo conteúdo do renderizável");
        Textos(preVisualizacao["itens"]!, "fatoCodigo").Should().Contain(ConjuntoBasicoDaInscricao.Fatos);
    }
}
