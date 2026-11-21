namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Mappings;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class FatoCandidatoMappingTests
{
    private static OperadorAdmitidoDto[] Operadores(
        DominioFato dominio, CardinalidadeFato cardinalidade, FonteValoresFato? fonte = null) =>
        [.. Fato(dominio, cardinalidade, fonte).ToDto().OperadoresAdmitidos];

    [Fact(DisplayName = "Categórico multivalorado devolve os operadores lidos sobre o conjunto")]
    public void ToDto_CategoricoMultivalorado_LeSobreOConjunto() =>
        Operadores(DominioFato.Categorico, CardinalidadeFato.Multivalorado, FonteValoresFato.Processo).Should().Equal(
            new OperadorAdmitidoDto("IGUAL", "inclui"),
            new OperadorAdmitidoDto("DIFERENTE", "não inclui"),
            new OperadorAdmitidoDto("EM", "inclui algum de"),
            new OperadorAdmitidoDto("NAO_EM", "não inclui nenhum de"));

    [Fact(DisplayName = "Categórico de valor único devolve os operadores lidos sobre um valor")]
    public void ToDto_CategoricoEscalar_LeSobreUmValor() =>
        Operadores(DominioFato.Categorico, CardinalidadeFato.Escalar, FonteValoresFato.Global).Should().Equal(
            new OperadorAdmitidoDto("IGUAL", "é igual a"),
            new OperadorAdmitidoDto("DIFERENTE", "é diferente de"),
            new OperadorAdmitidoDto("EM", "é um de"),
            new OperadorAdmitidoDto("NAO_EM", "não é nenhum de"));

    [Fact(DisplayName = "Numérico devolve igualdade, diferença e os limites")]
    public void ToDto_Numerico_DevolveOsLimites() =>
        Operadores(DominioFato.Numerico, CardinalidadeFato.Escalar).Should().Equal(
            new OperadorAdmitidoDto("IGUAL", "é igual a"),
            new OperadorAdmitidoDto("DIFERENTE", "é diferente de"),
            new OperadorAdmitidoDto("MAIOR_IGUAL", "maior ou igual a"),
            new OperadorAdmitidoDto("MENOR_IGUAL", "menor ou igual a"));

    [Fact(DisplayName = "Booleano devolve só igualdade e diferença")]
    public void ToDto_Booleano_DevolveIgualdadeEDiferenca() =>
        Operadores(DominioFato.Booleano, CardinalidadeFato.Escalar).Should().Equal(
            new OperadorAdmitidoDto("IGUAL", "é igual a"),
            new OperadorAdmitidoDto("DIFERENTE", "é diferente de"));

    [Fact(DisplayName = "Fato de texto, que nenhuma regra cita, não devolve operador")]
    public void ToDto_Texto_NaoDevolveOperador() =>
        Operadores(DominioFato.Texto, CardinalidadeFato.Escalar).Should().BeEmpty();

    private static FatoCandidato Fato(DominioFato dominio, CardinalidadeFato cardinalidade, FonteValoresFato? fonte) =>
        CriacaoDoFato.CriarDoAdministrador(
            "FATO_DE_TESTE", "Fato de teste", "Descrição do fato.", dominio, cardinalidade, fonte, dominio == DominioFato.Texto ? FormatoTexto.Livre : null, "INSCRICAO", EscopoFato.Candidato,
            ClassificacaoDeProtecao.Pessoal, "Verificação dos requisitos do processo seletivo.", HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
}
