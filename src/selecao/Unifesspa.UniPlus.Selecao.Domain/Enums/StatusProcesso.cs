namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Ciclo de vida do <see cref="Entities.ProcessoSeletivo"/> (agregado-raiz).
/// Em #758 todo processo nasce e permanece em <see cref="Rascunho"/> — as
/// transições (publicação, encerramento, cancelamento) são orquestradas pela
/// Story #759, que introduz o ato de publicar com snapshot RN08.
/// </summary>
public enum StatusProcesso
{
    Nenhum = 0,
    Rascunho = 1,
    Publicado = 2,
    Encerrado = 3,
    Cancelado = 4
}
