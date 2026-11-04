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
/// Projeta no envelope os predicados em forma normal disjuntiva: as condições, os valores de átomo
/// e o predicado de obrigatoriedade.
/// </summary>
internal static class SerializacaoDosPredicados
{
    internal static IEnumerable<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> LinhasDoPredicado(PredicadoDnf predicado) =>
        predicado.Clausulas.SelectMany(static (clausula, indice) =>
            clausula.Condicoes.Select(condicao => (indice, condicao.Fato, condicao.Operador, condicao.Valor)));

    internal static JsonArray? SerializarDnf(
        IEnumerable<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> condicoes)
    {
        List<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> lista = [.. condicoes];
        if (lista.Count == 0)
        {
            return null;
        }

        List<JsonArray> clausulas = [];
        foreach (IGrouping<int, (int Clausula, string Fato, Operador Operador, JsonElement Valor)> clausula
            in lista.GroupBy(static c => c.Clausula))
        {
            clausulas.Add(EscritaCanonica.OrdenarPorConteudo(clausula.Select(static c => new JsonObject
            {
                ["fato"] = HashCanonicalComputer.NormalizeNfc(c.Fato),
                ["operador"] = c.Operador.ToCodigo(),
                ["valor"] = SerializarValorDeAtomo(c.Operador, c.Valor),
            })));
        }

        return EscritaCanonica.OrdenarPorConteudo(clausulas);
    }

    internal static JsonNode? SerializarValorDeAtomo(Operador operador, JsonElement valor)
    {
        if (operador is not (Operador.Em or Operador.NaoEm))
        {
            return JsonNode.Parse(valor.GetRawText());
        }

        List<JsonValue> alternativas = [];
        foreach (JsonElement item in valor.EnumerateArray())
        {
            if (JsonNode.Parse(item.GetRawText()) is not JsonValue alternativa)
            {
                throw new InvalidOperationException(
                    $"O operador {operador.ToCodigo()} carrega uma alternativa que não é um valor escalar — " +
                    "PredicadoDnfValidador já deveria ter recusado a condição antes da publicação.");
            }

            alternativas.Add(alternativa);
        }

        return EscritaCanonica.OrdenarPorConteudo(alternativas);
    }

    internal static JsonObject SerializarPredicadoObrigatoriedade(PredicadoObrigatoriedade predicado)
    {
        (string tipo, JsonObject args) = predicado switch
        {
            EtapaObrigatoria p => ("etapaObrigatoria", new JsonObject { ["tipoEtapaCodigo"] = HashCanonicalComputer.NormalizeNfc(p.TipoEtapaCodigo) }),
            ModalidadesMinimas p => ("modalidadesMinimas", new JsonObject { ["codigos"] = EscritaCanonica.TextosEmOrdemCanonica(p.Codigos) }),
            DesempateDeveIncluir p => ("desempateDeveIncluir", new JsonObject { ["criterio"] = HashCanonicalComputer.NormalizeNfc(p.Criterio) }),
            DocumentoObrigatorioParaModalidade p => ("documentoObrigatorioParaModalidade", new JsonObject
            {
                ["modalidade"] = HashCanonicalComputer.NormalizeNfc(p.Modalidade),
                ["tipoDocumento"] = HashCanonicalComputer.NormalizeNfc(p.TipoDocumento),
            }),
            AtendimentoDisponivel p => ("atendimentoDisponivel", new JsonObject { ["necessidades"] = EscritaCanonica.TextosEmOrdemCanonica(p.Necessidades) }),
            ConcorrenciaDuplaObrigatoria => ("concorrenciaDuplaObrigatoria", []),
            Customizado p => ("customizado", new JsonObject { ["parametros"] = JsonNode.Parse(p.Parametros.GetRawText()) }),
            _ => throw new InvalidOperationException(
                $"Variante de {nameof(PredicadoObrigatoriedade)} não reconhecida: {predicado.GetType()}."),
        };

        return new JsonObject
        {
            ["tipo"] = tipo,
            ["args"] = args,
        };
    }
}
