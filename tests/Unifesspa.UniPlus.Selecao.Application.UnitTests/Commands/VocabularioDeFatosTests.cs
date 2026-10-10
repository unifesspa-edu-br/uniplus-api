namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

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
            ValoresDominio: null, "INSCRICAO", "CAMPO_FORMULARIO:DADO_PESSOAL", ValoresDominioDeclarados: null, FonteValores: null, Ativo: true, ClassificacaoProtecao: "PESSOAL"))
            .Should().BeNull();

    [Fact(DisplayName = "UF e município de residência são citáveis: a UF entre as 27 siglas, o município pelo código IBGE bem formado")]
    public void DominiosDinamicos_DerivadosDeResidencia()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Residência", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

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
        ValoresDominio: null, "INSCRICAO", $"ATRIBUTO_CANDIDATO:{codigo}", ValoresDominioDeclarados: null, FonteValores: fonte, Ativo: true, ClassificacaoProtecao: "PESSOAL");

    [Fact(DisplayName = "O domínio de contribuição de uma regra segue a fonte: valores do processo, ou todos os do catálogo")]
    public void DominioDeContribuicao_PelaFonte()
    {
        FatoCandidatoView global = new(
            Guid.CreateVersion7(), "FORMA_CONCLUSAO", "Forma de conclusão", null, "CATEGORICO", "DERIVADO", "MULTIVALORADO",
            ["REGULAR", "EJA"], "INSCRICAO", "REGRA_DERIVACAO:FORMA_CONCLUSAO",
            [new FatoValorDominioViewItem("REGULAR", "Regular", 0, true), new FatoValorDominioViewItem("EJA", "EJA", 1, false)],
            "GLOBAL", Ativo: true, ClassificacaoProtecao: "PESSOAL");
        Dictionary<string, DominioDeValores> dinamicos = new(StringComparer.Ordinal)
        {
            ["MODALIDADE"] = DominioDeValores.Enumerado(["AC", "LB_PPI"]),
        };

        VocabularioDeFatos.DominioDeContribuicao(global, dinamicos)
            .Should().BeEquivalentTo(["REGULAR", "EJA"], "o desativado é recusado como vínculo novo, não como fora do domínio");
        VocabularioDeFatos.DominioDeContribuicao(Derivado("MODALIDADE", "MODALIDADE"), dinamicos)
            .Should().BeEquivalentTo(["AC", "LB_PPI"]);
    }

    [Fact(DisplayName = "O agregado é resolvido pelo processo quando o fato de membro é campo de um grupo dele")]
    public void QueOProcessoResolve_AgregadoDeCampoDeGrupo_Incluido()
    {
        ProcessoSeletivo processo = Processo();
        GrupoColetado grupo = GrupoColetado.Criar(
            "COMPOSICAO", 0, FormularioDeTeste.Secao, "Composição familiar", 0, 5, null, Obrigatoriedade.Sempre,
            [FatoColetado.Criar("CATEGORIA_RENDA", 0, "Categoria de renda", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null).Value!]).Value!;
        processo.DefinirItens([], grupos: [grupo], finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

        VocabularioDeFatos.QueOProcessoResolve(processo, [Agregado("CATEGORIAS_RENDA_FAMILIA", "CATEGORIA_RENDA"), Agregado("SOB_GUARDA_NA_FAMILIA", "MENOR_SOB_GUARDA")])
            .Should().BeEquivalentTo(["CATEGORIAS_RENDA_FAMILIA"], "o fato de membro do outro agregado não é campo de grupo do processo");
    }

    [Fact(DisplayName = "As opções do processo de um agregado são as do fato de membro")]
    public void DominiosDinamicos_AgregadoComOpcoesDoProcesso_OpcoesDoMembro()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirOpcoesDeclaradas(
            "CATEGORIA_RENDA", [OpcaoDeclaradaFato.Criar("CATEGORIA_RENDA", "RURAL", "Trabalhador rural", 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        VocabularioDeFatos.DominiosDinamicos(processo, [Agregado("CATEGORIAS_RENDA_FAMILIA", "CATEGORIA_RENDA")])["CATEGORIAS_RENDA_FAMILIA"]
            .Contem("RURAL").Should().BeTrue();
    }

    private static ProcessoSeletivo Processo() => ProcessoSeletivo.Criar(
        "PS Agregado", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static FatoCandidatoView Agregado(string codigo, string membro) => new(
        Guid.CreateVersion7(), codigo, codigo, null, "CATEGORICO", "DERIVADO", "MULTIVALORADO",
        ValoresDominio: null, "HABILITACAO", $"AGREGACAO_GRUPO:{membro}", ValoresDominioDeclarados: null, FonteValores: "PROCESSO", Ativo: true, ClassificacaoProtecao: "PESSOAL");

    [Fact(DisplayName = "Os fatos cujos valores são modalidades são escolhidos pela fonte, não pelo código")]
    public void ComValoresDeModalidade_PelaFonte()
    {
        FatosDeModalidade fatos = VocabularioDeFatos.ComValoresDeModalidade(
            [Derivado("GRUPO_DE_COTA", "MODALIDADE"), Derivado("MODALIDADE", "GLOBAL"), Derivado("UF_RESIDENCIA", "GEO_UF")]);

        fatos.Codigos.Should().BeEquivalentTo(["GRUPO_DE_COTA"]);
    }
}
