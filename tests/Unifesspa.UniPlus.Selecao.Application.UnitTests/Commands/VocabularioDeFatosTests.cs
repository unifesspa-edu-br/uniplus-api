namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Texto, data e endereço nunca são citados em regra configurada (ADR-0136): o dado pessoal não
/// entra no edital congelado. O endereço entra só pelos derivados de residência, e a data, por um
/// derivado relativo à data de referência.
/// </summary>
public sealed class VocabularioDeFatosTests
{
    [Theory(DisplayName = "Fato de texto, data ou endereço fica fora do vocabulário citável")]
    [InlineData("TEXTO")]
    [InlineData("DATA")]
    [InlineData("ENDERECO")]
    public void Classificar_DominioNaoCitavel_Nulo(string dominio) =>
        VocabularioDeFatos.Classificar(new FatoCandidatoView(
            Guid.CreateVersion7(), "DADO_PESSOAL", "Dado pessoal", null, dominio, "DECLARADO", "ESCALAR",
            ValoresDominio: null, "INSCRICAO", "CAMPO_INSCRICAO:DADO_PESSOAL", ValoresDominioDeclarados: null, FonteValores: null))
            .Should().BeNull();

    [Fact(DisplayName = "UF e município de residência são citáveis: a UF entre as 27 siglas, o município pelo código IBGE bem formado")]
    public void DominiosDinamicos_DerivadosDeResidencia()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Residência", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        VocabularioDeFatos.Classificar(Derivado("UF_RESIDENCIA", "GEO_UF")).Should().Be(TipoDominioFato.CategoricoDinamico);
        VocabularioDeFatos.Classificar(Derivado("MUNICIPIO_RESIDENCIA", "GEO_MUNICIPIO")).Should().Be(TipoDominioFato.CategoricoDinamico);

        Dictionary<string, DominioDeValores> dominios = VocabularioDeFatos.DominiosDinamicos(
            processo, [Derivado("UF_RESIDENCIA", "GEO_UF"), Derivado("MUNICIPIO_RESIDENCIA", "GEO_MUNICIPIO")]);

        dominios["UF_RESIDENCIA"].Contem("PA").Should().BeTrue();
        dominios["UF_RESIDENCIA"].Contem("XX").Should().BeFalse();
        dominios["MUNICIPIO_RESIDENCIA"].Contem("1504208").Should().BeTrue();
        dominios["MUNICIPIO_RESIDENCIA"].Contem("9904208").Should().BeFalse("não há UF com prefixo 99");
        dominios["MUNICIPIO_RESIDENCIA"].Contem("150420").Should().BeFalse("o código IBGE de município tem sete dígitos");
    }

    private static FatoCandidatoView Derivado(string codigo, string fonte) => new(
        Guid.CreateVersion7(), codigo, codigo, null, "CATEGORICO", "DERIVADO", "ESCALAR",
        ValoresDominio: null, "INSCRICAO", $"ATRIBUTO_CANDIDATO:{codigo}", ValoresDominioDeclarados: null, FonteValores: fonte);
}
