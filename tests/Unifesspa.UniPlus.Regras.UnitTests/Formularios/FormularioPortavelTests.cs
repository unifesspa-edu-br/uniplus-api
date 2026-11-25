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

/// <summary>
/// O formulário portável vindo de fora — um arquivo importado, um caso do corpus compartilhado —: o que
/// a conversão de ida não exercita, porque a definição montada pelo módulo nunca chega assim.
/// </summary>
public sealed class FormularioPortavelTests
{
    [Fact]
    public void PredicadoSemClausula_ContinuaFalso_ENaoViraSemCondicao()
    {
        FormularioPortavel formulario = Formulario(Item("DETALHE", exibicao: []));

        AvaliacaoFormulario avaliacao = Avaliar(formulario, Respostas());

        avaliacao.Itens.Single().Visivel.Should().Be(Ternario.Falso, "a lista vazia é o predicado sem cláusula, que avalia falso");
    }

    [Fact]
    public void RespostaForaDaOferta_NaoVale()
    {
        FormularioPortavel formulario = Formulario(
            Item("TIPO_ENDERECO", oferta: ["URBANO", "ALDEIA"]),
            Item("NOME_ALDEIA", exibicao: [[new CondicaoPrecondicaoDto("TIPO_ENDERECO", "IGUAL", Texto("ALDEIA"))]]));

        AvaliacaoFormulario dentro = Avaliar(formulario, Respostas(("TIPO_ENDERECO", Texto("ALDEIA"))));
        AvaliacaoFormulario fora = Avaliar(formulario, Respostas(("TIPO_ENDERECO", Texto("QUILOMBO"))));

        dentro.Itens[1].Visivel.Should().Be(Ternario.Verdadeiro);
        fora.Fatos["TIPO_ENDERECO"].Estado.Should().Be(EstadoFato.Indeterminado, "o código fora da oferta é descartado como se não houvesse resposta");
    }

    [Theory]
    [MemberData(nameof(Malformados))]
    public void ConteudoMalformado_ERecusadoComACausa(FormularioPortavel formulario, string codigo)
    {
        ArgumentNullException.ThrowIfNull(formulario);
        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.IsFailure.Should().BeTrue();
        definicao.Error!.Code.Should().Be(codigo);
    }

    private static readonly JsonSerializerOptions OpcoesDoFio = new(JsonSerializerDefaults.Web);

    public static readonly TheoryData<FormularioPortavel, string> Malformados = new()
    {
        { Formulario(Item("RENDA", obrigatoriedade: "QUANDO")), FormularioPortavelErrorCodes.ObrigatoriedadeInvalida },
        { Formulario(Item("RENDA"), Item("RENDA")), FormularioPortavelErrorCodes.EstruturaInvalida },
        { Formulario(Item("CONTATO") with { Formato = "TELEGRAMA" }), FormularioPortavelErrorCodes.EstruturaInvalida },
        {
            Formulario(Item("ELEGIVEL")) with { Derivacoes = [new DerivacaoPortavel("COTISTA", true, [new RegraDerivacaoPortavel(null!, null)])] },
            FormularioPortavelErrorCodes.DerivacaoInvalida
        },
        {
            Formulario(Item("ELEGIVEL")) with { Agregados = [new AgregadoPortavel("ALGUEM", "FAMILIA", "SEM_RENDA", "SOMA")] },
            FormularioPortavelErrorCodes.OperacaoDeAgregadoInvalida
        },
    };

    [Theory(DisplayName = "A recusa do conteúdo malformado chega como texto em português, sem texto da exceção do framework")]
    [MemberData(nameof(MalformadosComDetalhe))]
    public void ConteudoMalformado_TemODetalheExato(FormularioPortavel formulario, string codigo, string detalhe)
    {
        ArgumentNullException.ThrowIfNull(formulario);
        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.Error!.Code.Should().Be(codigo);
        definicao.Error.Message.Should().Be(detalhe);
    }

