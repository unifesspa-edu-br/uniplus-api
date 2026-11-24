namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum TipoRegra
{
    [CodigoDeContrato("REGRA_CALCULO", "Cálculo da nota", Descricao = "Cálculo da nota.")]
    RegraCalculo = 1,

    [CodigoDeContrato("REGRA_ARREDONDAMENTO", "Arredondamento da nota", Descricao = "Arredondamento da nota.")]
    RegraArredondamento = 2,

    [CodigoDeContrato("REGRA_ELIMINACAO", "Eliminação", Descricao = "Eliminação.")]
    RegraEliminacao = 3,

    [CodigoDeContrato("REGRA_FADA", "Fator de Atenuação das Desigualdades de Acesso (FADA)", Descricao = "Regra do Fator de Atenuação das Desigualdades de Acesso (FADA).")]
    RegraBonus = 4,

    [CodigoDeContrato("CRITERIO_DESEMPATE", "Critério de desempate", Descricao = "Critério de desempate.")]
    CriterioDesempate = 5,

    [CodigoDeContrato("CRITERIO_REMANEJAMENTO", "Critério de remanejamento", Descricao = "Critério de remanejamento.")]
    CriterioRemanejamento = 6,

    [CodigoDeContrato("REGRA_ORDEM_ALOCACAO", "Ordem de alocação", Descricao = "Ordem de alocação.")]
    RegraOrdemAlocacao = 7,

    [CodigoDeContrato("REGRA_ELEGIBILIDADE", "Elegibilidade", Descricao = "Elegibilidade.")]
    RegraElegibilidade = 8,

    [CodigoDeContrato("REGRA_DISTRIBUICAO_VAGAS", "Distribuição de vagas", Descricao = "Distribuição de vagas.")]
    RegraDistribuicaoVagas = 9,

    [CodigoDeContrato("REGRA_AJUSTE_DISTRIBUICAO_VAGAS", "Ajuste da distribuição de vagas", Descricao = "Ajuste da distribuição de vagas.")]
    RegraAjusteDistribuicaoVagas = 10,

    [CodigoDeContrato("REGRA_PRAZO_RECURSO", "Prazo de recurso", Descricao = "Prazo de recurso.")]
    RegraPrazoRecurso = 11,

    [CodigoDeContrato("CONVENCAO_CONTAGEM_PRAZO", "Convenção de contagem do prazo", Descricao = "Convenção de contagem do prazo.")]
    ConvencaoContagemPrazo = 12,

    [CodigoDeContrato("REGRA_APURACAO_AVALIACAO", "Apuração da avaliação", Descricao = "Apuração da avaliação.")]
    RegraApuracaoAvaliacao = 13,

    [CodigoDeContrato("REGRA_PROCEDIMENTO_RECURSO", "Procedimento de recurso", Descricao = "Procedimento de recurso.")]
    RegraProcedimentoRecurso = 14,
}
