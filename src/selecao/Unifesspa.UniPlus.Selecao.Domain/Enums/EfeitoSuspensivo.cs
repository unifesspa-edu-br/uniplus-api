namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// O efeito suspensivo que o edital escolhe para o recurso da 1ª instância. A 2ª instância não tem escolha: sempre tem
/// efeito suspensivo.
/// </summary>
public enum EfeitoSuspensivo
{
    [CodigoDeContrato(
        "PARA_TODOS_OS_RECURSOS",
        "Efeito suspensivo para todos os recursos",
        Descricao = "Todo recurso da 1ª instância tem efeito suspensivo. Enquanto o prazo para recorrer está aberto e, havendo recurso, até que todos tenham decisão e o resultado definitivo esteja publicado, ficam suspensos os atos que não podem ser desfeitos e o início das fases posteriores.")]
    ParaTodosOsRecursos = 1,

    [CodigoDeContrato(
        "SEM_EFEITO_SUSPENSIVO",
        "Sem efeito suspensivo",
        Descricao = "O recurso da 1ª instância não suspende nenhum ato do processo enquanto é analisado.")]
    SemEfeitoSuspensivo = 2,

    [CodigoDeContrato(
        "UNIDADE_DECIDE_EM_CADA_RECURSO",
        "A unidade responsável pela fase de origem do ato recorrido decide em cada recurso",
        Descricao = "Em cada recurso da 1ª instância, essa unidade decide se concede o efeito suspensivo e registra a fundamentação. Só o recurso com a concessão suspende os atos que não podem ser desfeitos e o início das fases posteriores.")]
    UnidadeDecideEmCadaRecurso = 3,
}
