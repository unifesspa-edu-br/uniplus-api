namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Projeção <c>DocumentoAPublicar</c> → <c>DocumentoAPublicarDto</c>. Ponto único de
/// travessia entre a entidade e o contrato: os endereços de storage da entidade
/// não têm para onde ir no DTO, então nenhum caminho de leitura pode vazá-los
/// por descuido de projeção manual.
/// </summary>
public static class DocumentoAPublicarMapping
{
    public static DocumentoAPublicarDto ToDto(DocumentoAPublicar documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        return new DocumentoAPublicarDto(
            Id: documento.Id,
            ProcessoSeletivoId: documento.ProcessoSeletivoId,
            Finalidade: CodigoComRotulo<FinalidadeDoDocumento>.De(documento.Finalidade),
            NomeArquivo: documento.NomeArquivo,
            TipoDeMidia: documento.TipoDeMidia,
            Status: CodigoComRotulo<StatusArquivoEnviado>.De(documento.Status),
            CriadoEm: documento.CreatedAt,
            ExpiraEm: documento.ExpiraEm,
            TamanhoBytes: documento.TamanhoBytes,
            HashSha256: documento.HashSha256,
            ConfirmadoEm: documento.ConfirmadoEm);
    }
}
