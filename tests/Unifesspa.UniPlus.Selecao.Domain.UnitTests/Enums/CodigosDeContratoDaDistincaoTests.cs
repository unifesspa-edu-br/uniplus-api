namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class CodigosDeContratoDaDistincaoTests
{
    [Fact]
    public void CriterioDeDistincao_CadaValor_TemCodigoERotuloEsperados()
    {
        Enum.GetValues<CriterioDeDistincaoDasApresentacoes>()
            .ToDictionary(static valor => valor, static valor => (valor.Codigo(), valor.Rotulo()))
            .Should().Equal(new Dictionary<CriterioDeDistincaoDasApresentacoes, (string, string)>
            {
                [CriterioDeDistincaoDasApresentacoes.CompetenciaMensal] = ("COMPETENCIA_MENSAL", "Competência mensal"),
                [CriterioDeDistincaoDasApresentacoes.ExercicioAnual] = ("EXERCICIO_ANUAL", "Exercício anual"),
                [CriterioDeDistincaoDasApresentacoes.Ocorrencia] = ("OCORRENCIA", "Ocorrência"),
            });
    }
}
