namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O processo publicado de verdade: a unidade dona, a versão que a publicação congelou, a fase que exige os documentos,
/// o fato de membro de grupo acrescentado ao formulário e as exigências gravadas.
/// </summary>
internal sealed record ProcessoPublicado(
    Guid Id, Guid UnidadeId, Guid VersaoId, Guid FaseId, string FatoDoGrupo, IReadOnlyList<DocumentoExigido> Exigidos)
{
    public DocumentoExigido ExigenciaDoTipo(string prefixoDoTipo) =>
        Exigidos.Single(d => d.TipoDocumentoCodigo.StartsWith(prefixoDoTipo, StringComparison.Ordinal));
}
