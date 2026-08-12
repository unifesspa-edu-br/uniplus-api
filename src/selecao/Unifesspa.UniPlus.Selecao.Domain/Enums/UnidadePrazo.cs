namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Unidade de um prazo do recurso — interposição ou suspensividade
/// (<see cref="ValueObjects.ArgsRegraPrazoRecurso"/>, Story #851 §3.6).
/// </summary>
/// <remarks>
/// <see cref="DiasUteis"/> <b>fica no enum</b> — nunca excluído do domínio fechado: o
/// vocabulário legal admite dias úteis, e removê-lo do tipo seria mentir sobre o que o
/// edital pode declarar. O calendário de dias úteis existe (issue #1113,
/// <c>ICalendarioVigenteReader</c>, módulo Configuração) — o que pode faltar é um
/// <b>dataset vigente</b> ou a <b>localidade</b> da unidade administradora do processo
/// para resolver a abrangência municipal. O gate, por isso, saiu do domínio: é o
/// handler (<c>DefinirCronogramaFasesCommandHandler</c>) que confere as duas condições
/// em runtime e recusa com erro nomeado (<c>RegraRecursoFase.PrazoEmDiasUteisSemCalendario</c>
/// / <c>SuspensividadeEmDiasUteisSemCalendario</c> / <c>...SemLocalidade</c>), nunca
/// aproximando em silêncio.
/// </remarks>
public enum UnidadePrazo
{
    Nenhuma = 0,
    Horas = 1,
    Dias = 2,
    DiasUteis = 3,
}
