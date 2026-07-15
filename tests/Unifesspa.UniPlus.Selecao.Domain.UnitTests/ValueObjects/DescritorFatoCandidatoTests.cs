namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Cobre CA-10 da Story #847 (ADR-0111).</summary>
public sealed class DescritorFatoCandidatoTests
{
    [Fact(DisplayName = "DescritorFatoCandidato_Rejeita_Categorico_Sem_Dominio")]
    public void DescritorFatoCandidato_Rejeita_Categorico_Sem_Dominio()
    {
        Result<DescritorFatoCandidato> resultado = DescritorFatoCandidato.Criar(
            "MODALIDADE", TipoDominioFato.CategoricoEstatico, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DescritorFatoCandidato.DominioIncoerente");
    }

    [Theory(DisplayName = "DescritorFatoCandidato_Rejeita_Dominio_Em_Tipo_Nao_Categorico")]
    [InlineData(TipoDominioFato.Booleano)]
    [InlineData(TipoDominioFato.Numerico)]
    public void DescritorFatoCandidato_Rejeita_Dominio_Em_Tipo_Nao_Categorico(TipoDominioFato tipo)
    {
        Result<DescritorFatoCandidato> resultado = DescritorFatoCandidato.Criar("PCD", tipo, ["SIM", "NAO"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DescritorFatoCandidato.DominioIncoerente");
    }

    [Fact(DisplayName = "DescritorFatoCandidato aceita categórico estático com domínio preenchido")]
    public void DescritorFatoCandidato_Aceita_Categorico_Com_Dominio()
    {
        Result<DescritorFatoCandidato> resultado = DescritorFatoCandidato.Criar(
            "COR_RACA", TipoDominioFato.CategoricoEstatico, ["BRANCA", "PRETA", "PARDA"]);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "DescritorFatoCandidato aceita booleano/numérico sem domínio")]
    public void DescritorFatoCandidato_Aceita_NaoCategorico_Sem_Dominio()
    {
        Result<DescritorFatoCandidato> resultado = DescritorFatoCandidato.Criar("PCD", TipoDominioFato.Booleano, null);

        resultado.IsSuccess.Should().BeTrue();
    }
}
