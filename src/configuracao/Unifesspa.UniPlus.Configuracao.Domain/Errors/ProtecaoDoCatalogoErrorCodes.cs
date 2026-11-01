namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

/// <summary>
/// Recusas do circuito de aprovação da proteção de dados do catálogo (ADR-0153), comuns ao tipo de
/// documento e ao fato.
/// </summary>
public static class ProtecaoDoCatalogoErrorCodes
{
    /// <summary>Solicitação enviada quando nenhum item do catálogo está pendente.</summary>
    public const string SemItemPendente = "ProtecaoDoCatalogo.SemItemPendente";

    /// <summary>Decisão da proteção de dados sobre item que não está em solicitação.</summary>
    public const string ItemForaDeSolicitacao = "ProtecaoDoCatalogo.ItemForaDeSolicitacao";

    /// <summary>Negativa sem a classificação recomendada, ou com uma fora do vocabulário.</summary>
    public const string RecomendacaoObrigatoria = "ProtecaoDoCatalogo.RecomendacaoObrigatoria";

    /// <summary>Aceite da recomendação num item que a proteção de dados não negou.</summary>
    public const string SemRecomendacaoParaAceitar = "ProtecaoDoCatalogo.SemRecomendacaoParaAceitar";

    /// <summary>
    /// A hipótese legal do item não cabe na classificação recomendada (art. 7º ou art. 11 da LGPD):
    /// o item precisa ser editado e solicitado de novo.
    /// </summary>
    public const string RecomendacaoIncompativelComAHipoteseLegal = "ProtecaoDoCatalogo.RecomendacaoIncompativelComAHipoteseLegal";

    /// <summary>Solicitação enviada sem que ninguém tenha a permissão de aprovar para recebê-la.</summary>
    public const string SemQuemAprove = "ProtecaoDoCatalogo.SemQuemAprove";

    /// <summary>Quem age não tem permissão para a operação sobre nenhum dos cadastros que ela alcança.</summary>
    public const string SemPermissaoNoCatalogo = "ProtecaoDoCatalogo.SemPermissaoNoCatalogo";

    /// <summary>O filtro de situação da consulta fora do vocabulário.</summary>
    public const string SituacaoInvalida = "ProtecaoDoCatalogo.SituacaoInvalida";
}
