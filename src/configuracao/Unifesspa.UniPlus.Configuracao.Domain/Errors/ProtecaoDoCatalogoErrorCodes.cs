namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

/// <summary>
/// Recusas do circuito de aprovação da proteção de dados do catálogo de tipos de documento e de fatos do candidato
/// (ADR-0153), comuns aos dois cadastros.
/// </summary>
public static class ProtecaoDoCatalogoErrorCodes
{
    /// <summary>Solicitação enviada sem nenhum item selecionado.</summary>
    public const string SolicitacaoSemItem = "ProtecaoDoCatalogo.SolicitacaoSemItem";

    /// <summary>Item da solicitação que não existe ou que não está pendente de solicitação.</summary>
    public const string ItemNaoPendente = "ProtecaoDoCatalogo.ItemNaoPendente";

    /// <summary>Decisão do Representante do controlador sobre item que não está em solicitação.</summary>
    public const string ItemForaDeSolicitacao = "ProtecaoDoCatalogo.ItemForaDeSolicitacao";

    /// <summary>Ajuste recomendado sem a classificação recomendada, ou com uma fora do vocabulário.</summary>
    public const string RecomendacaoObrigatoria = "ProtecaoDoCatalogo.RecomendacaoObrigatoria";

    /// <summary>Aceite da recomendação num item em que o Representante do controlador não recomendou ajuste.</summary>
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

    /// <summary>O token não identifica quem pede por completo: não houve decisão de acesso a tomar.</summary>
    public const string IdentidadeIncompleta = "ProtecaoDoCatalogo.IdentidadeIncompleta";

    /// <summary>Um item do lote foi alterado concorrentemente entre a leitura e a gravação.</summary>
    public const string ConflitoDeConcorrencia = "ProtecaoDoCatalogo.ConflitoDeConcorrencia";

    /// <summary>
    /// A proteção que o Representante do controlador conferiu ausente, ou com classificação ou hipótese legal fora do vocabulário: a
    /// decisão não chega a ser comparada com a proteção vigente.
    /// </summary>
    public const string ProtecaoConferidaInvalida = "ProtecaoDoCatalogo.ProtecaoConferidaInvalida";

    /// <summary>
    /// A proteção vigente do item difere da que o Representante do controlador conferiu: ele foi editado depois de visto. Reler o item e
    /// decidir de novo resolve; repetir a mesma decisão nunca dá certo.
    /// </summary>
    public const string ProtecaoMudou = "ProtecaoDoCatalogo.ProtecaoMudou";

    /// <summary>A classificação recomendada no ajuste é a que o item já tem: não há o que corrigir, o item se aprova.</summary>
    public const string RecomendacaoIgualAAtual = "ProtecaoDoCatalogo.RecomendacaoIgualAAtual";

    /// <summary>O filtro de situação da consulta fora do vocabulário.</summary>
    public const string SituacaoInvalida = "ProtecaoDoCatalogo.SituacaoInvalida";

    /// <summary>O cadastro do item da decisão (<c>TIPO_DOCUMENTO</c> ou <c>FATO</c>) ausente ou fora do vocabulário.</summary>
    public const string CadastroInvalido = "ProtecaoDoCatalogo.CadastroInvalido";

    /// <summary>A decisão (<c>APROVAR</c>, <c>RECOMENDAR_AJUSTE</c> ou <c>REPROVAR</c>) ausente ou fora do vocabulário.</summary>
    public const string DecisaoInvalida = "ProtecaoDoCatalogo.DecisaoInvalida";

    /// <summary>Reprovação sem justificativa (ausente ou em branco).</summary>
    public const string JustificativaObrigatoria = "ProtecaoDoCatalogo.JustificativaObrigatoria";

    /// <summary>Justificativa da decisão acima do limite de caracteres.</summary>
    public const string JustificativaMuitoLonga = "ProtecaoDoCatalogo.JustificativaMuitoLonga";

    /// <summary>Classificação recomendada numa decisão que não a admite (aprovação ou reprovação).</summary>
    public const string DadoDaDecisaoNaoCabe = "ProtecaoDoCatalogo.DadoDaDecisaoNaoCabe";

    /// <summary>
    /// Aprovação ou recomendação de ajuste sem justificativa num item com manifestação técnica desfavorável vigente
    /// (recomenda ajuste ou inadequada) no ciclo.
    /// </summary>
    public const string JustificativaExigidaPelaManifestacao = "ProtecaoDoCatalogo.JustificativaExigidaPelaManifestacao";

    /// <summary>O parecer da manifestação (<c>ADEQUADA</c>, <c>RECOMENDA_AJUSTE</c> ou <c>INADEQUADA</c>) ausente ou fora do vocabulário.</summary>
    public const string ParecerInvalido = "ProtecaoDoCatalogo.ParecerInvalido";

    /// <summary>Manifestação com parecer recomenda ajuste ou inadequada sem o texto.</summary>
    public const string TextoDaManifestacaoObrigatorio = "ProtecaoDoCatalogo.TextoDaManifestacaoObrigatorio";

    /// <summary>Texto da manifestação acima do limite de caracteres.</summary>
    public const string TextoDaManifestacaoMuitoLongo = "ProtecaoDoCatalogo.TextoDaManifestacaoMuitoLongo";

    /// <summary>O ciclo de aprovação lido no item ausente ou menor que 1.</summary>
    public const string CicloInvalido = "ProtecaoDoCatalogo.CicloInvalido";

    /// <summary>
    /// O ciclo de aprovação do item mudou depois de lido: o item foi reiniciado, e a manifestação falaria do que quem
    /// se manifesta não viu. Reler o item resolve; repetir a mesma manifestação nunca dá certo.
    /// </summary>
    public const string CicloMudou = "ProtecaoDoCatalogo.CicloMudou";

    /// <summary>
    /// A decisão sem a contagem de manifestações que o Representante do controlador leu no item, ou com ela negativa.
    /// </summary>
    public const string ManifestacoesNoCicloInvalida = "ProtecaoDoCatalogo.ManifestacoesNoCicloInvalida";

    /// <summary>
    /// Houve manifestação técnica registrada, ou reinício do ciclo, depois da leitura do item pelo Representante do
    /// controlador: a decisão falaria do que ele não viu. Reler o item resolve; repetir a mesma decisão nunca dá certo.
    /// </summary>
    public const string ManifestacaoNaoLida = "ProtecaoDoCatalogo.ManifestacaoNaoLida";

    /// <summary>
    /// A manifestação chegou com o ciclo lido, mas o Representante do controlador já decidiu o item nesse ciclo depois da
    /// leitura: a manifestação falaria de uma solicitação que não está mais aberta. Reler o item resolve.
    /// </summary>
    public const string DecisaoJaTomada = "ProtecaoDoCatalogo.DecisaoJaTomada";

    /// <summary>Aceite da recomendação num item reprovado: a reprovação não recomenda classificação, o item só se edita.</summary>
    public const string AceiteDeItemReprovado = "ProtecaoDoCatalogo.AceiteDeItemReprovado";

    /// <summary>O filtro de decisão da consulta fora do vocabulário (<c>APROVACAO</c>, <c>AJUSTE_RECOMENDADO</c> ou <c>REPROVACAO</c>).</summary>
    public const string AtoInvalido = "ProtecaoDoCatalogo.AtoInvalido";

    /// <summary>O período da consulta com a data inicial depois da final.</summary>
    public const string PeriodoInvalido = "ProtecaoDoCatalogo.PeriodoInvalido";
}
