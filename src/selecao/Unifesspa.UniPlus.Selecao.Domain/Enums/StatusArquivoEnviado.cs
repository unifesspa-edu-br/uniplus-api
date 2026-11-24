namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Ciclo de vida de um arquivo enviado pelo administrador ao armazenamento de objetos — o
/// documento a publicar e o modelo de documento de uma exigência: nasce <see cref="Pendente"/> ao
/// gerar a URL pré-assinada de envio e passa a <see cref="Confirmado"/> quando o conteúdo é
/// validado e hasheado no servidor. Não há caminho de volta — o confirmado é imutável; um novo
/// envio sempre gera um novo registro.
/// </summary>
public enum StatusArquivoEnviado
{
    [CodigoDeContrato("PENDENTE", "Pendente", Descricao = "O envio ainda não foi confirmado.")]
    Pendente = 0,
    [CodigoDeContrato("CONFIRMADO", "Confirmado", Descricao = "O conteúdo do envio foi confirmado.")]
    Confirmado = 1
}
