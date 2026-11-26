namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.FatoCandidatoDeTeste;

public sealed class CodigosDaDefinicaoDoFatoTests
{
    [Theory]
    [InlineData(TipoDoDado.Opcao)]
    [InlineData(TipoDoDado.SimOuNao)]
    public void Criar_FonteDesconhecida_Recusa(TipoDoDado dominio) =>
        Criar(definicao: Definicao with { Dominio = dominio, FonteValores = CodigoInformado<FonteValoresFato>.Ler("INEXISTENTE") })
            .Errors.Should().ContainSingle().Which.Error.Code.Should().Be(FatoCandidatoErrorCodes.FonteValoresInvalida);

    [Theory]
    [InlineData(TipoDoDado.Texto)]
    [InlineData(TipoDoDado.SimOuNao)]
    public void Criar_FormatoDesconhecido_Recusa(TipoDoDado dominio) =>
        Criar(definicao: Definicao with { Dominio = dominio, FonteValores = null, Formato = CodigoInformado<FormatoTexto>.Ler("INEXISTENTE") })
            .Errors.Should().ContainSingle().Which.Error.Code.Should().Be(FatoCandidatoErrorCodes.FormatoInvalido);

    [Fact]
    public void Criar_DominioFonteEFormatoDesconhecidos_AcumulaNaOrdem()
    {
        Result<FatoCandidato> resultado = Criar(definicao: DefinicaoInvalida(), origem: null);
        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("dominio", FatoCandidatoErrorCodes.DominioInvalido), ("cardinalidade", FatoCandidatoErrorCodes.CardinalidadeInvalida),
            ("fonteValores", FatoCandidatoErrorCodes.FonteValoresInvalida), ("formato", FatoCandidatoErrorCodes.FormatoInvalido),
            ("origem", FatoCandidatoErrorCodes.OrigemObrigatoria), ("pontoResolucao", FatoCandidatoErrorCodes.PontoResolucaoObrigatorio),
            ("escopo", FatoCandidatoErrorCodes.EscopoInvalido), ("finalidadeTratamento", FatoCandidatoErrorCodes.FinalidadeTratamentoObrigatoria));
    }

    private static DefinicaoDoFato DefinicaoInvalida() => Definicao with
        {
            Dominio = CodigoInformado<TipoDoDado>.Ler("INEXISTENTE"),
            Cardinalidade = CodigoInformado<QuantidadeDeValores>.Ler("INEXISTENTE"),
            FonteValores = CodigoInformado<FonteValoresFato>.Ler("INEXISTENTE"),
            Formato = CodigoInformado<FormatoTexto>.Ler("INEXISTENTE"),
            PontoResolucao = "",
            Escopo = CodigoInformado<EscopoFato>.Ler("INEXISTENTE"),
            FinalidadeTratamento = "",
        };

    [Fact]
    public void Criar_DominioInvalidoComValoresConhecidos_NaoConfereCoerencia()
    {
        DefinicaoDoFato definicao = Definicao with { Dominio = CodigoInformado<TipoDoDado>.Ler("INEXISTENTE"), Formato = FormatoTexto.Cpf };
        Criar(definicao: definicao).Errors.Should().ContainSingle().Which.Error.Code.Should().Be(FatoCandidatoErrorCodes.DominioInvalido);
    }
}
