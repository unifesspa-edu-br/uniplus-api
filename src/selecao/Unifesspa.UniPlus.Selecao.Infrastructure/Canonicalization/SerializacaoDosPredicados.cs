namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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

using LinhaDoPredicado = (int Clausula, string Fato, Unifesspa.UniPlus.Regras.Enums.Operador Operador, System.Text.Json.JsonElement Valor);

/// <summary>
/// Projeta no envelope os predicados em forma normal disjuntiva: as condições, os valores de átomo
/// e o predicado de obrigatoriedade.
/// </summary>
internal static class SerializacaoDosPredicados
{
    internal static IEnumerable<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> LinhasDoPredicado(PredicadoDnf predicado) =>
        predicado.Clausulas.SelectMany(static (clausula, indice) =>
            clausula.Condicoes.Select(condicao => (indice, condicao.Fato, condicao.Operador, condicao.Valor)));

    internal static JsonArray? SerializarDnf(IEnumerable<LinhaDoPredicado> condicoes)
    {
        List<LinhaDoPredicado> lista = [.. condicoes];

        return lista.Count == 0
            ? null
            : EscritaCanonica.OrdenarPorConteudo([.. lista.GroupBy(static c => c.Clausula).Select(SerializarClausula)]);
    }

    private static JsonArray SerializarClausula(IGrouping<int, LinhaDoPredicado> clausula) =>
        EscritaCanonica.OrdenarPorConteudo(clausula.Select(static c => new JsonObject
        {
            ["codigoDoDado"] = HashCanonicalComputer.NormalizeNfc(c.Fato),
            ["operador"] = c.Operador.Codigo(),
            ["valor"] = SerializarValorDeAtomo(c.Operador, c.Valor),
        }));

    internal static JsonNode? SerializarValorDeAtomo(Operador operador, JsonElement valor)
    {
        if (operador is not (Operador.Em or Operador.NaoEm))
        {
            return JsonNode.Parse(valor.GetRawText());
        }

        return EscritaCanonica.OrdenarPorConteudo([.. valor.EnumerateArray().Select(item => AlternativaEscalar(operador, item))]);
    }

    private static JsonValue AlternativaEscalar(Operador operador, JsonElement item) =>
        JsonNode.Parse(item.GetRawText()) as JsonValue
        ?? throw new InvalidOperationException(
            $"O operador {operador.Codigo()} carrega uma alternativa que não é um valor escalar — " +
            "PredicadoDnfValidador já deveria ter recusado a condição antes da publicação.");

    internal static JsonObject SerializarPredicadoObrigatoriedade(PredicadoObrigatoriedade predicado)
    {
        (string tipo, JsonObject parametros) = TipoEParametros(predicado);

        return new JsonObject
        {
            ["tipo"] = tipo,
            ["parametros"] = parametros,
        };
    }

    private static (string Tipo, JsonObject Parametros) TipoEParametros(PredicadoObrigatoriedade predicado) => predicado switch
    {
        EtapaObrigatoria p => ("etapaObrigatoria", new JsonObject { ["tipoEtapaCodigo"] = HashCanonicalComputer.NormalizeNfc(p.TipoEtapaCodigo) }),
        ModalidadesMinimas p => ("modalidadesMinimas", new JsonObject { ["codigos"] = EscritaCanonica.TextosEmOrdemCanonica(p.Codigos) }),
        DesempateDeveIncluir p => ("desempateDeveIncluir", new JsonObject { ["criterio"] = HashCanonicalComputer.NormalizeNfc(p.Criterio) }),
        DocumentoObrigatorioParaModalidade p => ("documentoObrigatorioParaModalidade", ParametrosDoDocumentoPorModalidade(p)),
        AtendimentoDisponivel p => ("atendimentoDisponivel", new JsonObject { ["necessidades"] = EscritaCanonica.TextosEmOrdemCanonica(p.Necessidades) }),
        ConcorrenciaDuplaObrigatoria => ("concorrenciaDuplaObrigatoria", []),
        Customizado p => ("customizado", new JsonObject { ["parametros"] = JsonNode.Parse(p.Parametros.GetRawText()) }),
        _ => throw new InvalidOperationException(
            $"Variante de {nameof(PredicadoObrigatoriedade)} não reconhecida: {predicado.GetType()}."),
    };

    private static JsonObject ParametrosDoDocumentoPorModalidade(DocumentoObrigatorioParaModalidade p) => new()
    {
        ["modalidade"] = HashCanonicalComputer.NormalizeNfc(p.Modalidade),
        ["tipoDocumento"] = HashCanonicalComputer.NormalizeNfc(p.TipoDocumento),
    };
}
