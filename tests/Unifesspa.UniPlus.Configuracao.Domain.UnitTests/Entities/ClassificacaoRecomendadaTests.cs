namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A regra da classificação recomendada no ajuste que não depende do item: obrigatória e da escala. É uma só, chamada
/// por quem confere a forma da decisão e pela transição do circuito.
/// </summary>
public sealed class ClassificacaoRecomendadaTests
{
    [Theory(DisplayName = "A recomendação ausente, a sentinela e a fora da escala são recusadas uma vez no campo classificacaoRecomendada")]
    [InlineData(null)]
    [InlineData(ClassificacaoDeProtecao.Nenhuma)]
    [InlineData((ClassificacaoDeProtecao)(-1))]
    public void ConferirClassificacaoRecomendada_Invalida_RecusaUmaVez(ClassificacaoDeProtecao? recomendada) =>
        AprovacaoDaProtecao.ConferirClassificacaoRecomendada(recomendada).Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "classificacaoRecomendada" && e.Error.Code == ProtecaoDoCatalogoErrorCodes.RecomendacaoObrigatoria);

    [Fact(DisplayName = "A recomendação da escala é aceita")]
    public void ConferirClassificacaoRecomendada_DaEscala_Aceita() =>
        AprovacaoDaProtecao.ConferirClassificacaoRecomendada(ClassificacaoDeProtecao.Sensivel).IsSuccess.Should().BeTrue();
}