    [Fact(DisplayName = "Derivação sem regra é recusada como derivação inválida, com a mensagem da derivação")]
    public void ParaDefinicao_DerivacaoSemRegras_RecusaComoDerivacaoInvalida()
    {
        FormularioPortavel formulario = Formulario(Item("ELEGIVEL")) with { Derivacoes = [new DerivacaoPortavel("COTISTA", true, [])] };

        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.Error!.Code.Should().Be(FormularioPortavelErrorCodes.DerivacaoInvalida);
        definicao.Error.Message.Should().Be("A derivação de 'COTISTA' precisa de ao menos uma regra.");
    }

    public static readonly TheoryData<FormularioPortavel, string, string> MalformadosComDetalhe = new()
    {
        {
            new FormularioPortavel([new PaginaPortavel("DADOS", null, [], []), new PaginaPortavel("DADOS", null, [], [])], [], [], []),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "O código 'DADOS' aparece mais de uma vez como página."
        },
        {
            Formulario(Item("RENDA"), Item("RENDA")),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "O código 'RENDA' aparece mais de uma vez como fato produzido ou grupo do formulário."
        },
        {
            Formulario(Item("CONTATO") with { Formato = "TELEGRAMA" }),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "O formato de texto 'TELEGRAMA' não é do vocabulário do catálogo."
        },
        {
            Formulario(Item("ELEGIVEL")) with { Agregados = [new AgregadoPortavel("ALGUEM", "FAMILIA", "SEM_RENDA", "EXISTE")] },
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "O agregado 'ALGUEM' é sobre o grupo 'FAMILIA', que o formulário não tem."
        },
        {
            Formulario(Item(" ")),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Um campo do formulário não tem o código do fato."
        },
        {
            Formulario(Item("RENDA")) with { Termos = [new TermoPortavel("", Nunca())] },
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Um termo do formulário não tem código."
        },
        {
            ComGrupo(minimo: -1, maximo: null),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Os limites de ocorrências do grupo 'FAMILIA' (-1, sem máximo) são incoerentes."
        },
        {
            ComGrupo(minimo: 2, maximo: 1),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Os limites de ocorrências do grupo 'FAMILIA' (2, 1) são incoerentes."
        },
        {
            ComGrupos(Grupo("FAMILIA", "PARENTE_A"), Grupo("FAMILIA", "PARENTE_B")),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "O código 'FAMILIA' aparece mais de uma vez como fato produzido ou grupo do formulário."
        },
        {
            new FormularioPortavel([new PaginaPortavel(" ", null, [], [])], [], [], []),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Uma página do formulário não tem código."
        },
        {
            ComGrupos(Grupo("", "PARENTE_A")),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Um grupo do formulário não tem código."
        },
        {
            Formulario(Item("RENDA")) with { Derivacoes = [new DerivacaoPortavel(" ", true, [new RegraDerivacaoPortavel([], null)])] },
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Uma derivação do formulário não tem o código do fato."
        },
        {
            Formulario(Item("RENDA")) with { Agregados = [new AgregadoPortavel("", "FAMILIA", "SEM_RENDA", "EXISTE")] },
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Um agregado do formulário não tem código."
        },
        {
            Formulario(ItemComRestricao(new RestricaoValorDto(TipoRestricaoValor.FaixaNumerica.Codigo(), 10, 1, null, null, null, null))),
            RestricaoValorErrorCodes.LimitesIncoerentes,
            "O mínimo (10) é maior que o máximo (1)."
        },
        {
            Formulario(ItemComRestricao(new RestricaoValorDto(TipoRestricaoValor.OpcoesDasRespostas.Codigo(), null, null, null, [], null, null))),
            RestricaoValorErrorCodes.FatosVazios,
            "As opções formadas pelas respostas precisam citar ao menos um fato."
        },
        {
            Formulario(ItemComRestricao(new RestricaoValorDto(
                TipoRestricaoValor.OpcoesPermitidas.Codigo(), null, null, [new OpcoesCondicionadasDto(null, [])], null, null, null))),
            RestricaoValorErrorCodes.OpcoesVazias,
            "Um grupo de opções precisa de valores não vazios."
        },
    };

