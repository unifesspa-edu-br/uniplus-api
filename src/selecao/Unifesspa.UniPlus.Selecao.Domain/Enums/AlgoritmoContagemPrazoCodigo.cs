namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Códigos canônicos das entradas de <c>tipo=algoritmo_contagem_prazo</c> do
/// <c>rol_de_regras</c> (#1135): as convenções nomeadas de contagem do prazo
/// de interposição que o UNI-REQ-0112 tornou decláraveis por edital. Cada
/// entrada resolve, do seu jeito, as duas perguntas que distinguem uma
/// convenção da outra — o que "um dia útil" significa quando a âncora não cai
/// à meia-noite, e o que acontece quando a âncora cai em dia não útil.
/// </summary>
/// <remarks>
/// Símbolo, não literal solto: seed, testes e o futuro motor de contagem
/// referenciam estas constantes — nunca o texto espalhado pelo código.
/// </remarks>
public static class AlgoritmoContagemPrazoCodigo
{
    /// <summary>
    /// Exclui o dia civil da âncora e conta dias úteis inteiros; âncora em dia
    /// não útil desloca o início para o primeiro dia útil seguinte.
    /// </summary>
    public const string ExcluiDiaInicial = "CONTAGEM-PRAZO-EXCLUI-DIA-INICIAL";

    /// <summary>
    /// Consome horas situadas em dia útil a partir do instante exato da
    /// âncora; em dia não útil o relógio simplesmente não avança.
    /// </summary>
    public const string HorasUteisDesdeAncora = "CONTAGEM-PRAZO-HORAS-UTEIS-DESDE-ANCORA";

    /// <summary>
    /// Em dias úteis, mantém a hora da âncora e avança datas úteis; âncora em
    /// dia não útil desloca para o próximo dia útil na mesma hora. Em horas,
    /// coincide com <see cref="HorasUteisDesdeAncora"/> — a diferença entre as
    /// duas está só na unidade dias úteis.
    /// </summary>
    public const string AvancaDataUtil = "CONTAGEM-PRAZO-AVANCA-DATA-UTIL";

    /// <summary>
    /// Fundamento das entradas de contagem: a convenção aplicável é a que o edital
    /// declara. Substituiu o placeholder de pendência quando a decisão institucional
    /// sobre o prazo recursal foi registrada (UNI-REQ-0095).
    /// </summary>
    /// <remarks>
    /// O texto não excede o que a decisão sustenta. Ela é orientação institucional
    /// juridicamente orientada — não parecer formal nem jurisprudência consolidada —, e não
    /// converte o dispositivo geral em fonte do prazo do certame: o que prevalece no âmbito
    /// do processo seletivo é a regra específica do edital. Nada aqui dispõe sobre efeito
    /// suspensivo, cuja confirmação continua pendente em requisito próprio.
    /// </remarks>
    public const string BaseLegalDeclaradaPeloEdital =
        "O edital de abertura é o fundamento normativo do prazo de interposição e declara a convenção "
        + "pela qual ele se conta (UNI-REQ-0095): na ausência de norma específica que fixe outro prazo, o "
        + "edital estabelece o seu, e o sistema congela e reproduz o declarado sem julgar a escolha. "
        + "Decisão institucional juridicamente orientada — não é parecer formal nem jurisprudência "
        + "consolidada. Não dispõe sobre efeito suspensivo, cuja confirmação é dependência própria "
        + "(UNI-REQ-0117).";
}
