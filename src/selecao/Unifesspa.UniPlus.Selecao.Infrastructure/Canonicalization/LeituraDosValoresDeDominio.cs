namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using LinhaDoPredicado = (int Clausula, string Fato, Unifesspa.UniPlus.Regras.Enums.Operador Operador, System.Text.Json.JsonElement Valor);

/// <summary>
/// Leitura dos valores declarados de um domínio e dos valores selecionáveis de um fato de seleção.
/// </summary>
internal static class LeituraDosValoresDeDominio
{
    private static readonly string[] ChavesDoValorDeDominio = ["valorCodigo", "descricao", "ordem"];

    private static readonly string[] ChavesDoValorSelecionavel = ["valorCodigo", "descricao", "ordem", "orientacao"];

    /// <summary>
    /// Mesma técnica de <see cref="LeituraDosMetadadosDosFatos.LerValoresDominio"/> para o campo nulo-ou-array. Cada item
    /// exige <c>ordem</c> (UNI-REQ-0072) — não negativa — e <c>valorCodigo</c> não
    /// pode se repetir dentro do array: o encoder nunca emite duas entradas para o mesmo valor.
    /// </summary>
    internal static IReadOnlyList<ValorDominioDeclaradoCongelado>? LerValoresDominioDeclarados(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        string path = $"{pathPai}.valoresDominioDeclarados";
        if (item["valoresDominioDeclarados"] is not JsonNode node)
        {
            return null;
        }

        return node is JsonArray array
            ? LerValoresDeclarados(leitor, array, path, comOrientacao: false)
            : leitor.Recusar<IReadOnlyList<ValorDominioDeclaradoCongelado>?>($"'{path}' deveria ser um array ou null.");
    }

    private static List<ValorDominioDeclaradoCongelado>? LerValoresDeclarados(LeitorEnvelope leitor, JsonArray array, string path, bool comOrientacao)
    {
        List<ValorDominioDeclaradoCongelado> valores = [];
        HashSet<string> codigos = new(StringComparer.Ordinal);
        for (int i = 0; i < array.Count; i++)
        {
            ValorDominioDeclaradoCongelado? valor = LerValorDeclarado(leitor, leitor.ItemObjeto(array, i, path), $"{path}[{i}]", comOrientacao);
            if (valor is null)
            {
                return null;
            }

            if (!codigos.Add(valor.Codigo))
            {
                leitor.Recusar<object>($"'{path}': o valor '{valor.Codigo}' aparece mais de uma vez.");
                return null;
            }

            valores.Add(valor);
        }

        return valores;
    }

    private static ValorDominioDeclaradoCongelado? LerValorDeclarado(LeitorEnvelope leitor, JsonObject valorItem, string itemPath, bool comOrientacao)
    {
        leitor.ExigirChaves(valorItem, itemPath, comOrientacao ? ChavesDoValorSelecionavel : ChavesDoValorDeDominio);

        string codigo = leitor.TextoNaoVazio(valorItem, "valorCodigo", itemPath);
        string? descricao = leitor.TextoOpcional(valorItem, "descricao", itemPath);
        int ordem = leitor.Inteiro(valorItem, "ordem", itemPath);
        string? orientacao = comOrientacao ? leitor.TextoOpcional(valorItem, "orientacao", itemPath) : null;
        if (leitor.Falhou)
        {
            return null;
        }

        return ordem < 0
            ? leitor.Recusar<ValorDominioDeclaradoCongelado?>($"'{itemPath}.ordem' não pode ser negativa.")
            : new ValorDominioDeclaradoCongelado(codigo, descricao, ordem, orientacao);
    }

