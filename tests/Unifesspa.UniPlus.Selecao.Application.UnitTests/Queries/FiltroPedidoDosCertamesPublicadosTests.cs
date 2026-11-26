namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class FiltroPedidoDosCertamesPublicadosTests
{
    [Fact(DisplayName = "Situação desconhecida é recusada")]
    public void Resolver_SituacaoDesconhecida_Recusa() =>
        FiltroPedidoDosCertamesPublicados.Resolver("desconhecida", null, null).Error!.Code
            .Should().Be(FiltroDeSituacaoDosCertamesPublicados.CodigoDeErro);

    [Fact(DisplayName = "Situação com espaços é recusada")]
    public void Resolver_SituacaoComEspacos_Recusa() =>
        FiltroPedidoDosCertamesPublicados.Resolver("   ", null, null).Error!.Code
            .Should().Be(FiltroDeSituacaoDosCertamesPublicados.CodigoDeErro);

    [Fact(DisplayName = "Busca com 201 caracteres é recusada")]
    public void Resolver_BuscaLonga_Recusa() =>
        FiltroPedidoDosCertamesPublicados.Resolver(null, null, new string('a', 201)).Error!.Code
            .Should().Be(TermoDeBusca.CodigoDeBuscaMuitoLonga);

    [Fact(DisplayName = "Busca com 200 caracteres úteis aceita espaços nas pontas")]
    public void Resolver_BuscaComEspacos_AceitaTextoAparado()
    {
        Result<FiltroDosCertamesPublicados> resultado = FiltroPedidoDosCertamesPublicados.Resolver(null, "AC", "  " + new string('a', 200) + "  ");
        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Busca!.Normalizado.Should().Be(new string('a', 200));
        resultado.Value.Modalidade.Should().Be("AC");
    }

    [Fact(DisplayName = "Situação inválida tem precedência sobre busca longa")]
    public void Resolver_SituacaoEBuscaInvalidas_RecusaSituacaoPrimeiro() =>
        FiltroPedidoDosCertamesPublicados.Resolver("inválida", null, new string('a', 201)).Error!.Code
            .Should().Be(FiltroDeSituacaoDosCertamesPublicados.CodigoDeErro);
}
