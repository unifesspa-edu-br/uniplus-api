namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Ciclo de vida do <see cref="Entities.ProcessoSeletivo"/> (agregado-raiz).
/// Todo processo nasce em <see cref="Rascunho"/>. A publicação congela os parâmetros
/// do processo (UNI-REQ-0019); encerramento e cancelamento são as demais transições.
/// </summary>
public enum SituacaoDoProcessoSeletivo
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
