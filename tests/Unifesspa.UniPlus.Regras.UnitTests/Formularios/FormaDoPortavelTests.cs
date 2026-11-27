namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Leituras;
using Unifesspa.UniPlus.Regras.Serializacao;

using static Unifesspa.UniPlus.Regras.UnitTests.Formularios.PortavelDeTeste;

/// <summary>
/// A forma do documento vindo de fora: elemento nulo e grupo sem ocorrências são recusados no caminho do elemento.
/// </summary>
public sealed class FormaDoPortavelTests
{
    private static readonly JsonSerializerOptions OpcoesDoFio = new(JsonSerializerDefaults.Web);

    [Theory(DisplayName = "Elemento nulo dentro do bloco de regras é recusado com o caminho aninhado")]
    [MemberData(nameof(NulosAninhados))]
    public void ElementoNuloNoBlocoDeRegras_RecusadoComOCaminhoAninhado(FormularioPortavel formulario, string caminho)
    {
        ArgumentNullException.ThrowIfNull(formulario);

        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == caminho && e.Error.Code == FormularioPortavelErrorCodes.EstruturaInvalida);
    }

    public static readonly TheoryData<FormularioPortavel, string> NulosAninhados = new()
    {
        { Formulario(Item("RENDA", exibicao: [null!])), "paginas[0].itens[0].regras.exibicao[0]" },
        { Formulario(ItemComRegras(new RegrasDoCampoDto(null, Quando([[null!]]), [], null))), "paginas[0].itens[0].regras.obrigatoriedade.predicado[0][0]" },
        { Formulario(ItemComRegras(new RegrasDoCampoDto(null, new ObrigatoriedadeDto("NUNCA", null), [], new ImpedimentoDto([null!], "Mensagem")))), "paginas[0].itens[0].regras.impedimento.quando[0]" },
        {
            Formulario(ItemComRestricao(new RestricaoValorDto(TipoRestricaoValor.OpcoesPermitidas.Codigo(), null, null, [null!], null, null, null))),
            "paginas[0].itens[0].regras.restricoes[0].entradas[0]"
        },
        {
            Formulario(ItemComRestricao(new RestricaoValorDto(TipoRestricaoValor.OpcoesDasRespostas.Codigo(), null, null, null, [null!], null, null))),
            "paginas[0].itens[0].regras.restricoes[0].fatos[0]"
        },
        { Formulario(Item("RENDA")) with { Termos = [new TermoPortavel("TERMO", Nunca([null!]))] }, "termos[0].regras.exibicao[0]" },
        { new FormularioPortavel([null!], [], [], []), "paginas[0]" },
        { Formulario(Item("RENDA")) with { Termos = [null!] }, "termos[0]" },
        { Formulario(Item("RENDA")) with { Derivacoes = [null!] }, "derivacoes[0]" },
        { Formulario(Item("RENDA")) with { Derivacoes = [new DerivacaoPortavel("FATO", false, [null!])] }, "derivacoes[0].regras[0]" },
        { Formulario(Item("RENDA")) with { Agregados = [null!] }, "agregados[0]" },
        { Formulario(ItemComRegras(new RegrasDoCampoDto(null, new ObrigatoriedadeDto("NUNCA", null), [null!], null))), "paginas[0].itens[0].regras.restricoes[0]" },
        { Formulario(Item("RENDA", oferta: [null!])), "paginas[0].itens[0].oferta[0]" },
        {
            Formulario(ItemComRestricao(new RestricaoValorDto(TipoRestricaoValor.OpcoesPermitidas.Codigo(), null, null, [new OpcoesCondicionadasDto(null, [null!])], null, null, null))),
            "paginas[0].itens[0].regras.restricoes[0].entradas[0].valores[0]"
        },
        {
            ComGrupos(Grupo("FAMILIA", "NOME") with { Regras = new ExibicaoEObrigatoriedadeDto(null, Quando([null!])) }),
            "paginas[0].grupos[0].regras.obrigatoriedade.predicado[0]"
        },
    };

    [Theory(DisplayName = "Grupo sem ocorrências é recusado no caminho do bloco")]
    [InlineData(false)]
    [InlineData(true)]
    public void ParaDefinicao_GrupoSemOcorrencias_RecusaNoCaminho(bool explicito)
    {
        string ocorrencias = explicito ? "\"ocorrencias\":null," : "";
        string json = "{\"paginas\":[{\"codigo\":\"INSCRICAO_DADOS\",\"itens\":[],\"grupos\":[{\"codigo\":\"FAMILIA\"," +
            ocorrencias + "\"regras\":{\"obrigatoriedade\":{\"tipo\":\"NUNCA\"}},\"subitens\":[]}]}],\"termos\":[],\"derivacoes\":[],\"agregados\":[]}";
        FormularioPortavel formulario = JsonSerializer.Deserialize<FormularioPortavel>(json, OpcoesDoFio)!;

        Result<DefinicaoFormulario> resultado = formulario.ParaDefinicao();

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "paginas[0].grupos[0].ocorrencias" && e.Error.Code == GrupoFormularioErrorCodes.OcorrenciasObrigatorias);
    }

    [Fact]
    public void Avaliar_GrupoSemOcorrencias_PreservaOCaminho()
    {
        FormularioPortavel formulario = new([new PaginaPortavel("DADOS", null, [],
            [new GrupoPortavel("FAMILIA", null!, Nunca(), [])])], [], [], []);
        EntradaAvaliacaoFormulario entrada = new(new Dictionary<string, JsonElement>(), new HashSet<string>(),
            new Dictionary<string, Regras.ValueObjects.FatoResolvido>(), null);

        Result<AvaliacaoFormulario> resultado = formulario.Avaliar(entrada);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("paginas[0].grupos[0].ocorrencias");
    }

    [Fact(DisplayName = "Elemento nulo dentro das regras é recusado com o caminho dele, sem estourar")]
    public void ElementoNulo_RecusadoComOCaminho()
    {
        FormularioPortavel formulario = Formulario(Item("RENDA"), null!);

        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.Errors.Should().ContainSingle().Which.Field.Should().Be("paginas[0].itens[1]");
    }

    [Fact(DisplayName = "A mensagem do elemento nulo não traz o caminho, que fica no campo")]
    public void ElementoNulo_MensagemExata()
    {
        Result<DefinicaoFormulario> definicao = Formulario(Item("RENDA"), null!).ParaDefinicao();

        definicao.Error!.Message.Should().Be("O elemento das regras é nulo.");
    }

    [Fact(DisplayName = "Dois nulos e dois grupos sem ocorrências voltam em quatro recusas, na ordem do documento")]
    public void QuatroRecusas_VoltamNaOrdemDoDocumentoSemFundir()
    {
        GrupoPortavel semOcorrencias = Grupo("FAMILIA", "NOME") with { Ocorrencias = null! };
        FormularioPortavel formulario = new([new PaginaPortavel("INSCRICAO_DADOS", null, [Item("RENDA"), null!, null!], [semOcorrencias, semOcorrencias])], [], [], []);

        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.Errors.Select(static e => e.Field).Should().Equal(
            "paginas[0].itens[1]", "paginas[0].itens[2]", "paginas[0].grupos[0].ocorrencias", "paginas[0].grupos[1].ocorrencias");
    }

    [Fact(DisplayName = "No grupo, as ocorrências são recusadas antes do subitem nulo")]
    public void Grupo_OcorrenciasAntesDoSubitemNulo()
    {
        GrupoPortavel grupo = Grupo("FAMILIA", "NOME") with { Ocorrencias = null!, Subitens = [null!] };

        Result<DefinicaoFormulario> definicao = ComGrupos(grupo).ParaDefinicao();

        definicao.Errors.Select(static e => e.Field).Should().Equal("paginas[0].grupos[0].ocorrencias", "paginas[0].grupos[0].subitens[0]");
    }

    [Fact(DisplayName = "A forma recusada esconde a obrigatoriedade inválida de outra página")]
    public void FormaRecusada_NaoConverteOsElementos()
    {
        ItemPortavel obrigatoriedadeInvalida = Item("RENDA", obrigatoriedade: "QUANDO");
        FormularioPortavel formulario = new(
            [new PaginaPortavel("PAGINA_UM", null, [null!], []), new PaginaPortavel("PAGINA_DOIS", null, [obrigatoriedadeInvalida], [])], [], [], []);

        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.Errors.Should().ContainSingle().Which.Field.Should().Be("paginas[0].itens[0]");
    }

    [Fact(DisplayName = "Condição nula na simulação é recusada pela forma, antes da leitura que lançaria exceção")]
    public void Avaliar_CondicaoNulaNoPredicado_RecusadaNoCaminhoDaCondicao()
    {
        CondicaoPrecondicaoDto condicao = new("FATO_FICTICIO", "IGUAL", JsonSerializer.SerializeToElement("x"));
        FormularioPortavel formulario = new(
            [new PaginaPortavel("INSCRICAO_DADOS", [[condicao], [condicao, null!]], [], [])], [], [], []);
        EntradaAvaliacaoFormulario entrada = new(new Dictionary<string, JsonElement>(), new HashSet<string>(),
            new Dictionary<string, Regras.ValueObjects.FatoResolvido>(), null);

        Result<AvaliacaoFormulario> resultado = formulario.Avaliar(entrada);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "paginas[0].exibicao[1][1]" && e.Error.Code == FormularioPortavelErrorCodes.EstruturaInvalida);
    }
}
