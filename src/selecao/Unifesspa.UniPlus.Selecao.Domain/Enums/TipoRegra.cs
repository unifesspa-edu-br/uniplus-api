namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Tipo de uma regra do <c>rol_de_regras</c> — a biblioteca de regras tipadas
/// e versionadas que a configuração do Processo Seletivo referencia
/// (<c>codigo</c>+<c>versao</c>+<c>hash</c>), congelando-a no snapshot de
/// publicação (RN08). "Configurável, não programável": o comportamento
/// (fórmula, distribuição de vagas, bônus, desempate, …) é conteúdo de uma
/// regra versionada, não constante do motor — lei muda → nova versão, não
/// deploy.
/// </summary>
/// <remarks>
/// <para>
/// Os 11 tipos espelham o domínio validado do <c>rol_de_regras</c>
/// (fonte de verdade da modelagem P-A/P-B, validada contra Postgres real).
/// O sentinela <see cref="Nenhuma"/> garante que <c>default(TipoRegra)</c>
/// nunca colida com um tipo real — a factory o rejeita explicitamente.
/// </para>
/// <para>
/// Cada valor mapeia para um código textual canônico snake_case
/// (<c>ToCodigo</c>) — o valor persistido na coluna <c>tipo</c> e o token que
/// entra no hash content-addressable da regra. Renumerar o enum não muda o
/// hash de regras existentes (o hash usa o código textual, não o ordinal).
/// </para>
/// </remarks>
public enum TipoRegra
{
    Nenhuma = 0,

    /// <summary>Fórmula da nota final (ex.: <c>FORMULA-MEDIA-PONDERADA</c>, <c>CLASSIFICACAO-IMPORTADA</c>).</summary>
    RegraCalculo = 1,

    /// <summary>Precisão da nota (ex.: <c>PRECISAO-TRUNCAR</c>, <c>PRECISAO-ARREDONDAR-CIMA</c>).</summary>
    RegraArredondamento = 2,

    /// <summary>Eliminação por cálculo (ex.: <c>ELIM-NOTA-MINIMA-ETAPA</c>, <c>ELIM-CORTE-REDACAO</c>, <c>ELIM-ZERO-EM-AREA</c>).</summary>
    RegraEliminacao = 3,

    /// <summary>Bônus sobre a nota final (ex.: <c>BONUS-MULTIPLICATIVO</c>).</summary>
    RegraBonus = 4,

    /// <summary>Critério de desempate (ex.: <c>DESEMPATE-IDOSO</c>, <c>DESEMPATE-MAIOR-NOTA-ETAPA</c>, <c>DESEMPATE-PREDICADO-FATO</c>).</summary>
    CriterioDesempate = 5,

    /// <summary>Critério de remanejamento entre modalidades (cascata / cruzado PSIQ).</summary>
    CriterioRemanejamento = 6,

    /// <summary>Ordem de alocação 1ª/2ª opção → remanejamento → lista de espera (ex.: <c>ALOCACAO-OPCOES-RN04</c>).</summary>
    RegraOrdemAlocacao = 7,

    /// <summary>Enquadramento em cota (ex.: <c>RENDA-PER-CAPITA-LEI-12711</c>).</summary>
    RegraElegibilidade = 8,

    /// <summary>Cálculo do quadro de vagas reservadas (ex.: <c>DISTRIB-VAGAS-LEI-12711</c>, <c>DISTRIB-VAGAS-INSTITUCIONAL</c>).</summary>
    RegraDistribuicaoVagas = 9,

    /// <summary>Reconciliação do estouro por arredondamento na distribuição (ex.: <c>RECONCILIACAO-VAGAS-ART11-PU</c>).</summary>
    RegraAjusteDistribuicaoVagas = 10,

    /// <summary>Prazo/janela/instâncias do recurso (P-D cronograma).</summary>
    RegraPrazoRecurso = 11,
}
