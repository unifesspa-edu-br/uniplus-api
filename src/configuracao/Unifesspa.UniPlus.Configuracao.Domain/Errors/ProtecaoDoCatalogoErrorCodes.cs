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

    /// <summary>Um item do lote foi alterado concorrentemente entre a leitura e a gravação.</summary>
    public const string ConflitoDeConcorrencia = "ProtecaoDoCatalogo.ConflitoDeConcorrencia";

    /// <summary>
    /// A proteção que o aprovador conferiu ausente, ou com classificação ou hipótese legal fora do vocabulário: a
    /// decisão não chega a ser comparada com a proteção vigente.
    /// </summary>
    public const string ProtecaoConferidaInvalida = "ProtecaoDoCatalogo.ProtecaoConferidaInvalida";

    /// <summary>
    /// A proteção vigente do item difere da que o aprovador conferiu: ele foi editado depois de visto. Reler o item e
    /// decidir de novo resolve; repetir a mesma decisão nunca dá certo.
    /// </summary>
    public const string ProtecaoMudou = "ProtecaoDoCatalogo.ProtecaoMudou";

    /// <summary>A classificação recomendada na negativa é a que o item já tem: não há o que corrigir, o item se aprova.</summary>
    public const string RecomendacaoIgualAAtual = "ProtecaoDoCatalogo.RecomendacaoIgualAAtual";

    /// <summary>O filtro de situação da consulta fora do vocabulário.</summary>
    public const string SituacaoInvalida = "ProtecaoDoCatalogo.SituacaoInvalida";

    /// <summary>O cadastro do item da decisão (<c>TIPO_DOCUMENTO</c> ou <c>FATO</c>) ausente ou fora do vocabulário.</summary>
    public const string CadastroInvalido = "ProtecaoDoCatalogo.CadastroInvalido";

    /// <summary>A decisão (<c>APROVADA</c> ou <c>NEGADA</c>) ausente ou fora do vocabulário.</summary>
    public const string DecisaoInvalida = "ProtecaoDoCatalogo.DecisaoInvalida";
}
