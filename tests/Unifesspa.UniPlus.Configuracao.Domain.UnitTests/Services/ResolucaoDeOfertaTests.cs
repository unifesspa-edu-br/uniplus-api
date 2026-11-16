namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ResolucaoDeOfertaTests
{
    private static OfertaCurso Oferta(string eMecCodigo) =>
        OfertaCurso.Criar(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            UnidadeOfertante.Criar(Guid.CreateVersion7(), "FACET", "Faculdade de Computação e Engenharia Elétrica", "Faculdade").Value!,
            "REGULAR",
            "PRESENCIAL",
            "EXTENSIVO",
            "REGULAR",
            ["MATUTINO"],
            eMecCodigo,
            "ENG-01",
            40,
            null,
            null).Value!;

    [Fact(DisplayName = "Nenhuma candidata é referência não encontrada")]
    public void Resolver_SemCandidatas_NaoEncontrada()
    {
        Result<OfertaCurso> resultado = ResolucaoDeOferta.Resolver([]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.ReferenciaNaoEncontrada);
    }

    [Fact(DisplayName = "Uma candidata é a oferta")]
    public void Resolver_UmaCandidata_Resolve()
    {
        OfertaCurso unica = Oferta("1262444");

        Result<OfertaCurso> resultado = ResolucaoDeOferta.Resolver([unica]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeSameAs(unica);
    }

    [Fact(DisplayName = "Duas ou mais candidatas são referência ambígua: a regra nunca escolhe")]
    public void Resolver_VariasCandidatas_Ambigua()
    {
        Result<OfertaCurso> resultado = ResolucaoDeOferta.Resolver([Oferta("1262444"), Oferta("1276153"), Oferta("1270446")]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.ReferenciaAmbigua);
        resultado.Error.Message.Should().Contain("3 ofertas");
    }

    [Fact(DisplayName = "A lista de candidatas é obrigatória")]
    public void Resolver_Nula_Lanca()
    {
        Action resolver = () => ResolucaoDeOferta.Resolver(null!);

        resolver.Should().Throw<ArgumentNullException>();
    }
}
