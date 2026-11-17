namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Monta o documento que a exportação entrega: o guardado, com o <c>id</c> do rascunho na raiz e os descritivos de
/// identificação que o Uni+ já conhece (unidade administradora e tipo de processo), todos só de leitura. O descritivo cujo
/// cadastro não resolve é omitido. O documento é editado como árvore JSON, nunca como texto.
/// </summary>
public sealed class ExportacaoDoRascunho(IUnidadeReader unidades, ITipoProcessoReader tipos)
{
    private const int PosicaoDoIdNaRaiz = 2;

    public async Task<JsonObject> ExportarAsync(RascunhoDeProcessoSeletivo rascunho, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rascunho);

        JsonObject documento = JsonNode.Parse(rascunho.Documento)!.AsObject();
        documento.Remove("id");
        documento.Insert(Math.Min(PosicaoDoIdNaRaiz, documento.Count), "id", rascunho.Id.ToString());

        if (documento["identificacao"] is JsonObject identificacao)
        {
            await DescreverUnidadeAsync(identificacao, rascunho.UnidadeAdministradoraOrigemId, cancellationToken).ConfigureAwait(false);
            await DescreverTipoAsync(identificacao, cancellationToken).ConfigureAwait(false);
        }

        return documento;
    }

    private async Task DescreverUnidadeAsync(JsonObject identificacao, Guid unidadeId, CancellationToken cancellationToken)
    {
        UnidadeView? unidade = await unidades.ObterPorIdAsync(unidadeId, cancellationToken).ConfigureAwait(false);
        if (unidade is not null)
        {
            identificacao["unidadeAdministradoraNome"] = unidade.Nome;
            identificacao["unidadeAdministradoraSigla"] = unidade.Sigla;
        }
    }

    private async Task DescreverTipoAsync(JsonObject identificacao, CancellationToken cancellationToken)
    {
        if (identificacao["tipoProcessoCodigo"] is not JsonValue valor || !valor.TryGetValue(out string? codigo))
        {
            return;
        }

        TipoProcessoView? tipo = await tipos.ObterAtivoPorCodigoAsync(codigo, cancellationToken).ConfigureAwait(false);
        if (tipo is not null)
        {
            identificacao["tipoProcessoNome"] = tipo.Nome;
        }
    }
}
