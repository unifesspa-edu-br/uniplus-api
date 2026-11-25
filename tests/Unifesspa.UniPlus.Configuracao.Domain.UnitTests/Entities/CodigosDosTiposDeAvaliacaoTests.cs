namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CodigosDosTiposDeAvaliacaoTests
{
    [Theory]
    [InlineData("A")]
    [InlineData("PROVA_2027")]
    public void Criar_CodigoValido_PreservaValor(string codigo)
    {
        TipoEtapa.Criar(codigo, "Nome", null, true, true, false).Value!.Codigo.Should().Be(codigo);
        TipoConceito.Criar(codigo, DefinicoesDeTipoConceito.Numerica()).Value!.Codigo.Should().Be(codigo);
    }

    [Theory]
    [InlineData(" PROVA")]
    [InlineData("PROVA ")]
    [InlineData("prova")]
    [InlineData("PRÓVA")]
    [InlineData("TIPO__NOVO")]
    public void Criar_CodigoInvalido_RecusaSemExporValor(string codigo)
    {
        Result<TipoEtapa> etapa = TipoEtapa.Criar(codigo, "Nome", null, true, true, false);
        Result<TipoConceito> conceito = TipoConceito.Criar(codigo, DefinicoesDeTipoConceito.Numerica());
        etapa.Error!.Code.Should().Be(TipoEtapaErrorCodes.CodigoFormatoInvalido);
        conceito.Error!.Code.Should().Be(TipoConceitoErrorCodes.CodigoFormatoInvalido);
        etapa.Error.Message.Should().NotContain(codigo);
        conceito.Error.Message.Should().NotContain(codigo);
    }

    [Fact]
    public void Criar_CodigoNoLimite_AceitaERecusaAcima()
    {
        TipoEtapa.ValidarCodigo(new string('A', 80)).IsSuccess.Should().BeTrue();
        TipoEtapa.ValidarCodigo(new string('A', 81)).Error!.Code.Should().Be(TipoEtapaErrorCodes.CodigoFormatoInvalido);
        TipoConceito.Criar(new string('A', 80), DefinicoesDeTipoConceito.Numerica()).IsSuccess.Should().BeTrue();
        TipoConceito.Criar(new string('A', 81), DefinicoesDeTipoConceito.Numerica()).Error!.Code
            .Should().Be(TipoConceitoErrorCodes.CodigoFormatoInvalido);
    }

    [Fact]
    public void Criar_CodigoENomeInvalidos_AcumulaNaOrdem()
    {
        Result<TipoEtapa> etapa = TipoEtapa.Criar("invalido", "", null, true, true, false);
        Result<TipoConceito> conceito = TipoConceito.Criar("invalido", DefinicoesDeTipoConceito.Numerica() with { Nome = "" });
        etapa.Errors.Select(erro => erro.Error.Code).Should().Equal(TipoEtapaErrorCodes.CodigoFormatoInvalido, TipoEtapaErrorCodes.NomeObrigatorio);
        conceito.Errors.Select(erro => erro.Error.Code).Should().Equal(TipoConceitoErrorCodes.CodigoFormatoInvalido, TipoConceitoErrorCodes.NomeObrigatorio);
    }
}
