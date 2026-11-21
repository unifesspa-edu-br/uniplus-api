namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Mappings;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

public sealed class FaseCanonicaMappingTests
{
    [Theory(DisplayName = "O DTO diz o que o código da fase admite, sem depender do que está gravado")]
    [InlineData("INSCRICAO", false, false)]
    [InlineData("AVALIACAO", true, false)]
    [InlineData("HOMOLOGACAO_INSCRICOES", false, true)]
    [InlineData("HABILITACAO", false, false)]
    public void ToDto_ExpoeOQueOCodigoAdmite(string codigo, bool admiteAgruparEtapas, bool admiteComplementacao)
    {
        FaseCanonica fase = CriarFaseSemSinalizadores(codigo);

        FaseCanonicaDto dto = fase.ToDto();

        dto.AdmiteAgruparEtapas.Should().Be(admiteAgruparEtapas);
        dto.AdmiteComplementacao.Should().Be(admiteComplementacao);
        dto.AgrupaEtapas.Should().BeFalse("o sinalizador gravado é independente do que o código admite");
        dto.PermiteComplementacao.Should().BeFalse("o sinalizador gravado é independente do que o código admite");
    }

    [Fact(DisplayName = "O DTO mantém o sinalizador gravado ao lado do que o código admite")]
    public void ToDto_MantemOGravadoAoLadoDoAdmitido()
    {
        FaseCanonica fase = FaseCanonica.Criar(
            "HOMOLOGACAO_INSCRICOES", "Homologação das inscrições", null, baseLegal: null, origemData: "NO_EDITAL",
            sinalizadores: new SinalizadoresDaFase(
                AgrupaEtapas: false, PermiteComplementacao: true, ColetaInscricao: false, ColetaSolicitacaoIsencao: false)).Value!;

        FaseCanonicaDto dto = fase.ToDto();

        dto.PermiteComplementacao.Should().BeTrue();
        dto.AdmiteComplementacao.Should().BeTrue();
    }

    [Fact(DisplayName = "O DTO projeta a unidade sugerida congelada quando a fase a tem")]
    public void ToDto_ProjetaAUnidadeSugerida()
    {
        FaseCanonica fase = CriarFaseSemSinalizadores("INSCRICAO");
        Guid origemId = Guid.NewGuid();
        fase.DefinirUnidadeSugerida(new UnidadeSugerida(origemId, "CEPS", "Centro de Processos Seletivos"));

        FaseCanonicaDto dto = fase.ToDto();

        dto.UnidadeSugerida.Should().Be(new UnidadeSugeridaDto(origemId, "CEPS", "Centro de Processos Seletivos"));
    }

    [Fact(DisplayName = "O DTO deixa a unidade sugerida ausente quando a fase não a tem")]
    public void ToDto_SemUnidadeSugerida_Nula()
    {
        FaseCanonicaDto dto = CriarFaseSemSinalizadores("INSCRICAO").ToDto();

        dto.UnidadeSugerida.Should().BeNull();
    }

    private static FaseCanonica CriarFaseSemSinalizadores(string codigo) =>
        FaseCanonica.Criar(
            codigo, "Fase de teste", null, baseLegal: null, origemData: "NO_EDITAL",
            sinalizadores: new SinalizadoresDaFase(
                AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false)).Value!;
}
