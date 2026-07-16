namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Unidade de um prazo do recurso — interposição ou suspensividade
/// (<see cref="ValueObjects.ArgsRegraPrazoRecurso"/>, Story #851 §3.6).
/// </summary>
/// <remarks>
/// <see cref="DiasUteis"/> <b>fica no enum</b> — nunca excluído do domínio fechado: o
/// vocabulário legal admite dias úteis, e removê-lo do tipo seria mentir sobre o que o
/// edital pode declarar. O que não existe é o <b>calendário</b> capaz de resolver a
/// unidade em runtime — por isso o gate recusa, em runtime, com erro nomeado
/// (<c>RegraRecursoFase.PrazoEmDiasUteisSemCalendario</c> /
/// <c>RegraRecursoFase.SuspensividadeEmDiasUteisSemCalendario</c>), nunca aproximando em
/// silêncio.
/// </remarks>
public enum UnidadePrazo
{
    Nenhuma = 0,
    Horas = 1,
    Dias = 2,
    DiasUteis = 3,
}
