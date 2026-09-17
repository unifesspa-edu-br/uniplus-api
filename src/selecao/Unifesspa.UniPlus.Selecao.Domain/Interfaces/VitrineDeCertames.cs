namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Situação da janela de inscrição de um certame, resolvida contra o instante da consulta.
/// </summary>
/// <remarks>
/// <para>
/// Os quatro valores PARTICIONAM o conjunto divulgado: todo certame cai em exatamente um, sempre. É
/// o que torna os contadores somáveis, o que faz cada número exibido ter um filtro que o serve — um
/// contador cujo recorte não existisse como filtro seria um rótulo em que não se pode clicar — e o
/// que permite marcar cada item com a mesma palavra pela qual ele é filtrado e contado.
/// </para>
/// <para>
/// Não há valor para "sem filtro": ausência de filtro é ausência do parâmetro. Um valor "todas"
/// seria situação que nenhum certame tem, e bastaria escrevê-lo num item para a marca deixar de
/// querer dizer o mesmo que o recorte.
/// </para>
/// <para>
/// A janela tem DOIS lados, e a partição respeita os dois. Um edital publicado antes de a inscrição
/// abrir é o caso normal, não a exceção: o ato sai primeiro. Definir "inscrições abertas" apenas
/// pelo fim da janela o classificaria como aberto, e o candidato tomaria uma recusa ao tentar se
/// inscrever num certame que a própria vitrine anunciou como disponível.
/// </para>
/// </remarks>
public enum SituacaoDoCertame
{
    /// <summary>Divulgado, com a janela de inscrição ainda por abrir.</summary>
    EmBreve = 1,

    /// <summary>Recebe inscrição, e o encerramento ainda está além do limiar final.</summary>
    InscricoesAbertas = 2,

    /// <summary>Recebe inscrição, e o encerramento já está dentro do limiar final.</summary>
    UltimosDias = 3,

    /// <summary>A janela já encerrou.</summary>
    Encerradas = 4,
}

/// <summary>
/// A regra que classifica um certame divulgado numa das quatro situações.
/// </summary>
/// <remarks>
/// <para>
/// Existe para ser o <b>único enunciado</b> da partição em código que roda. A consulta precisa
/// repeti-la como predicado traduzível para SQL — não há como o banco chamar este método —, e é
/// justamente por isso que ela precisa de um lugar canônico contra o qual o recorte do banco possa
/// ser conferido: duas expressões da mesma regra divergem em silêncio, e o sintoma seria a marca do
/// item discordar do grupo em que ele foi listado.
/// </para>
/// </remarks>
public static class SituacaoDaVitrine
{
    /// <summary>
    /// Em qual situação está um certame cuja janela vai de <paramref name="inscricoesDe"/> a
    /// <paramref name="inscricoesAte"/>, no instante dado.
    /// </summary>
    /// <remarks>
    /// A ordem dos testes é a da linha do tempo, e é ela que garante a partição mesmo diante de
    /// janela invertida: encerrado primeiro, depois o que ainda não abriu, e só então a distinção
    /// entre quem tem prazo folgado e quem está no limiar.
    /// </remarks>
    public static SituacaoDoCertame Classificar(
        DateTimeOffset inscricoesDe,
        DateTimeOffset inscricoesAte,
        DateTimeOffset instante,
        TimeSpan limiarDosUltimosDias)
    {
        if (inscricoesAte < instante)
        {
            return SituacaoDoCertame.Encerradas;
        }

        if (inscricoesDe > instante)
        {
            return SituacaoDoCertame.EmBreve;
        }

        return inscricoesAte < instante + limiarDosUltimosDias
            ? SituacaoDoCertame.UltimosDias
            : SituacaoDoCertame.InscricoesAbertas;
    }
}

/// <summary>
/// Quantos certames divulgados há em cada situação, no instante da consulta.
/// </summary>
/// <remarks>
/// <para>
/// Agregação sobre o conjunto divulgado <b>sem</b> o recorte de situação: são estes números que
/// alimentam o próprio filtro de situação, e aplicá-lo a eles deixaria todos zerados menos um.
/// </para>
/// <para>
/// Contam exatamente o que a vitrine lista — só há linha para certame divulgado —, e como as quatro
/// situações particionam esse conjunto, os quatro números somam o total.
/// </para>
/// </remarks>
/// <param name="EmBreve">Divulgados com a janela ainda por abrir.</param>
/// <param name="InscricoesAbertas">Recebem inscrição, sem estar no limiar final.</param>
/// <param name="UltimosDias">Recebem inscrição, e encerram dentro do limiar final.</param>
/// <param name="Encerrados">Já encerraram.</param>
public readonly record struct ContadoresDaVitrine(
    int EmBreve,
    int InscricoesAbertas,
    int UltimosDias,
    int Encerrados);
