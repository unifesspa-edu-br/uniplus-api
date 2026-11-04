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
/// Projeta no envelope os dados do edital: hashes e anexos, localidade, contagem de prazo, calendário de dias úteis,
/// identidade da unidade, tipos de ato e a apresentação dos pressupostos.
/// </summary>
internal static class SerializacaoDoEdital
{
    internal static JsonArray SerializarApresentacaoDosPressupostos(IReadOnlyDictionary<string, ApresentacaoDoFato>? apresentacao) =>
        new([.. (apresentacao ?? new Dictionary<string, ApresentacaoDoFato>(StringComparer.Ordinal)).Values
            .OrderBy(static a => a.FatoCodigo, StringComparer.Ordinal)
            .Select(static a => (JsonNode?)new JsonObject
            {
                ["fatoCodigo"] = a.FatoCodigo,
                ["rotulo"] = HashCanonicalComputer.NormalizeNfc(a.Rotulo),
                ["tipoRenderizacao"] = a.TipoRenderizacao,
            })]);

    internal static JsonArray SerializarTiposDeAto(IReadOnlyDictionary<string, TipoDeAtoCongelado>? tipos) =>
        new([.. (tipos ?? new Dictionary<string, TipoDeAtoCongelado>(StringComparer.Ordinal)).Values
            .OrderBy(static t => HashCanonicalComputer.NormalizeNfc(t.Codigo), StringComparer.Ordinal)
            .Select(static t => (JsonNode?)new JsonObject
            {
                ["codigo"] = HashCanonicalComputer.NormalizeNfc(t.Codigo),
                ["unicoPorObjeto"] = t.UnicoPorObjeto,
                ["ehResultado"] = t.EhResultado,
                ["congelaConfiguracao"] = t.CongelaConfiguracao,
            })]);

    internal static JsonObject SerializarLocalidade(ProcessoSeletivo processo, string fusoHorario) =>
        new()
        {
            ["codigoIbge"] = processo.Localidade.CodigoIbge,
            ["nome"] = HashCanonicalComputer.NormalizeNfc(processo.Localidade.Nome),
            ["uf"] = processo.Localidade.Uf,
            ["fusoHorario"] = fusoHorario,
        };

    internal static JsonObject SerializarAlgoritmoContagemPrazo(ProcessoSeletivo processo)
    {
        if (processo.AlgoritmoContagemPrazo is not { } algoritmo)
        {
            return new JsonObject { ["presente"] = false };
        }

        return new JsonObject
        {
            ["presente"] = true,
            ["codigo"] = algoritmo.Codigo,
            ["versao"] = algoritmo.Versao,
            ["hash"] = algoritmo.Hash,
        };
    }

    internal static JsonObject SerializarCalendarioDiasUteis(CalendarioDiasUteisCongelado? calendario)
    {
        if (calendario is null)
        {
            return new JsonObject { ["presente"] = false };
        }

        return new JsonObject
        {
            ["presente"] = true,
            ["origemId"] = calendario.OrigemId,
            ["versaoDataset"] = HashCanonicalComputer.NormalizeNfc(calendario.VersaoDataset),
            ["diasNaoUteis"] = new JsonArray(
            [
                .. CalendarioDiasUteisCongelado.Ordenar(calendario.DiasNaoUteis).Select(static dia => (JsonNode)new JsonObject
                {
                    ["data"] = dia.Data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["abrangencia"] = dia.Abrangencia,
                    ["municipioIbge"] = dia.MunicipioIbge,
                    ["municipioNome"] = dia.MunicipioNome is { } nome ? HashCanonicalComputer.NormalizeNfc(nome) : null,
                    ["uf"] = dia.Uf,
                }),
            ]),
        };
    }

    internal static JsonObject SerializarIdentidadesUnidade(ProcessoSeletivo processo) => new()
    {
        ["administradora"] = new JsonObject
        {
            ["origemId"] = processo.UnidadeAdministradoraOrigemId,
            ["sigla"] = HashCanonicalComputer.NormalizeNfc(processo.UnidadeAdministradora.Sigla),
            ["slug"] = HashCanonicalComputer.NormalizeNfc(processo.UnidadeAdministradora.Slug),
            ["nome"] = HashCanonicalComputer.NormalizeNfc(processo.UnidadeAdministradora.Nome),
            ["tipo"] = HashCanonicalComputer.NormalizeNfc(processo.UnidadeAdministradora.Tipo),
            ["cidadeCodigoIbge"] = processo.UnidadeAdministradora.CidadeCodigoIbge is { } codigoIbge
                ? HashCanonicalComputer.NormalizeNfc(codigoIbge) : null,
            ["cidadeNome"] = processo.UnidadeAdministradora.CidadeNome is { } cidadeNome
                ? HashCanonicalComputer.NormalizeNfc(cidadeNome) : null,
            ["cidadeUf"] = processo.UnidadeAdministradora.CidadeUf is { } cidadeUf
                ? HashCanonicalComputer.NormalizeNfc(cidadeUf) : null,
        },
    };

    internal static JsonObject SerializarHashesEdital(DadosEdital dados, string hashEdital) => new()
    {
        ["documentoEditalId"] = dados.DocumentoEditalId,
        ["hashSha256"] = hashEdital,
        ["anexos"] = new JsonArray([.. dados.Anexos.Select(SerializarAnexo)]),
    };

    private static JsonObject SerializarAnexo(AnexoDoEdital anexo) => new()
    {
        ["anexoId"] = anexo.Id,
        ["documentoId"] = anexo.DocumentoId,
        ["nomeArquivo"] = HashCanonicalComputer.NormalizeNfc(anexo.NomeArquivo),
        ["tipoDeMidia"] = anexo.TipoDeMidia,
        ["tamanhoBytes"] = anexo.TamanhoBytes,
        ["hashSha256"] = anexo.HashSha256,
    };
}
