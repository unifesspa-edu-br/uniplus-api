namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Primitivas de escrita comuns a todos os blocos do envelope: a ordenação de conjuntos pelo conteúdo
/// canônico dos itens (bytes comparados lexicograficamente) e a referência a uma regra versionada.
/// O perfil canônico e as escalas decimais também moram aqui, para que todo bloco use os mesmos.
/// </summary>
internal static class EscritaCanonica
{
    internal const int EscalaPadrao = 4;
    internal const int EscalaPercentual = 2;

    internal static readonly PerfilCanonicoV1 PerfilAtual = PerfilCanonicoV1.Instancia;

    internal static JsonArray CodigosEmOrdem(IEnumerable<string> codigos) =>
        new([.. codigos.Select(HashCanonicalComputer.NormalizeNfc).Order(StringComparer.Ordinal).Select(static c => (JsonNode)c)]);

    internal static JsonArray TextosEmOrdemCanonica(IEnumerable<string> textos) =>
        OrdenarPorConteudo(textos.Select(static t => JsonValue.Create(HashCanonicalComputer.NormalizeNfc(t))!));

    internal static JsonArray OrdenarPorConteudo(IEnumerable<JsonObject> itens)
    {
        IOrderedEnumerable<JsonObject> ordenados = itens.OrderBy(
            static item => PerfilAtual.Serializar(item),
            ComparadorLexicograficoDeBytes.Instancia);

        return new JsonArray([.. ordenados.Select(static item => (JsonNode)item)]);
    }

    internal static JsonArray OrdenarPorConteudo(IEnumerable<JsonValue> itens)
    {
        List<JsonValue> lista = [];
        foreach (JsonValue? item in itens)
        {
            if (item is null)
            {
                throw new InvalidOperationException(
                    "Um array de texto do envelope recebeu um elemento nulo antes da ordenação — nenhum dos " +
                    "conjuntos de texto simples (ocorrenciasEsperadas, valoresDominio, " +
                    "codigos, necessidades, alternativas de átomo EM/NAO_EM) admite null como item.");
            }

            lista.Add(item);
        }

        IOrderedEnumerable<JsonValue> ordenados = lista.OrderBy(
            static item => PerfilCanonicoV1.SerializarChave(item),
            ComparadorLexicograficoDeBytes.Instancia);

        return new JsonArray([.. ordenados.Select(static item => (JsonNode)item)]);
    }

    internal static JsonArray OrdenarPorConteudo(IEnumerable<JsonArray> clausulas)
    {
        IOrderedEnumerable<JsonArray> ordenadas = clausulas.OrderBy(
            static clausula => PerfilCanonicoV1.SerializarChave(clausula),
            ComparadorLexicograficoDeBytes.Instancia);

        return new JsonArray([.. ordenadas.Select(static clausula => (JsonNode)clausula)]);
    }

    internal static JsonObject SerializarReferenciaRegra(ReferenciaRegra regra) => new()
    {
        ["codigo"] = regra.Codigo,
        ["versao"] = regra.Versao,
        ["hash"] = regra.Hash,
    };
}
