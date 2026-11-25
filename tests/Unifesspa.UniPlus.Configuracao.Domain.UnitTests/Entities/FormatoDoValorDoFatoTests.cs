namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.FatoCandidatoDeTeste;

public sealed class FormatoDoValorDoFatoTests
{
    [Theory]
    [InlineData("A")]
    [InlineData("VALOR_2")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWX")]
    public void AdicionarValorDominio_CodigoValido_GravaComoRecebido(string codigo)
    {
        FatoCandidato fato = Criar().Value!;
        fato.AdicionarValorDominio(codigo, "Descrição", 0, true).IsSuccess.Should().BeTrue();
        fato.ValoresDominioDeclarados.Single().Codigo.Should().Be(codigo);
    }

    [Theory]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXY")]
    [InlineData(" VALOR")]
    [InlineData("VALOR ")]
    [InlineData("codigo_minusculo")]
    [InlineData("VALÓR")]
    [InlineData("VALO\u0301R")]
    [InlineData("VALOR__DOIS")]
    public void AdicionarValorDominio_CodigoForaDoFormato_RecusaSemRepetirValor(string codigo)
    {
        Result resultado = Criar().Value!.AdicionarValorDominio(codigo, "Descrição", 0, true);
        resultado.Error!.Code.Should().Be(FatoValorDominioErrorCodes.CodigoFormatoInvalido);
        resultado.Error.Message.Should().NotContain(codigo);
    }

    [Fact]
    public void AdicionarValorDominio_VariasFalhas_AcumulaNaOrdemDosCampos()
    {
        Result resultado = Criar().Value!.AdicionarValorDominio("codigo_minusculo", null, -1, true, new string('x', 2001));
        resultado.Errors.Select(static erro => erro.Error.Code).Should().Equal(
            FatoValorDominioErrorCodes.CodigoFormatoInvalido, FatoValorDominioErrorCodes.DescricaoObrigatoria,
            FatoValorDominioErrorCodes.OrientacaoTamanho, FatoValorDominioErrorCodes.OrdemInvalida);
    }

    [Fact]
    public void ReordenarValores_CodigoComEspacos_RecusaSemAparar()
    {
        FatoCandidato fato = Criar().Value!;
        fato.AdicionarValorDominio("VALOR", "Descrição", 0, true).IsSuccess.Should().BeTrue();
        Result resultado = fato.ReordenarValores([" VALOR "]);
        resultado.Errors.Select(static erro => erro.Error.Code).Should().Equal(
            FatoValorDominioErrorCodes.OrdemComCodigoDesconhecido, FatoValorDominioErrorCodes.OrdemSemTodosOsValores);
    }

    [Fact]
    public void CorrigirValor_CodigoComEspacos_NaoEncontraValor()
    {
        FatoCandidato fato = Criar().Value!;
        fato.AdicionarValorDominio("VALOR", "Descrição", 0, true).IsSuccess.Should().BeTrue();
        fato.CorrigirValor(" VALOR ", "Outra", null).Error!.Code.Should().Be(FatoValorDominioErrorCodes.NaoEncontrado);
    }
}
