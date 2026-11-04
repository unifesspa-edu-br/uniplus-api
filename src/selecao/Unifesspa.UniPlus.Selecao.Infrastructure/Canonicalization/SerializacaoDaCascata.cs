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
/// Projeta no envelope a cascata de remanejamento entre modalidades.
/// </summary>
internal static class SerializacaoDaCascata
{
    internal static JsonObject SerializarCascataRemanejamento(ProcessoSeletivo processo)
    {
        if (processo.Cascata is not { } cascata)
        {
            return new JsonObject { ["presente"] = false };
        }

        JsonArray ordens = [];
        foreach (string origem in cascata.Destinos.Select(static d => d.ModalidadeOrigemCodigo).Distinct(StringComparer.Ordinal).OrderBy(static o => o, StringComparer.Ordinal))
        {
            JsonArray destinos = [];
            foreach (DestinoRemanejamento destino in cascata.Destinos
                .Where(d => string.Equals(d.ModalidadeOrigemCodigo, origem, StringComparison.Ordinal))
                .OrderBy(static d => d.Ordem))
            {
                destinos.Add(HashCanonicalComputer.NormalizeNfc(destino.ModalidadeDestinoCodigo));
            }

            ordens.Add(new JsonObject
            {
                ["origem"] = HashCanonicalComputer.NormalizeNfc(origem),
                ["destinos"] = destinos,
            });
        }

        return new JsonObject
        {
            ["presente"] = true,
            ["regra"] = EscritaCanonica.SerializarReferenciaRegra(cascata.Regra),
            ["fallbackCodigo"] = HashCanonicalComputer.NormalizeNfc(cascata.FallbackCodigo),
            ["ordens"] = ordens,
        };
    }
}
