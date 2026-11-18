namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class CombinacaoDoGrupoTests
{
    [Fact]
    public void Declarar_GrupoEComQuantidadeMinima_ExplicaComoCorrigir()
    {
        Result<CombinacaoDoGrupo> resultado = CombinacaoDoGrupo.Declarar(TipoNo.GrupoE, 1, null, 1, false);

        resultado.Error!.Message.Should().Be("Não informe a quantidade mínima de documentos para um grupo E.");
    }

    [Theory]
    [InlineData(TipoNo.GrupoE, 0, "INVALIDA", true, "NoExigencia.QuantidadeMinimaProibidaEmGrupoE")]
    [InlineData(TipoNo.GrupoE, null, "INVALIDA", true, "NoExigencia.ConsequenciaProibidaEmGrupoE")]
    [InlineData(TipoNo.GrupoOu, 0, "INVALIDA", true, "NoExigencia.QuantidadeMinimaForaDosLimites")]
    [InlineData(TipoNo.GrupoOu, 1, "INVALIDA", true, "NoExigencia.ConsequenciaInvalida")]
    public void Declarar_DefeitosSimultaneos_ConservaPrimeiraRecusa(TipoNo tipo, int? quantidade, string consequencia, bool temBase, string codigo)
    {
        CombinacaoDoGrupo.Declarar(tipo, quantidade, consequencia, 1, temBase).Error!.Code.Should().Be(codigo);
    }

    [Fact]
    public void Declarar_GrupoOuSemCardinalidadeENormalizavel_AplicaPadrao()
    {
        CombinacaoDoGrupo combinacao = CombinacaoDoGrupo.Declarar(TipoNo.GrupoOu, null, " ELIMINA ", 1, true).Value!;

        combinacao.Tipo.Should().Be(TipoNo.GrupoOu);
        combinacao.QuantidadeMinima.Should().Be(1);
        combinacao.Consequencia.Should().Be("ELIMINA");
    }
}
