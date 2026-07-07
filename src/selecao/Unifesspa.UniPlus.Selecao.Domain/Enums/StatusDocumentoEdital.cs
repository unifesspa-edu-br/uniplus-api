namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Ciclo de vida do <see cref="Entities.DocumentoEdital"/> (Story #759, T3
/// #784): nasce <see cref="Pendente"/> ao gerar a URL pre-assinada de upload
/// e transita para <see cref="Confirmado"/> quando o conteúdo é validado e
/// hasheado server-side. Não há caminho de volta — um documento confirmado é
/// imutável; um novo envio sempre gera um novo registro.
/// </summary>
public enum StatusDocumentoEdital
{
    Pendente = 0,
    Confirmado = 1
}
