namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Situação da janela de inscrição de um certame, resolvida contra o instante da consulta.
/// </summary>
/// <remarks>
/// <para>
/// Os quatro valores PARTICIONAM o conjunto divulgado: é o que torna os contadores somáveis, dá a
/// cada número exibido um filtro que o serve, e permite marcar o item com a mesma palavra pela qual
/// ele é filtrado e contado. Não há valor para "sem filtro" — ausência de filtro é ausência do
/// parâmetro.
/// </para>
/// <para>
/// A partição respeita os DOIS lados da janela. Um edital publicado antes de a inscrição abrir é o
/// caso normal, e classificá-lo como aberto faria o candidato tomar recusa ao tentar se inscrever.
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
/// Único enunciado da partição em código que roda. A consulta precisa repeti-la como predicado
/// traduzível para SQL, e é contra este método que o recorte do banco é conferido — duas expressões
/// da mesma regra divergem em silêncio.
/// </remarks>
public static class SituacaoDaVitrine
{
    /// <summary>
    /// Em qual situação está um certame cuja janela vai de <paramref name="inscricoesDe"/> a
    /// <paramref name="inscricoesAte"/>, no instante dado.
    /// </summary>
    /// <remarks>
    /// A ordem dos testes é a da linha do tempo, e é ela que garante a partição mesmo diante de
    /// janela invertida.
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
/// Agregação sobre o conjunto divulgado <b>sem</b> o recorte de situação: são estes números que
/// alimentam o próprio filtro, e aplicá-lo a eles deixaria todos zerados menos um. Como as quatro
/// situações particionam o conjunto, os quatro somam o total.
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
