namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using Domain.Entities;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Projeção <c>ModeloDeDocumento</c> → <c>ModeloDeDocumentoDto</c>: ponto único de travessia, sem
/// lugar no DTO para os endereços de storage da entidade.
/// </summary>
public static class ModeloDeDocumentoMapping
{
    public static ModeloDeDocumentoDto ToDto(ModeloDeDocumento modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        return new ModeloDeDocumentoDto(
            Id: modelo.Id,
            ProcessoSeletivoId: modelo.ProcessoSeletivoId,
            NomeArquivo: modelo.NomeArquivo,
            Formato: CodigoComRotulo<FormatoDeDocumento>.De(modelo.Formato),
            Status: CodigoComRotulo<StatusArquivoEnviado>.De(modelo.Status),
            CriadoEm: modelo.CreatedAt,
            ExpiraEm: modelo.ExpiraEm,
            TamanhoBytes: modelo.TamanhoBytes,
            HashSha256: modelo.HashSha256,
            ConfirmadoEm: modelo.ConfirmadoEm);
    }
}
