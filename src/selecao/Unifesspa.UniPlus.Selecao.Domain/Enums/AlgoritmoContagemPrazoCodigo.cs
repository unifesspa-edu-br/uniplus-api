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
    /// Placeholder honesto de pendência para a <c>base_legal</c> das entradas
    /// de contagem (UNI-REQ-0095): o dispositivo exato ainda não foi
    /// confirmado juridicamente, e citação aproximada seria pior que a lacuna.
    /// Quando a confirmação vier, entra por substituição enquanto nenhuma
    /// configuração congelada referenciar a entrada, ou por versão nova a
    /// partir da primeira referência (ADR-0112).
    /// </summary>
    public const string BaseLegalPendente =
        "BASE LEGAL PENDENTE DE CONFIRMAÇÃO JURÍDICA — o dispositivo exato (lei, artigo e parágrafo) "
        + "que sustenta o prazo de interposição e o efeito suspensivo do recurso administrativo ainda "
        + "não foi confirmado (UNI-REQ-0095). A convenção de contagem desta entrada é escolha declarada "
        + "pelo edital; nenhuma citação aproximada substitui este texto.";
}
