namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Ciclo de vida de um arquivo enviado pelo administrador ao armazenamento de objetos — o
/// documento do Edital e o modelo de documento de uma exigência: nasce <see cref="Pendente"/> ao
/// gerar a URL pré-assinada de envio e passa a <see cref="Confirmado"/> quando o conteúdo é
/// validado e hasheado no servidor. Não há caminho de volta — o confirmado é imutável; um novo
/// envio sempre gera um novo registro.
/// </summary>
public enum StatusArquivoEnviado
{
    Pendente = 0,
    Confirmado = 1
}
