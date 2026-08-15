namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Unidade de um prazo do recurso — interposição ou suspensividade
/// (<see cref="ValueObjects.ArgsRegraPrazoRecurso"/>, Story #851 §3.6).
/// </summary>
/// <remarks>
/// <para>
/// O enum é o vocabulário completo, e nem todo valor é declarável nos dois prazos.
/// </para>
/// <para>
/// No <b>prazo de interposição</b>, só <see cref="DiasUteis"/> em valor inteiro e
/// <see cref="Horas"/> (UNI-REQ-0113): é o prazo que fecha a porta do candidato, e tempo
/// que passa quando ele não tem como agir não pode consumir a janela.
/// <see cref="Dias"/> é recusado — encolheria a janela sempre que calhasse de cair em
/// feriado — e fração de dia útil também, por não ter leitura unívoca.
/// <see cref="Entities.RegraRecursoFase.Criar"/> recusa em runtime com erro nomeado
/// (<c>RegraRecursoFase.PrazoEmDiasCorridos</c> /
/// <c>RegraRecursoFase.PrazoEmFracaoDeDiaUtil</c>), nunca aproximando em silêncio.
/// </para>
/// <para>
/// Na <b>suspensividade</b>, as três unidades são declaráveis, <see cref="Dias"/> incluído
/// (UNI-REQ-0080, que congela o par valor-unidade, e UNI-REQ-0113, cuja recusa alcança só a
/// interposição) — é outro relógio, com outra regra. O que a contagem em dia útil exige,
/// ali e na interposição, é a convenção de contagem declarada pelo processo (UNI-REQ-0112),
/// que só é condição de publicar quando alguma contagem do certame distingue dia útil
/// (UNI-REQ-0116) — invariante da raiz, não desta unidade.
/// </para>
/// </remarks>
public enum UnidadePrazo
{
    Nenhuma = 0,
    Horas = 1,
    Dias = 2,
    DiasUteis = 3,
}
