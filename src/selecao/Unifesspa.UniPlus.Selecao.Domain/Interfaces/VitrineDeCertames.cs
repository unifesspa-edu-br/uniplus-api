namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Filtro por situação da janela de inscrição, resolvido contra o instante da consulta.
/// </summary>
/// <remarks>
/// Os três valores concretos PARTICIONAM o conjunto publicado: cada certame cai em exatamente um.
/// É o que torna os contadores somáveis e o que faz cada número exibido ter um filtro que o serve —
/// um contador cujo recorte não existisse como filtro seria um rótulo em que não se pode clicar.
/// </remarks>
public enum SituacaoDoCertame
{
    /// <summary>Sem filtro: publicado aparece, encerrado ou não.</summary>
    Todas = 0,

    /// <summary>Recebem inscrição e ainda não entraram no limiar final.</summary>
    InscricoesAbertas = 1,

    /// <summary>Apenas os que já encerraram.</summary>
    Encerradas = 2,

    /// <summary>Recebem inscrição e encerram dentro do limiar final.</summary>
    UltimosDias = 3,
}

/// <summary>
/// Quantos certames publicados há em cada situação, no instante da consulta.
/// </summary>
/// <remarks>
/// <para>
/// Agregação sobre o conjunto filtrado <b>exceto</b> pela própria situação: são estes números que
/// alimentam o filtro de situação, e filtrá-los por ele deixaria todos zerados menos um.
/// </para>
/// <para>
/// <b>Contam o que está publicado, não o que está visível.</b> A visibilidade exige ato normativo
/// registrado, que vive noutro módulo e nenhuma agregação SQL daqui pode afirmar. A diferença
/// alcança apenas o certame cuja publicação de abertura teve o registro do ato ainda não drenado ou
/// recusado — estado raro, e que alguém reconcilia. Conferi-lo exigiria trazer a linhagem de todos
/// os certames para contar três números.
/// </para>
/// </remarks>
/// <param name="InscricoesAbertas">Ainda recebem inscrição, sem estar no limiar final.</param>
/// <param name="UltimosDias">Ainda recebem inscrição, e encerram dentro do limiar final.</param>
/// <param name="Encerrados">Já encerraram.</param>
public readonly record struct ContadoresDaVitrine(int InscricoesAbertas, int UltimosDias, int Encerrados);
