namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Códigos de erro da avaliação declarada na etapa do processo. Constantes, e não literais repetidos no
/// domínio, na conferência da Application e nos testes: um código escrito duas vezes diverge na primeira
/// correção de grafia.
/// </summary>
public static class EtapaProcessoErrorCodes
{
    /// <summary>Caráter declarado que o tipo de etapa não admite (<c>CaraterDaEtapa.ValidarAdmitido</c>).</summary>
    public const string CaraterNaoAdmitidoPeloTipo = "EtapaProcesso.CaraterNaoAdmitidoPeloTipo";

    public const string NotaMaximaInvalida = "EtapaProcesso.NotaMaximaInvalida";
    public const string PassoDoLancamentoInvalido = "EtapaProcesso.PassoDoLancamentoInvalido";
    public const string NotaMinimaAcimaDaNotaMaxima = "EtapaProcesso.NotaMinimaAcimaDaNotaMaxima";
    public const string NotaMaximaForaDoPasso = "EtapaProcesso.NotaMaximaForaDoPasso";
    public const string AvaliacaoNaEtapaDoEnem = "EtapaProcesso.AvaliacaoNaEtapaDoEnem";
    public const string RegraDeConsolidacaoForaDaFormaHumana = "EtapaProcesso.RegraDeConsolidacaoForaDaFormaHumana";
    public const string RegraDeConsolidacaoInvalida = "EtapaProcesso.RegraDeConsolidacaoInvalida";
    public const string PassoNaFormaObjetiva = "EtapaProcesso.PassoNaFormaObjetiva";
    public const string FormaObjetivaEmTipoQueExigeCorrecaoIndependente = "EtapaProcesso.FormaObjetivaEmTipoQueExigeCorrecaoIndependente";
    public const string CorrecaoIndependenteComMenosDeDoisCorretores = "EtapaProcesso.CorrecaoIndependenteComMenosDeDoisCorretores";
    public const string RegraDeConsolidacaoNaoResolvida = "EtapaProcesso.RegraDeConsolidacaoNaoResolvida";
    public const string FormaDeAvaliacaoDesconhecida = "EtapaProcesso.FormaDeAvaliacaoDesconhecida";
    public const string ItensNaEtapaDoEnem = "EtapaProcesso.ItensNaEtapaDoEnem";
    public const string EscalaSemItens = "EtapaProcesso.EscalaSemItens";
    public const string EscalaNaFormaObjetiva = "EtapaProcesso.EscalaNaFormaObjetiva";
    public const string TipoBancaNaoEncontradoOuInativo = "EtapaProcesso.TipoBancaNaoEncontradoOuInativo";
    public const string PassoComEscalaDeAtendimento = "EtapaProcesso.PassoComEscalaDeAtendimento";

    /// <summary>O tipo de ato que a etapa publica não tem versão vigente no catálogo de Publicações.</summary>
    public const string AtoDoProdutoNaoEncontrado = "ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo";
}
