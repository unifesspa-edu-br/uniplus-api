namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Unidade de um prazo do recurso: o de interposição e o da 2ª instância
/// (<see cref="ValueObjects.PrazoDoRecurso"/>).
/// </summary>
/// <remarks>
/// O enum é o vocabulário completo, mas só <see cref="DiasUteis"/> em valor inteiro e <see cref="Horas"/> são
/// declaráveis (UNI-REQ-0113): o prazo fecha a porta de quem recorre, e tempo que passa quando a pessoa não tem como
/// agir não pode consumir o prazo. <see cref="Dias"/> existe para receber a recusa nomeada de dias corridos, que
/// encolheriam o prazo sempre que calhasse de cair em feriado, e fração de dia útil também é recusada, por não ter
/// leitura unívoca (<c>prazo_em_dias_corridos</c> e <c>prazo_em_fracao_de_dia_util</c>).
/// </remarks>
public enum UnidadePrazo
{
    [CodigoDeContrato("HORAS", "Horas")]
    Horas = 1,

    [CodigoDeContrato("DIAS", "Dias corridos")]
    Dias = 2,

    [CodigoDeContrato("DIAS_UTEIS", "Dias úteis")]
    DiasUteis = 3,
}
