namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class CombinacaoDoGrupoTests
{
    [Fact]
    public void Declarar_GrupoDeTodosOsItensComQuantidadeMinima_ExplicaComoCorrigir()
    {
        Result<CombinacaoDoGrupo> resultado = CombinacaoDoGrupo.Declarar(FormaDeComprovacao.TodosOsItens, 1, null, 1, false);

        resultado.Error!.Message.Should().Be("Este grupo exige todos os itens, mas informa uma quantidade mínima. Retire a quantidade mínima.");
    }

    [Theory]
    [InlineData(FormaDeComprovacao.TodosOsItens, 0, "INVALIDA", true, "RegraDeComprovacao.QuantidadeMinimaProibidaQuandoExigeTodos")]
    [InlineData(FormaDeComprovacao.TodosOsItens, null, "INVALIDA", true, "RegraDeComprovacao.ConsequenciaProibidaQuandoExigeTodos")]
    [InlineData(FormaDeComprovacao.QuantidadeMinimaDeItens, 0, "INVALIDA", true, "RegraDeComprovacao.QuantidadeMinimaDeItensForaDosLimites")]
    [InlineData(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, "INVALIDA", true, "RegraDeComprovacao.ConsequenciaInvalida")]
    public void Declarar_DefeitosSimultaneos_ConservaPrimeiraRecusa(FormaDeComprovacao tipo, int? quantidade, string consequencia, bool temBase, string codigo)
    {
        CombinacaoDoGrupo.Declarar(tipo, quantidade, consequencia, 1, temBase).Error!.Code.Should().Be(codigo);
    }

    [Fact]
    public void Declarar_GrupoDeQuantidadeMinimaSemCardinalidade_AplicaPadrao()
    {
        CombinacaoDoGrupo combinacao = CombinacaoDoGrupo.Declarar(FormaDeComprovacao.QuantidadeMinimaDeItens, null, "ELIMINA", 1, true).Value!;

        combinacao.Forma.Should().Be(FormaDeComprovacao.QuantidadeMinimaDeItens);
        combinacao.QuantidadeMinima.Should().Be(1);
        combinacao.Consequencia.Should().Be("ELIMINA");
    }
    [Theory]
    [InlineData(" ELIMINA ")]
    [InlineData("elimina")]
    public void Declarar_ConsequenciaNaoExata_Recusa(string consequencia)
    {
        Result<CombinacaoDoGrupo> resultado = CombinacaoDoGrupo.Declarar(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, consequencia, 1, false);

        resultado.Error!.Code.Should().Be("RegraDeComprovacao.ConsequenciaInvalida");
        resultado.Error.Message.Should().NotContain(consequencia);
    }

}
