namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Unidade de um prazo do recurso — interposição ou suspensividade
/// (<see cref="ValueObjects.ArgsRegraPrazoRecurso"/>, Story #851 §3.6).
/// </summary>
/// <remarks>
/// <see cref="DiasUteis"/> <b>fica no enum</b> — nunca excluído do domínio fechado: o
/// vocabulário legal admite dias úteis, e removê-lo do tipo seria mentir sobre o que o
/// edital pode declarar. As duas recusas, porém, NÃO têm o mesmo motivo (UNI-REQ-0113):
/// no prazo de interposição, dias úteis é proibido de forma <b>permanente</b> — é o prazo
/// que fecha a porta do candidato, e um erro de contagem para menos cercearia direito;
/// nunca deixa de ser recusado, mesmo com calendário e algoritmo de contagem disponíveis.
/// Na suspensividade, dias úteis é condicionalmente aceitável (UNI-REQ-0116), mas só
/// quando existirem, ao mesmo tempo, um calendário de dias úteis vigente E uma versão
/// identificável do algoritmo de contagem — nenhum artefato de algoritmo versionado
/// existe hoje (o motor de contagem é UNI-REQ-0081, incremento futuro), então a recusa
/// também vale sempre, por ora. O gate recusa em runtime com erro nomeado
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
