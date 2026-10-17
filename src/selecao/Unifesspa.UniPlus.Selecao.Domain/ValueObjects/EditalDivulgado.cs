namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O edital de uma versão do certame, divulgado no acervo público pelo ato que criou a versão.
/// </summary>
/// <remarks>
/// O endereço fica registrado no instante da divulgação porque a chave no acervo depende do ano e
/// do tipo do ato, que só chegam com o registro do ato e que Seleção não guarda: depois, não há
/// como recalculá-la.
/// </remarks>
/// <param name="AtoId">O ato normativo que criou a versão e, por isso, divulgou o edital.</param>
/// <param name="NumeroVersao">A versão de configuração a que o edital pertence.</param>
/// <param name="UrlDownload">Endereço do arquivo no acervo público, o mesmo que o documento divulgado informa.</param>
public sealed record EditalDivulgado(Guid AtoId, int NumeroVersao, string UrlDownload);