    /// <summary>
    /// As opções que o processo declarou, reconstruídas dos valores selecionáveis
    /// congelados dos fatos coletados cuja fonte é o processo — o envelope não guarda uma segunda
    /// cópia delas. As dos fatos de atendimento não entram: vêm da oferta, reposta à parte.
    /// </summary>
    internal static Result<IReadOnlyList<OpcaoDeclaradaFato>> ReconstruirOpcoesDeclaradas(
        IReadOnlyList<FatoColetado> fatos,
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis)
    {
        List<OpcaoDeclaradaFato> opcoes = [];
        foreach (FatoColetado fato in fatos)
        {
            if (!fato.OpcoesDoProcesso || OfertaAtendimentoEspecializado.GereOpcoesDoFato(fato.FatoCodigo))
            {
                continue;
            }

            foreach (ValorDominioDeclaradoCongelado valor in valoresSelecionaveis.GetValueOrDefault(fato.FatoCodigo) ?? [])
            {
                Result<OpcaoDeclaradaFato> opcao = OpcaoDeclaradaFato.Criar(
                    fato.FatoCodigo, valor.Codigo, valor.Descricao ?? string.Empty, valor.Ordem);
                if (opcao.IsFailure)
                {
                    return Result<IReadOnlyList<OpcaoDeclaradaFato>>.Failure(opcao.Error!);
                }

                opcoes.Add(opcao.Value!);
            }
        }

        return Result<IReadOnlyList<OpcaoDeclaradaFato>>.Success(opcoes);
    }

    /// <summary>
    /// Os valores selecionáveis de um fato coletado (UNI-REQ-0072) — bicondicional
    /// com <paramref name="tipoRenderizacao"/> : <c>SELECAO_UNICA</c>/
    /// <c>SELECAO_MULTIPLA</c> exige array com cardinalidade mínima 1 (nunca vazio);
    /// <c>BOOLEANO</c>/<c>NUMERO</c> exige <see langword="null"/>. Um envelope que descumpra a
    /// bicondicional em qualquer sentido — seletor mudo (<c>null</c> ou <c>[]</c> onde deveria
    /// ter opção), ou vocabulário pendurado num campo booleano/numérico — é malformado. Cada
    /// item exige <c>ordem</c> não negativa e
    /// <c>valorCodigo</c> sem repetição, mesma disciplina que
    /// <c>LerValoresDominioDeclarados</c> aplica a <c>valoresDominioDeclarados</c>.
    /// </summary>
    internal static IReadOnlyList<ValorDominioDeclaradoCongelado>? LerValoresSelecionaveis(
        LeitorEnvelope leitor, JsonObject item, string pathPai, TipoRenderizacao tipoRenderizacao)
    {
        string path = $"{pathPai}.valoresSelecionaveis";
        bool ehFatoDeSelecao = tipoRenderizacao.EhSelecao();

        if (item["valoresSelecionaveis"] is not JsonNode node)
        {
            return ehFatoDeSelecao
                ? leitor.Recusar<IReadOnlyList<ValorDominioDeclaradoCongelado>?>($"'{path}' é null, mas o tipo de renderização é de seleção — a bicondicional exige um array.")
                : null;
        }

        if (node is not JsonArray array)
        {
            return leitor.Recusar<IReadOnlyList<ValorDominioDeclaradoCongelado>?>($"'{path}' deveria ser um array ou null.");
        }

        if (!ehFatoDeSelecao)
        {
            return leitor.Recusar<IReadOnlyList<ValorDominioDeclaradoCongelado>?>(
                $"'{path}' é um array, mas o tipo de renderização '{tipoRenderizacao}' não é de seleção — a bicondicional exige null.");
        }

        List<ValorDominioDeclaradoCongelado>? valores = LerValoresDeclarados(leitor, array, path, comOrientacao: true);

        // Um seletor publicado sem opção nenhuma não é respondível: o array vazio é malformado.
        return valores is { Count: 0 }
            ? leitor.Recusar<IReadOnlyList<ValorDominioDeclaradoCongelado>?>($"'{path}' é um array vazio, mas o tipo de renderização é de seleção — a cardinalidade mínima é 1.")
            : valores;
    }
}
