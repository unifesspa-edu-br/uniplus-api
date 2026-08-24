namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Projeção <c>DocumentoEdital</c> → <c>DocumentoEditalDto</c>. Ponto único de
/// travessia entre a entidade e o contrato: os endereços de storage da entidade
/// não têm para onde ir no DTO, então nenhum caminho de leitura pode vazá-los
/// por descuido de projeção manual.
/// </summary>
public static class DocumentoEditalMapping
{
    public static DocumentoEditalDto ToDto(DocumentoEdital documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        return new DocumentoEditalDto(
            Id: documento.Id,
            ProcessoSeletivoId: documento.ProcessoSeletivoId,
            Status: documento.Status.ToString(),
            CriadoEm: documento.CreatedAt,
            ExpiraEm: documento.ExpiraEm,
            TamanhoBytes: documento.TamanhoBytes,
            HashSha256: documento.HashSha256,
            ConfirmadoEm: documento.ConfirmadoEm);
    }
}
