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
    public void Declarar_DefeitosSimultaneos_ApontaPrimeiraRecusaEmError(FormaDeComprovacao tipo, int? quantidade, string consequencia, bool temBase, string codigo)
    {
        CombinacaoDoGrupo.Declarar(tipo, quantidade, consequencia, 1, temBase).Error!.Code.Should().Be(codigo);
    }

    [Theory]
    [InlineData(FormaDeComprovacao.TodosOsItens, 0, "INVALIDA", true,
        "RegraDeComprovacao.QuantidadeMinimaProibidaQuandoExigeTodos,RegraDeComprovacao.ConsequenciaProibidaQuandoExigeTodos,RegraDeComprovacao.BaseLegalProibidaQuandoExigeTodos")]
    [InlineData(FormaDeComprovacao.QuantidadeMinimaDeItens, 0, "INVALIDA", true,
        "RegraDeComprovacao.QuantidadeMinimaDeItensForaDosLimites,RegraDeComprovacao.ConsequenciaInvalida")]
    [InlineData(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null, true, "RegraDeComprovacao.BaseLegalSemConsequencia")]
    public void Declarar_DefeitosSimultaneos_DevolveTodasAsRecusasNaOrdem(FormaDeComprovacao tipo, int? quantidade, string? consequencia, bool temBase, string codigos)
    {
        ArgumentNullException.ThrowIfNull(codigos);
        Result<CombinacaoDoGrupo> resultado = CombinacaoDoGrupo.Declarar(tipo, quantidade, consequencia, 1, temBase);

        resultado.Errors.Select(erro => erro.Error.Code).Should().Equal(codigos.Split(','));
    }

    [Fact]
    public void Declarar_GrupoDeQuantidadeMinimaSemCardinalidade_AplicaPadrao()
    {
        CombinacaoDoGrupo combinacao = CombinacaoDoGrupo.Declarar(FormaDeComprovacao.QuantidadeMinimaDeItens, null, "ELIMINA_DO_PROCESSO", 1, true).Value!;

        combinacao.Forma.Should().Be(FormaDeComprovacao.QuantidadeMinimaDeItens);
        combinacao.QuantidadeMinima.Should().Be(1);
        combinacao.ConsequenciaDoDescumprimento.Should().Be("ELIMINA_DO_PROCESSO");
    }
    [Theory]
    [InlineData(" ELIMINA_DO_PROCESSO ")]
    [InlineData("elimina_do_processo")]
    public void Declarar_ConsequenciaNaoExata_Recusa(string consequencia)
    {
        Result<CombinacaoDoGrupo> resultado = CombinacaoDoGrupo.Declarar(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, consequencia, 1, false);

        resultado.Error!.Code.Should().Be("RegraDeComprovacao.ConsequenciaInvalida");
        resultado.Error.Message.Should().NotContain(consequencia);
    }


    [Theory(DisplayName = "A regra de documento aceita o reenvio ausente e falso e recusa o verdadeiro")]
    [InlineData(null, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void RecusaDoReenvio_RegraDeDocumento(bool? abrePrazoParaReenvio, bool recusada)
    {
        DomainError? recusa = CombinacaoDoGrupo.RecusaDoReenvio(FormaDeComprovacao.Documento, abrePrazoParaReenvio);

        (recusa is not null).Should().Be(recusada);
        recusa?.Code.Should().Be("RegraDeComprovacao.ReenvioProibidoEmRegraDeDocumento");
    }

    [Theory(DisplayName = "O grupo recusa o reenvio ausente com o código de campo obrigatório e aceita verdadeiro e falso")]
    [InlineData(FormaDeComprovacao.TodosOsItens)]
    [InlineData(FormaDeComprovacao.QuantidadeMinimaDeItens)]
    public void RecusaDoReenvio_Grupo(FormaDeComprovacao forma)
    {
        CombinacaoDoGrupo.RecusaDoReenvio(forma, null)!.Code.Should().Be(CodigosDeValidacao.Obrigatorio);
        CombinacaoDoGrupo.RecusaDoReenvio(forma, false).Should().BeNull();
        CombinacaoDoGrupo.RecusaDoReenvio(forma, true).Should().BeNull();
    }
}
