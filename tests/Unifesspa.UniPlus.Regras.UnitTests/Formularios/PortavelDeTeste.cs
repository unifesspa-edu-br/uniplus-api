namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Leituras;

/// <summary>Monta formulários portáveis, itens, grupos e respostas fictícios para os testes do formulário portável.</summary>
internal static class PortavelDeTeste
{
    internal static GrupoPortavel Grupo(string codigo, string subitem) =>
        new(codigo, new OcorrenciasDoGrupoDto(0, null, false), Nunca(), [Item(subitem)]);

    internal static FormularioPortavel ComGrupos(params GrupoPortavel[] grupos) =>
        new([new PaginaPortavel("INSCRICAO_DADOS", null, [], grupos)], [], [], []);

    internal static FormularioPortavel ComGrupo(int minimo, int? maximo) =>
        new(
            [new PaginaPortavel("INSCRICAO_DADOS", null, [], [new GrupoPortavel("FAMILIA", new OcorrenciasDoGrupoDto(minimo, maximo, false), Nunca(), [Item("NOME")])])],
            [], [], []);

    internal static ItemPortavel ItemComRestricao(RestricaoValorDto restricao)
    {
        ItemPortavel item = Item("RENDA");
        return item with { Regras = item.Regras with { Restricoes = [restricao] } };
    }

    internal static ExibicaoEObrigatoriedadeDto Nunca(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? exibicao = null) =>
        new(exibicao, new ObrigatoriedadeDto("NUNCA", null));

    internal static ItemPortavel ItemComRegras(RegrasDoCampoDto regras) => Item("RENDA") with { Regras = regras };

    internal static ObrigatoriedadeDto Quando(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>> predicado) => new("QUANDO", predicado);

    internal static AvaliacaoFormulario Avaliar(FormularioPortavel formulario, IReadOnlyDictionary<string, JsonElement> respostas)
    {
        Result<AvaliacaoFormulario> avaliacao = formulario.Avaliar(new EntradaAvaliacaoFormulario(
            respostas, new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, Regras.ValueObjects.FatoResolvido>(StringComparer.Ordinal), Envio: null));
        avaliacao.IsSuccess.Should().BeTrue(avaliacao.Error?.Message);
        return avaliacao.Value!;
    }

    internal static FormularioPortavel Formulario(params ItemPortavel[] itens) =>
        new([new PaginaPortavel("INSCRICAO_DADOS", null, itens, [])], [], [], []);

    internal static ItemPortavel Item(
        string fato,
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? exibicao = null,
        string obrigatoriedade = "NUNCA",
        IReadOnlyList<string>? oferta = null) =>
        new(fato, new RegrasDoCampoDto(exibicao, new ObrigatoriedadeDto(obrigatoriedade, null), [], null), oferta, null);

    internal static Dictionary<string, JsonElement> Respostas(params (string Fato, JsonElement Valor)[] respostas) =>
        respostas.ToDictionary(static r => r.Fato, static r => r.Valor, StringComparer.Ordinal);

    internal static JsonElement Texto(string valor) => JsonSerializer.SerializeToElement(valor);
}
