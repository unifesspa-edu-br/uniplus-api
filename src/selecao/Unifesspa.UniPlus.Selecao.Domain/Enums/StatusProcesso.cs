namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Ciclo de vida do <see cref="Entities.ProcessoSeletivo"/> (agregado-raiz).
/// Em #758 todo processo nasce e permanece em <see cref="Rascunho"/> — as
/// transições (publicação, encerramento, cancelamento) são orquestradas pela
/// Story #759, que introduz o ato de publicar com snapshot de congelamento dos parâmetros na publicação (UNI-REQ-0019).
/// </summary>
public enum StatusProcesso
{
    [CodigoDeContrato("RASCUNHO", "Rascunho", Descricao = "O processo está em preparação.")]
    Rascunho = 1,
    [CodigoDeContrato("PUBLICADO", "Publicado", Descricao = "O processo foi publicado.")]
    Publicado = 2,
    [CodigoDeContrato("ENCERRADO", "Encerrado", Descricao = "O processo foi encerrado.")]
    Encerrado = 3,
    [CodigoDeContrato("CANCELADO", "Cancelado", Descricao = "O processo foi cancelado.")]
    Cancelado = 4
}
