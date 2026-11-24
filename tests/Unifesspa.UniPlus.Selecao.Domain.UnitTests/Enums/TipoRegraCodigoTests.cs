namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class TipoRegraCodigoTests
{
    [Fact]
    public void TipoRegra_Valores_CodigosERotulosDeclarados() =>
        Enum.GetValues<TipoRegra>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(Pares);

    private static readonly (TipoRegra Valor, string Codigo, string Rotulo)[] Pares =
    [
                (TipoRegra.RegraCalculo, "REGRA_CALCULO", "Cálculo da nota"),
                (TipoRegra.RegraArredondamento, "REGRA_ARREDONDAMENTO", "Arredondamento da nota"),
                (TipoRegra.RegraEliminacao, "REGRA_ELIMINACAO", "Eliminação"),
                (TipoRegra.RegraBonus, "REGRA_FADA", "Fator de Atenuação das Desigualdades de Acesso (FADA)"),
                (TipoRegra.CriterioDesempate, "CRITERIO_DESEMPATE", "Critério de desempate"),
                (TipoRegra.CriterioRemanejamento, "CRITERIO_REMANEJAMENTO", "Critério de remanejamento"),
                (TipoRegra.RegraOrdemAlocacao, "REGRA_ORDEM_ALOCACAO", "Ordem de alocação"),
                (TipoRegra.RegraElegibilidade, "REGRA_ELEGIBILIDADE", "Elegibilidade"),
                (TipoRegra.RegraDistribuicaoVagas, "REGRA_DISTRIBUICAO_VAGAS", "Distribuição de vagas"),
                (TipoRegra.RegraAjusteDistribuicaoVagas, "REGRA_AJUSTE_DISTRIBUICAO_VAGAS", "Ajuste da distribuição de vagas"),
                (TipoRegra.RegraPrazoRecurso, "REGRA_PRAZO_RECURSO", "Prazo de recurso"),
                (TipoRegra.ConvencaoContagemPrazo, "CONVENCAO_CONTAGEM_PRAZO", "Convenção de contagem do prazo"),
                (TipoRegra.RegraApuracaoAvaliacao, "REGRA_APURACAO_AVALIACAO", "Apuração da avaliação"),
                (TipoRegra.RegraProcedimentoRecurso, "REGRA_PROCEDIMENTO_RECURSO", "Procedimento de recurso"),
    ];
}