    [Theory(DisplayName = "Com dois defeitos, vale a primeira recusa na ordem de leitura do portável")]
    [MemberData(nameof(ComDoisDefeitos))]
    public void ConteudoComDoisDefeitos_RecusaOPrimeiroNaOrdemDeLeitura(FormularioPortavel formulario, string codigo, string detalhe)
    {
        ArgumentNullException.ThrowIfNull(formulario);
        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.Error!.Code.Should().Be(codigo);
        definicao.Error.Message.Should().Be(detalhe);
    }

    public static readonly TheoryData<FormularioPortavel, string, string> ComDoisDefeitos = new()
    {
        {
            Formulario(Item("RENDA", obrigatoriedade: "QUANDO"), Item(" ")),
            FormularioPortavelErrorCodes.ObrigatoriedadeInvalida,
            "A obrigatoriedade do campo 'RENDA' é SEMPRE ou NUNCA sem predicado, ou QUANDO com ele."
        },
        {
            Formulario(Item("RENDA", obrigatoriedade: "QUANDO")) with
            {
                Termos = [new TermoPortavel("TERMO", Nunca([[new CondicaoPrecondicaoDto("X", "IGUAL", default)]]))],
            },
            FormularioPortavelErrorCodes.ObrigatoriedadeInvalida,
            "A obrigatoriedade do campo 'RENDA' é SEMPRE ou NUNCA sem predicado, ou QUANDO com ele."
        },
        {
            Formulario(Item("CONTATO", obrigatoriedade: "QUANDO") with { Formato = "TELEGRAMA" }),
            FormularioPortavelErrorCodes.ObrigatoriedadeInvalida,
            "A obrigatoriedade do campo 'CONTATO' é SEMPRE ou NUNCA sem predicado, ou QUANDO com ele."
        },
        {
            new FormularioPortavel([new PaginaPortavel(" ", null, [Item(" ")], [])], [], [], []),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Um campo do formulário não tem o código do fato."
        },
        {
            Formulario(Item("RENDA"), Item("RENDA")) with { Termos = [new TermoPortavel("", Nunca())] },
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Um termo do formulário não tem código."
        },
        {
            Formulario(Item("RENDA")) with { Agregados = [new AgregadoPortavel("", "FAMILIA", "SEM_RENDA", "SOMA")] },
            FormularioPortavelErrorCodes.OperacaoDeAgregadoInvalida,
            "A operação do agregado '' é EXISTE ou VALORES_PRESENTES."
        },
    };

    [Fact(DisplayName = "O formato fora do catálogo no item NOME não é recusado, porque o nome não tem formato")]
    public void FormatoForaDoCatalogo_NoNome_EIgnorado()
    {
        FormularioPortavel formulario = Formulario(Item(ConjuntoBasicoDaInscricao.Nome) with { Formato = "TELEGRAMA" });

        formulario.ParaDefinicao().IsSuccess.Should().BeTrue();
    }

    private static GrupoPortavel Grupo(string codigo, string subitem) =>
        new(codigo, new OcorrenciasDoGrupoDto(0, null, false), Nunca(), [Item(subitem)]);

    private static FormularioPortavel ComGrupos(params GrupoPortavel[] grupos) =>
        new([new PaginaPortavel("INSCRICAO:DADOS", null, [], grupos)], [], [], []);

    private static FormularioPortavel ComGrupo(int minimo, int? maximo) =>
        new(
            [new PaginaPortavel("INSCRICAO:DADOS", null, [], [new GrupoPortavel("FAMILIA", new OcorrenciasDoGrupoDto(minimo, maximo, false), Nunca(), [Item("NOME")])])],
            [], [], []);

    private static ItemPortavel ItemComRestricao(RestricaoValorDto restricao)
    {
        ItemPortavel item = Item("RENDA");
        return item with { Regras = item.Regras with { Restricoes = [restricao] } };
    }

    private static ExibicaoEObrigatoriedadeDto Nunca(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? exibicao = null) =>
        new(exibicao, new ObrigatoriedadeDto("NUNCA", null));

