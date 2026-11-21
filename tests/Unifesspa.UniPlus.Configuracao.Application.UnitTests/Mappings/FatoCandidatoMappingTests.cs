namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Mappings;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;

public sealed class FatoCandidatoMappingTests
{
    private static CodigoComRotulo<Operador>[] Operadores(
        DominioFato dominio, CardinalidadeFato cardinalidade, FonteValoresFato? fonte = null) =>
        [.. Fato(dominio, cardinalidade, fonte).ToDto().OperadoresAdmitidos];

    [Fact(DisplayName = "Categórico multivalorado devolve os operadores lidos sobre o conjunto")]
    public void ToDto_CategoricoMultivalorado_LeSobreOConjunto() =>
        Operadores(DominioFato.Categorico, CardinalidadeFato.Multivalorado, FonteValoresFato.Processo).Should().Equal(
            Operador("IGUAL", "inclui"),
            Operador("DIFERENTE", "não inclui"),
            Operador("EM", "inclui algum de"),
            Operador("NAO_EM", "não inclui nenhum de"));

    [Fact(DisplayName = "Categórico de valor único devolve os operadores lidos sobre um valor")]
    public void ToDto_CategoricoEscalar_LeSobreUmValor() =>
        Operadores(DominioFato.Categorico, CardinalidadeFato.Escalar, FonteValoresFato.Global).Should().Equal(
            Operador("IGUAL", "é igual a"),
            Operador("DIFERENTE", "é diferente de"),
            Operador("EM", "é um de"),
            Operador("NAO_EM", "não é nenhum de"));

    [Fact(DisplayName = "Numérico devolve igualdade, diferença e os limites")]
    public void ToDto_Numerico_DevolveOsLimites() =>
        Operadores(DominioFato.Numerico, CardinalidadeFato.Escalar).Should().Equal(
            Operador("IGUAL", "é igual a"),
            Operador("DIFERENTE", "é diferente de"),
            Operador("MAIOR_IGUAL", "maior ou igual a"),
            Operador("MENOR_IGUAL", "menor ou igual a"));

    [Fact(DisplayName = "Booleano devolve só igualdade e diferença")]
    public void ToDto_Booleano_DevolveIgualdadeEDiferenca() =>
        Operadores(DominioFato.Booleano, CardinalidadeFato.Escalar).Should().Equal(
            Operador("IGUAL", "é igual a"),
            Operador("DIFERENTE", "é diferente de"));

    [Fact(DisplayName = "Fato de texto, que nenhuma regra cita, não devolve operador")]
    public void ToDto_Texto_NaoDevolveOperador() =>
        Operadores(DominioFato.Texto, CardinalidadeFato.Escalar).Should().BeEmpty();

    [Fact(DisplayName = "Categórico de conjunto devolve os operadores admitidos pela regra, cada um com o rótulo sobre o conjunto")]
    public void ToDto_CategoricoDeConjunto_DevolveOsAdmitidosComORotuloSobreOConjunto()
    {
        CodigoComRotulo<Operador>[] operadores = Operadores(DominioFato.Categorico, CardinalidadeFato.Multivalorado, FonteValoresFato.Processo);

        IReadOnlyList<Operador> admitidos = OperadoresDoFato.Admitidos(TipoDominioFato.CategoricoDinamico, CardinalidadeDoFato.Conjunto);
        operadores.Select(static o => o.Codigo).Should().Equal(admitidos.Select(static o => o.Codigo()));
        operadores.Select(static o => o.Rotulo).Should().Equal(
            admitidos.Select(static o => OperadoresDoFato.Rotulo(o, CardinalidadeDoFato.Conjunto)));
    }

    private static CodigoComRotulo<Operador> Operador(string codigo, string rotulo) =>
        CodigoComRotulo<Operador>.De(CatalogoDe<Operador>.Obter(codigo), rotulo);

    private static FatoCandidato Fato(DominioFato dominio, CardinalidadeFato cardinalidade, FonteValoresFato? fonte) =>
        CriacaoDoFato.CriarDoAdministrador(
            "FATO_DE_TESTE", "Fato de teste", "Descrição do fato.", dominio, cardinalidade, fonte, dominio == DominioFato.Texto ? FormatoTexto.Livre : null, "INSCRICAO", EscopoFato.Candidato,
            ClassificacaoDeProtecao.Pessoal, "Verificação dos requisitos do processo seletivo.", HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
}
