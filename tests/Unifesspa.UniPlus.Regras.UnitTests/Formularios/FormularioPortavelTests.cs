namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;

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

    private static AvaliacaoFormulario Avaliar(FormularioPortavel formulario, IReadOnlyDictionary<string, JsonElement> respostas)
    {
        Result<AvaliacaoFormulario> avaliacao = formulario.Avaliar(new EntradaAvaliacaoFormulario(
            respostas, new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, Regras.ValueObjects.FatoResolvido>(StringComparer.Ordinal)));
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