    [Theory(DisplayName = "Elemento nulo dentro do bloco de regras é recusado com o caminho aninhado")]
    [MemberData(nameof(NulosAninhados))]
    public void ElementoNuloNoBlocoDeRegras_RecusadoComOCaminhoAninhado(FormularioPortavel formulario, string caminho)
    {
        ArgumentNullException.ThrowIfNull(formulario);

        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.Error!.Code.Should().Be(FormularioPortavelErrorCodes.EstruturaInvalida);
        definicao.Error.Message.Should().Contain($"'{caminho}'");
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
        {
            ComGrupos(Grupo("FAMILIA", "NOME") with { Regras = new ExibicaoEObrigatoriedadeDto(null, Quando([null!])) }),
            "paginas[0].grupos[0].regras.obrigatoriedade.predicado[0]"
        },
    };

    [Fact(DisplayName = "Item sem o bloco de regras é recusado como obrigatoriedade inválida")]
    public void ParaDefinicao_ItemSemRegras_RecusaComoObrigatoriedadeInvalida()
    {
        FormularioPortavel formulario = Formulario(Item("RENDA") with { Regras = null! });

        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.Error!.Code.Should().Be(FormularioPortavelErrorCodes.ObrigatoriedadeInvalida);
        definicao.Error.Message.Should().Be("A obrigatoriedade do campo 'RENDA' é SEMPRE ou NUNCA sem predicado, ou QUANDO com ele.");
    }

    [Theory(DisplayName = "Grupo sem ocorrências é recusado no caminho do bloco")]
    [InlineData(false)]
    [InlineData(true)]
    public void ParaDefinicao_GrupoSemOcorrencias_RecusaNoCaminho(bool explicito)
    {
        string ocorrencias = explicito ? "\"ocorrencias\":null," : "";
        string json = "{\"paginas\":[{\"codigo\":\"INSCRICAO:DADOS\",\"itens\":[],\"grupos\":[{\"codigo\":\"FAMILIA\"," +
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

    private static ItemPortavel ItemComRegras(RegrasDoCampoDto regras) => Item("RENDA") with { Regras = regras };

    private static ObrigatoriedadeDto Quando(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>> predicado) => new("QUANDO", predicado);

    [Fact(DisplayName = "Elemento nulo dentro das regras é recusado com o caminho dele, sem estourar")]
    public void ElementoNulo_RecusadoComOCaminho()
    {
        FormularioPortavel formulario = Formulario(Item("RENDA"), null!);

        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.Error!.Code.Should().Be(FormularioPortavelErrorCodes.EstruturaInvalida);
        definicao.Error.Message.Should().Contain("paginas[0].itens[1]");
    }

    private static AvaliacaoFormulario Avaliar(FormularioPortavel formulario, IReadOnlyDictionary<string, JsonElement> respostas)
    {
        Result<AvaliacaoFormulario> avaliacao = formulario.Avaliar(new EntradaAvaliacaoFormulario(
            respostas, new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, Regras.ValueObjects.FatoResolvido>(StringComparer.Ordinal), Envio: null));
        avaliacao.IsSuccess.Should().BeTrue(avaliacao.Error?.Message);
        return avaliacao.Value!;
    }

    private static FormularioPortavel Formulario(params ItemPortavel[] itens) =>
        new([new PaginaPortavel("INSCRICAO:DADOS", null, itens, [])], [], [], []);

    private static ItemPortavel Item(
        string fato,
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? exibicao = null,
        string obrigatoriedade = "NUNCA",
        IReadOnlyList<string>? oferta = null) =>
        new(fato, new RegrasDoCampoDto(exibicao, new ObrigatoriedadeDto(obrigatoriedade, null), [], null), oferta, null);

    private static Dictionary<string, JsonElement> Respostas(params (string Fato, JsonElement Valor)[] respostas) =>
        respostas.ToDictionary(static r => r.Fato, static r => r.Valor, StringComparer.Ordinal);

    private static JsonElement Texto(string valor) => JsonSerializer.SerializeToElement(valor);
}
