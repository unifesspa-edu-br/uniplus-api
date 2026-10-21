namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

/// <summary>
/// Corpo de <c>POST .../inscricao/documentos/envios</c>: a exigência da árvore congelada da versão da
/// inscrição, a ocorrência quando ela se repete por ocorrência de um grupo, a apresentação a que o
/// arquivo se junta (o verso depois da frente), o tipo de mídia (<c>application/pdf</c>,
/// <c>image/jpeg</c> ou <c>image/png</c>) e o tamanho que o cliente vai enviar. O nome do arquivo não
/// vai: a API não o guarda.
/// </summary>
public sealed record IniciarEnvioDeDocumentoDaInscricaoRequest(
    Guid ExigenciaId, string? OcorrenciaId, Guid? ApresentacaoId, string? TipoDeMidia, long TamanhoBytes);
