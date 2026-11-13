namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.ConsultaAdministrativaDeTeste;

/// <summary>
/// Como cada item sai na resposta administrativa: o rótulo, a descrição, o tipo, o componente e o valor vêm do que o
/// processo congelou, e o derivado, a opção, a UF e o município saem no formato do contrato.
/// </summary>
public sealed class ObterInscricaoAdministrativaMontagemDosItensTests
{
    [Fact(DisplayName = "A ajuda do formulário e o nome do catálogo não saem no contrato administrativo: rótulo e descrição vêm do que o processo congelou")]
    public async Task Item_TrazRotuloEDescricaoCongelados_SemAjuda()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("NOME", "PESSOAL", TipoRenderizacao.Texto, Texto("Fernanda"), ajuda: "Digite o nome como no documento", descricao: "Nome civil do candidato");

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, ComDadosPessoais());

        ItemDeRespostaDto item = ItensDe(dto).Single();
        item.Rotulo.Should().Be("Rótulo de NOME");
        item.Descricao.Should().Be("Nome civil do candidato");
        item.Tipo.Should().Be(TiposDeItem.Texto);
        item.Componente.Should().Be(ComponentesDeItem.Padrao);
        JsonSerializer.Serialize(dto).Should().NotContain("Digite o nome");
    }

    [Fact(DisplayName = "O componente declarado no cadastro e congelado vale sobre o formato e a classificação, em minúsculas")]
    public async Task Componente_DeclaradoValeSobreOResto()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("RENDA_FAMILIAR_PER_CAPITA", "PESSOAL", TipoRenderizacao.Numero, JsonSerializer.SerializeToElement(1234.56m), componente: "MOEDA");

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, ComDadosPessoais());

        ItemDeRespostaDto renda = ItensDe(dto).Single();
        renda.Componente.Should().Be("moeda");
        renda.Tipo.Should().Be(TiposDeItem.Numero);
        renda.Valor!.Value.GetString().Should().Be("1234.56", "o número sai como texto decimal exato");
    }

    [Fact(DisplayName = "Opção de seleção única sai como código e rótulo")]
    public async Task Opcao_SaiComCodigoERotulo()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("COR_RACA", "PESSOAL", TipoRenderizacao.SelecaoUnica, Texto("PARDA"));

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, ComDadosPessoais());

        ItemDeRespostaDto item = ItensDe(dto).Single();
        item.Tipo.Should().Be(TiposDeItem.Opcao);
        item.Valor!.Value.GetProperty("codigo").GetString().Should().Be("PARDA");
    }

    [Theory(DisplayName = "A UF sai pelo nome da lista fixa, venha o valor como sigla ou como código IBGE, ainda que a versão tenha congelado só a sigla")]
    [InlineData("PA")]
    [InlineData("15")]
    public async Task Uf_SaiPeloNome(string valor)
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("NATURALIDADE_UF", "PESSOAL", TipoRenderizacao.SelecaoUnica, Texto(valor), origemValores: OrigemValoresColeta.UnidadesFederativas);

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, ComDadosPessoais());

        JsonElement uf = ItensDe(dto).Single().Valor!.Value;
        uf.GetProperty("codigo").GetString().Should().Be(valor);
        uf.GetProperty("rotulo").GetString().Should().Be("Pará");
    }

    [Fact(DisplayName = "O município gravado sai com o nome conferido no envio, na resposta, no subitem de grupo e no derivado, sem consulta ao Geo")]
    public async Task Municipio_SaiComONomeGravado()
    {
        JsonElement salvador = ValorDeMunicipio.Gravado("2927408", "Salvador");

        InscricaoAdministrativaDto dto = await ConsultarDto(CenarioComMunicipio(salvador), ComDadosPessoais());

        SecaoDaInscricaoDto secao = dto.Secoes.Single();
        secao.Itens.Single().Valor!.Value.GetRawText().Should().Be(salvador.GetRawText());
        RotuloDe(secao.Grupos.Single().Ocorrencias.Single().Itens.Single()).Should().Be("Salvador");
        RotuloDe(dto.Derivados.Single()).Should().Be("Salvador");
    }

    [Fact(DisplayName = "Fato derivado sai pelo nome, pela classificação e pelo tipo que a publicação congelou; o derivado sem aprovação fica de fora")]
    public async Task Derivado_SaiPeloQueAPublicacaoCongelou()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Derivado("MODALIDADE", "Modalidade de concorrência", "CATEGORICO", "INTERNO", Texto("AC_PCD"));
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosInternosConsultar);

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, diogo);

        ItemDeRespostaDto modalidade = dto.Derivados.Should().ContainSingle().Subject;
        modalidade.Rotulo.Should().Be("Modalidade de concorrência");
        modalidade.Tipo.Should().Be(TiposDeItem.Opcao);
    }

    [Fact(DisplayName = "A seção sai com o título que o formulário do processo congelou; a seção que a versão não tem sai sem título")]
    public async Task Secao_SaiComOTituloCongelado()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("NOME", "PESSOAL", TipoRenderizacao.Texto, Texto("Fernanda"), secao: "DADOS", ordem: 0)
            .Fato("ANTIGO", "PESSOAL", TipoRenderizacao.Texto, Texto("a"), secao: "SECAO_QUE_A_VERSAO_NAO_TEM", ordem: 1);

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, ComDadosPessoais());

        dto.Secoes.Single(static s => s.Codigo == "DADOS").Titulo.Should().Be("Dados");
        dto.Secoes.Single(static s => s.Codigo == "SECAO_QUE_A_VERSAO_NAO_TEM").Titulo.Should().BeNull();
    }

    private static DecisoesDeAcessoDeTeste ComDadosPessoais() =>
        new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);

    private static string? RotuloDe(ItemDeRespostaDto item) => item.Valor!.Value.GetProperty("rotulo").GetString();

    private static InscricaoAdministrativaDeTeste CenarioComMunicipio(JsonElement salvador) =>
        new InscricaoAdministrativaDeTeste(PersonasDeTeste.ErickIsaacRamos)
            .Fato("NATURALIDADE_MUNICIPIO", "PESSOAL", TipoRenderizacao.Municipio, salvador, restricoes: [new MunicipiosDaUf("NATURALIDADE_UF")])
            .Grupo(GrupoDeLocais(), new OcorrenciaDeGrupoInscricao("local-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["LOCAL_MUNICIPIO"] = salvador }))
            .Derivado("MUNICIPIO_RESIDENCIA", "Município de residência", "CATEGORICO", "PESSOAL", salvador);

    private static GrupoColetado GrupoDeLocais() =>
        GrupoColetado.Criar(
            "LOCAIS", 1, "DADOS", "Locais", minimo: 0, maximo: null, exibicao: null, Obrigatoriedade.Nunca,
            [FatoColetado.Criar(
                "LOCAL_MUNICIPIO", 0, "Município do local", TipoRenderizacao.Municipio, Obrigatoriedade.Nunca, null, "PESSOAL",
                InscricaoAdministrativaDeTeste.Hipotese, ProtecaoDoFatoDeTeste.Finalidade, "Município do local",
                restricoes: [new MunicipiosDaUf("NATURALIDADE_UF")]).Value!],
            FinalidadeFormulario.Inscricao).Value!;
}
