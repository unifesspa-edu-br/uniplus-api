namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Exporta o documento guardado com o <c>id</c> do rascunho e os descritivos só de leitura dos cadastros vivos. A unidade
/// administradora é a que o rascunho guarda pelo identificador, e o documento traz o código que ela tem hoje, para que o
/// documento exportado, salvo de volta, nomeie a mesma unidade ainda que o código tenha mudado depois. O descritivo cujo
/// cadastro não resolve é omitido.
/// </summary>
public sealed class ExportacaoDoRascunho(IUnidadeReader unidades, DescricaoDoDocumentoDoRascunho descricao)
{
    private const int PosicaoDoIdNaRaiz = 2;

    public async Task<JsonObject> ExportarAsync(RascunhoDeProcessoSeletivo rascunho, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rascunho);
        JsonObject documento = JsonNode.Parse(rascunho.Documento)!.AsObject();
        documento.Remove("id");
        documento.Insert(Math.Min(PosicaoDoIdNaRaiz, documento.Count), "id", rascunho.Id.ToString());
        OmitirNomesDeMunicipiosAusentes(documento);
        await DescreverUnidadeAdministradoraAsync(documento, rascunho.UnidadeAdministradoraOrigemId, cancellationToken).ConfigureAwait(false);
        await descricao.DescreverAsync(documento, cancellationToken).ConfigureAwait(false);
        return documento;
    }

    private static void OmitirNomesDeMunicipiosAusentes(JsonObject documento)
    {
        if (documento["provaPresencial"]?["municipios"] is not JsonArray municipios)
        {
            return;
        }

        foreach (JsonObject municipio in municipios.OfType<JsonObject>())
        {
            if (municipio["nome"] is null)
            {
                municipio.Remove("nome");
            }
        }
    }

    private async Task DescreverUnidadeAdministradoraAsync(JsonObject documento, Guid unidadeId, CancellationToken cancellationToken)
    {
        if (documento["identificacao"] is not JsonObject identificacao)
        {
            return;
        }

        UnidadeView? unidade = await unidades.ObterPorIdAsync(unidadeId, cancellationToken).ConfigureAwait(false);
        if (unidade is not null)
        {
            identificacao["unidadeAdministradoraCodigo"] = unidade.Codigo;
            identificacao["unidadeAdministradoraNome"] = unidade.Nome;
            identificacao["unidadeAdministradoraSigla"] = unidade.Sigla;
        }
    }
}
