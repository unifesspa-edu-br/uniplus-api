namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
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
            Item("NOME_ALDEIA", exibicao: [[new CondicaoPrecondicaoInput("TIPO_ENDERECO", "IGUAL", Texto("ALDEIA"))]]));

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

    public static TheoryData<FormularioPortavel, string> Malformados() => new()
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

    public static TheoryData<FormularioPortavel, string, string> MalformadosComDetalhe() => new()
    {
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
            Formulario(Item("RENDA")) with { Termos = [new TermoPortavel("", null, "NUNCA", null)] },
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
            new FormularioPortavel([new EtapaPortavel(" ", null, [], [])], [], [], []),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Uma etapa do formulário não tem código."
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
            Formulario(ItemComRestricao(new RestricaoValorInput(RestricaoValorJson.FaixaNumerica, Minimo: 10, Maximo: 1))),
            RestricaoValorErrorCodes.LimitesIncoerentes,
            "O mínimo (10) é maior que o máximo (1)."
        },
        {
            Formulario(ItemComRestricao(new RestricaoValorInput(RestricaoValorJson.OpcoesDasRespostas, Fatos: []))),
            RestricaoValorErrorCodes.FatosVazios,
            "As opções formadas pelas respostas precisam citar ao menos um fato."
        },
        {
            Formulario(ItemComRestricao(new RestricaoValorInput(
                RestricaoValorJson.OpcoesPermitidas, Entradas: [new OpcoesCondicionadasInput(null, [])]))),
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

    public static TheoryData<FormularioPortavel, string, string> ComDoisDefeitos() => new()
    {
        {
            Formulario(Item("RENDA", obrigatoriedade: "QUANDO"), Item(" ")),
            FormularioPortavelErrorCodes.ObrigatoriedadeInvalida,
            "A obrigatoriedade do campo 'RENDA' é SEMPRE ou NUNCA sem predicado, ou QUANDO com ele."
        },
        {
            Formulario(Item("RENDA", obrigatoriedade: "QUANDO")) with
            {
                Termos = [new TermoPortavel("TERMO", [[new CondicaoPrecondicaoInput("X", "IGUAL", default)]], "NUNCA", null)],
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
            new FormularioPortavel([new EtapaPortavel(" ", null, [Item(" ")], [])], [], [], []),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Um campo do formulário não tem o código do fato."
        },
        {
            Formulario(Item("RENDA"), Item("RENDA")) with { Termos = [new TermoPortavel("", null, "NUNCA", null)] },
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
        new(codigo, null, "NUNCA", null, 0, null, false, [Item(subitem)]);

    private static FormularioPortavel ComGrupos(params GrupoPortavel[] grupos) =>
        new([new EtapaPortavel("INSCRICAO:DADOS", null, [], grupos)], [], [], []);

    private static FormularioPortavel ComGrupo(int minimo, int? maximo) =>
        new(
            [new EtapaPortavel("INSCRICAO:DADOS", null, [], [new GrupoPortavel("FAMILIA", null, "NUNCA", null, minimo, maximo, false, [Item("NOME")])])],
            [], [], []);

    private static ItemPortavel ItemComRestricao(RestricaoValorInput restricao) =>
        Item("RENDA") with { Restricoes = [restricao] };

    [Fact(DisplayName = "Elemento nulo dentro das regras é recusado com o caminho dele, sem estourar")]
    public void ElementoNulo_RecusadoComOCaminho()
    {
        FormularioPortavel formulario = Formulario(Item("RENDA"), null!);

        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.Error!.Code.Should().Be(FormularioPortavelErrorCodes.EstruturaInvalida);
        definicao.Error.Message.Should().Contain("etapas[0].itens[1]");
    }

    private static AvaliacaoFormulario Avaliar(FormularioPortavel formulario, IReadOnlyDictionary<string, JsonElement> respostas)
    {
        Result<AvaliacaoFormulario> avaliacao = formulario.Avaliar(new EntradaAvaliacaoFormulario(
            respostas, new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, Regras.ValueObjects.FatoResolvido>(StringComparer.Ordinal), Envio: null));
        avaliacao.IsSuccess.Should().BeTrue(avaliacao.Error?.Message);
        return avaliacao.Value!;
    }

    private static FormularioPortavel Formulario(params ItemPortavel[] itens) =>
        new([new EtapaPortavel("INSCRICAO:DADOS", null, itens, [])], [], [], []);

    private static ItemPortavel Item(
        string fato,
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? exibicao = null,
        string obrigatoriedade = "NUNCA",
        IReadOnlyList<string>? oferta = null) =>
        new(fato, exibicao, obrigatoriedade, null, [], null, oferta);

    private static Dictionary<string, JsonElement> Respostas(params (string Fato, JsonElement Valor)[] respostas) =>
        respostas.ToDictionary(static r => r.Fato, static r => r.Valor, StringComparer.Ordinal);

    private static JsonElement Texto(string valor) => JsonSerializer.SerializeToElement(valor);
}
