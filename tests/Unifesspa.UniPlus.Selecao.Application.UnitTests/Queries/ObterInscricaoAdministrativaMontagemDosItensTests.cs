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
        InscricaoAdministrativaDeTeste cenario = ComAjudaEDescricao();

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, ComDadosPessoais());

        ItemDeRespostaDto item = ItensDe(dto).Single();
        item.Rotulo.Should().Be("Rótulo de NOME");
        item.Descricao.Should().Be("Nome civil do candidato");
        item.Tipo.Codigo.Should().Be("TEXTO");
        item.Componente.Codigo.Should().Be("PADRAO");
        JsonSerializer.Serialize(dto).Should().NotContain("Digite o nome");
    }

    [Fact(DisplayName = "O componente declarado no cadastro e congelado sai pelo código do contrato")]
    public async Task Componente_Declarado_SaiPeloCodigo()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("RENDA_FAMILIAR_PER_CAPITA", "PESSOAL", TipoDeCampo.Numero, JsonSerializer.SerializeToElement(1234.56m), new MetadadosDoFatoDeTeste
            {
                Componente = "MOEDA",
            });

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, ComDadosPessoais());

        ItemDeRespostaDto renda = ItensDe(dto).Single();
        renda.Componente.Codigo.Should().Be("MOEDA");
        renda.Tipo.Codigo.Should().Be("NUMERO");
        renda.Valor!.Value.GetString().Should().Be("1234.56", "o número sai como texto decimal exato");
    }

    [Fact(DisplayName = "Opção de seleção única sai como código e rótulo")]
    public async Task Opcao_SaiComCodigoERotulo()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("COR_RACA", "PESSOAL", TipoDeCampo.EscolhaUnica, Texto("PARDA"));

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, ComDadosPessoais());

        ItemDeRespostaDto item = ItensDe(dto).Single();
        item.Tipo.Codigo.Should().Be("OPCAO");
        item.Valor!.Value.GetProperty("codigo").GetString().Should().Be("PARDA");
    }

    [Theory(DisplayName = "A opção usa o rótulo congelado com qualquer componente")]
    [InlineData(null, "PADRAO")]
    [InlineData("SITUACAO", "SITUACAO")]
    public async Task Opcao_ComponenteDeExibicao_PreservaValorCongelado(string? componente, string codigoComponente)
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("SITUACAO_TESTE", "PESSOAL", TipoDeCampo.EscolhaUnica, Texto("EM_ANALISE"), new MetadadosDoFatoDeTeste { Componente = componente })
            .OpcaoCongelada("SITUACAO_TESTE", "EM_ANALISE", "Em análise");

        ItemDeRespostaDto item = ItensDe(await ConsultarDto(cenario, ComDadosPessoais())).Single();

        item.Tipo.Codigo.Should().Be("OPCAO");
        item.Componente.Codigo.Should().Be(codigoComponente);
        item.Valor!.Value.GetRawText().Should().Be("{\"codigo\":\"EM_ANALISE\",\"rotulo\":\"Em an\\u00E1lise\"}");
    }

    [Theory(DisplayName = "A UF sai pelo nome da lista fixa, venha o valor como sigla ou como código IBGE, ainda que a versão tenha congelado só a sigla")]
    [InlineData("PA")]
    [InlineData("15")]
    public async Task Uf_SaiPeloNome(string valor)
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("NATURALIDADE_UF", "PESSOAL", TipoDeCampo.EscolhaUnica, Texto(valor), new MetadadosDoFatoDeTeste
            {
                OrigemValores = OrigemValoresColeta.UnidadesFederativas,
            });

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
            .Derivado("MODALIDADE", "Modalidade de concorrência", TipoDoDado.Opcao, "INTERNO", Texto("AC_PCD"));
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosInternosConsultar);

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, diogo);

        ItemDeRespostaDto modalidade = dto.Derivados.Should().ContainSingle().Subject;
        modalidade.Rotulo.Should().Be("Modalidade de concorrência");
        modalidade.Tipo.Codigo.Should().Be("OPCAO");
    }

    [Fact(DisplayName = "A seção sai com o título que o formulário do processo congelou; a seção que a versão não tem sai sem título")]
    public async Task Secao_SaiComOTituloCongelado()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("NOME", "PESSOAL", TipoDeCampo.Texto, Texto("Fernanda"), secao: "DADOS", ordem: 0)
            .Fato("ANTIGO", "PESSOAL", TipoDeCampo.Texto, Texto("a"), secao: "SECAO_QUE_A_VERSAO_NAO_TEM", ordem: 1);

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, ComDadosPessoais());

        dto.Secoes.Single(static s => s.Codigo == "DADOS").Titulo.Should().Be("Dados");
        dto.Secoes.Single(static s => s.Codigo == "SECAO_QUE_A_VERSAO_NAO_TEM").Titulo.Should().BeNull();
    }

    private static DecisoesDeAcessoDeTeste ComDadosPessoais() =>
        new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);

    private static string? RotuloDe(ItemDeRespostaDto item) => item.Valor!.Value.GetProperty("rotulo").GetString();

    private static InscricaoAdministrativaDeTeste CenarioComMunicipio(JsonElement salvador) =>
        new InscricaoAdministrativaDeTeste(PersonasDeTeste.ErickIsaacRamos)
            .Fato("NATURALIDADE_MUNICIPIO", "PESSOAL", TipoDeCampo.Municipio, salvador, new MetadadosDoFatoDeTeste
            {
                Restricoes = [new MunicipiosDaUf("NATURALIDADE_UF")],
            })
            .Grupo(GrupoDeLocais(), new OcorrenciaDeGrupoInscricao("local-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["LOCAL_MUNICIPIO"] = salvador }))
            .Derivado("MUNICIPIO_RESIDENCIA", "Município de residência", TipoDoDado.Opcao, "PESSOAL", salvador);

    private static GrupoColetado GrupoDeLocais() =>
        GrupoColetadoDeTeste.Agrupando("LOCAIS", 1)
            .NaSecao("DADOS")
            .ComRotulo("Locais")
            .ComOcorrencias(0, null)
            .ComCampos(FatoColetadoDeTeste.Coletando("LOCAL_MUNICIPIO", 0)
                .ComRotulo("Município do local")
                .DoTipo(TipoDeCampo.Municipio)
                .ComCopia(ProtecaoDoFatoDeTeste.Copia() with { HipoteseLegal = InscricaoAdministrativaDeTeste.Hipotese, Descricao = "Município do local" })
                .ComRestricoes(new MunicipiosDaUf("NATURALIDADE_UF"))
                .Construir())
            .Construir();
    private static InscricaoAdministrativaDeTeste ComAjudaEDescricao()
    {
        return new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("NOME", "PESSOAL", TipoDeCampo.Texto, Texto("Fernanda"), new MetadadosDoFatoDeTeste
            {
                Ajuda = "Digite o nome como no documento",
                Descricao = "Nome civil do candidato",
            });
    }
}
