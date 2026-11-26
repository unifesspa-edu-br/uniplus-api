namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

/// <summary>Os municípios vêm da própria base legal, com código IBGE, nome e UF, sem consultar o Geo.</summary>
public sealed class DescricaoDeBasesLegaisDaFada(IBaseLegalDoFadaReader bases) : IDescricaoDeCadastro
{
    public string Cadastro => CamposDeContextoDoDocumento.BasesLegaisDaFada;

    public async Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken)
    {
        Dictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>> atributos = [];
        foreach (string codigo in AtributosDosCodigos.Textos(codigos))
        {
            BaseLegalDoFadaView? baseLegal = await bases.ObterPorCodigoAsync(codigo, cancellationToken).ConfigureAwait(false);
            if (baseLegal is not null)
            {
                atributos.Add((codigo, null), AtributosDa(baseLegal));
            }
        }

        return atributos;
    }

    private static IReadOnlyList<object?> AtributosDa(BaseLegalDoFadaView baseLegal) =>
        [baseLegal.TipoInstrumento, baseLegal.Identificacao, baseLegal.Descricao, Municipios(baseLegal.Municipios)];

    private static JsonArray Municipios(IReadOnlyList<MunicipioDaBaseLegalDoFadaView> municipios) =>
        new([.. municipios.Select(static municipio => (JsonNode)new JsonObject
        {
            ["codigoIbge"] = municipio.CodigoIbge,
            ["nome"] = municipio.Nome,
            ["uf"] = municipio.Uf,
        })]);
}
