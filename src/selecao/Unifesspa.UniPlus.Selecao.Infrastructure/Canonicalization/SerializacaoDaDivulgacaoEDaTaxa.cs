namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Projeta no envelope os campos publicados nas listas de resultado e a taxa de inscrição.
/// </summary>
internal static class SerializacaoDaDivulgacaoEDaTaxa
{
    internal static JsonObject SerializarDivulgacao(ProcessoSeletivo processo)
    {
        ConfiguracaoDivulgacao? divulgacao = processo.ConfiguracaoDivulgacao;
        IReadOnlyList<string> camposPublicos = divulgacao?.CamposPublicos ?? [ConfiguracaoDivulgacao.NumeroInscricao];
        bool abrevia = camposPublicos.Contains(ConfiguracaoDivulgacao.NomeAbreviado, StringComparer.Ordinal);

        return new JsonObject
        {
            ["camposPublicos"] = EscritaCanonica.OrdenarPorConteudo(camposPublicos.Select(static c => JsonValue.Create(c)!)),
            ["regraNomeAbreviado"] = abrevia ? RegrasDeNomeAbreviado.Vigente : null,
            ["justificativa"] = divulgacao?.Justificativa is { } justificativa ? HashCanonicalComputer.NormalizeNfc(justificativa) : null,
        };
    }

    internal static JsonObject SerializarTaxaInscricao(ProcessoSeletivo processo)
    {
        if (processo.ConfiguracaoTaxaInscricao is not { } taxa)
        {
            return new JsonObject { ["presente"] = false };
        }

        return new JsonObject
        {
            ["presente"] = true,
            ["cobra"] = taxa.Cobra,
            ["valor"] = taxa.Valor is { } valor ? HashCanonicalComputer.SerializeDecimalCanonical(valor, ConfiguracaoTaxaInscricao.ValorEscala) : null,
            ["fundamentos"] = new JsonArray([.. taxa.Fundamentos.Select(static f => (JsonNode)f.ToCodigo())]),
        };
    }
}
