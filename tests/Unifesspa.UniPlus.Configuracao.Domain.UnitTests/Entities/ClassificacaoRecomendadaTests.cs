namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A regra da classificação recomendada no ajuste que não depende do item: obrigatória e da escala. É uma só, chamada
/// por quem confere a forma da decisão e pela transição do circuito.
/// </summary>
public sealed class ClassificacaoRecomendadaTests
{
    [Theory]
    [InlineData(null, ProtecaoDoCatalogoErrorCodes.RecomendacaoObrigatoria)]
    [InlineData("", ProtecaoDoCatalogoErrorCodes.RecomendacaoObrigatoria)]
    [InlineData("INEXISTENTE", ProtecaoDoCatalogoErrorCodes.RecomendacaoInvalida)]
    public void ConferirClassificacaoRecomendada_AusenteOuDesconhecida_RecusaNoCampo(string? codigo, string recusa)
    {
        FieldError erro = AprovacaoDaProtecao.ConferirClassificacaoRecomendada(
            CodigoInformado<ClassificacaoDeProtecao>.Ler(codigo)).Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("classificacaoRecomendada");
        erro.Error.Code.Should().Be(recusa);
        erro.Error.Message.Should().NotContain("INEXISTENTE");
    }

    [Fact(DisplayName = "A recomendação da escala é aceita")]
    public void ConferirClassificacaoRecomendada_DaEscala_Aceita() =>
        AprovacaoDaProtecao.ConferirClassificacaoRecomendada(ClassificacaoDeProtecao.Sensivel).IsSuccess.Should().BeTrue();
}
