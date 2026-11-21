namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Mappings;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public sealed class FatoCandidatoMappingTests
{
    [Fact(DisplayName = "O fato sai com a identificação, o descritivo e o estado")]
    public void ToDto_Categorico_SaiComDescritivoEEstado()
    {
        FatoCandidato fato = FatosCandidatoDeTeste.Declarado(
            DominioFato.Categorico, CardinalidadeFato.Escalar, FonteValoresFato.Global, ComponenteDeExibicao.Situacao);

        FatoCandidatoDto dto = fato.ToDto().Fato;

        dto.Id.Should().Be(fato.Id);
        dto.Codigo.Should().Be("FATO_DE_TESTE");
        dto.Descritivo.Should().Be(new DescritivoDoFatoDto("Fato de teste", "Descrição do fato.", "SITUACAO"));
        dto.SemeadoPeloSistema.Should().BeFalse();
        dto.Ativo.Should().BeTrue();
        dto.RegrasPadrao.Should().BeEmpty();
    }

    [Fact(DisplayName = "Os eixos, a fonte dos valores e a resolução saem agrupados na definição")]
    public void ToDto_CategoricoDeFonteGlobal_AgrupaOsEixosNaDefinicao()
    {
        DefinicaoDoFatoDto definicao = FatosCandidatoDeTeste
            .Declarado(DominioFato.Categorico, CardinalidadeFato.Escalar, FonteValoresFato.Global).ToDto().Fato.Definicao;

        definicao.Dominio.Should().Be("CATEGORICO");
        definicao.Cardinalidade.Should().Be("ESCALAR");
        definicao.Escopo.Should().Be("CANDIDATO");
        definicao.Formato.Should().BeNull();
        definicao.Valores.Fonte.Should().Be("GLOBAL");
        definicao.Resolucao.Should().Be(new ResolucaoDoFatoDto("DECLARADO", "INSCRICAO", "CAMPO_FORMULARIO:FATO_DE_TESTE"));
    }

    [Fact(DisplayName = "A classificação, a finalidade e a hipótese legal saem agrupadas na proteção")]
    public void ToDto_Fato_AgrupaAProtecao()
    {
        ProtecaoDoFatoDto protecao = FatosCandidatoDeTeste.Declarado(DominioFato.Booleano).ToDto().Fato.Definicao.Protecao;

        protecao.Classificacao.Should().Be("PESSOAL");
        protecao.Finalidade.Should().Be("Verificação dos requisitos do processo seletivo.");
        protecao.HipoteseLegal.Should().Be("CUMPRIMENTO_OBRIGACAO_LEGAL");
    }

    [Fact(DisplayName = "Os valores declarados saem em ordem, com o estado de cada um")]
    public void ToDto_ValoresDeclarados_SaemEmOrdemComOEstado()
    {
        FatoCandidato fato = FatosCandidatoDeTeste.Declarado(DominioFato.Categorico, CardinalidadeFato.Escalar, FonteValoresFato.Global);
        fato.AdicionarValorDominio("REGULAR", "Regular", 2, ativo: true);
        fato.AdicionarValorDominio("EJA", "Educação de jovens e adultos", 1, ativo: false, "Cursou o supletivo.");

        IReadOnlyList<FatoValorDominioDto> declarados = fato.ToDto().Fato.Definicao.Valores.Declarados;

        declarados.Should().Equal(
            new FatoValorDominioDto("EJA", "Educação de jovens e adultos", 1, false, "Cursou o supletivo."),
            new FatoValorDominioDto("REGULAR", "Regular", 2, true, null));
    }

    [Fact(DisplayName = "O fato de texto sai com o formato, sem fonte nem valores e sem operadores")]
    public void ToDto_Texto_SaiComOFormatoESemOperadores()
    {
        FatoDeManutencaoDto dto = FatosCandidatoDeTeste.Declarado(DominioFato.Texto).ToDto();

        dto.Fato.Definicao.Formato.Should().Be("LIVRE");
        dto.Fato.Definicao.Valores.Fonte.Should().BeNull();
        dto.Fato.Definicao.Valores.Declarados.Should().BeEmpty();
        dto.OperadoresAdmitidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "O envelope de manutenção traz os operadores do fato, os mesmos da consulta pública")]
    public void ToDto_Booleano_TrazOsOperadoresDoFato()
    {
        FatoCandidato fato = FatosCandidatoDeTeste.Declarado(DominioFato.Booleano);

        FatoDeManutencaoDto dto = fato.ToDto();

        dto.OperadoresAdmitidos.Should().Equal(OperadoresAdmitidosDoFato.De(fato));
        dto.OperadoresAdmitidos.Select(static o => o.Codigo).Should().Equal("IGUAL", "DIFERENTE");
    }
}
