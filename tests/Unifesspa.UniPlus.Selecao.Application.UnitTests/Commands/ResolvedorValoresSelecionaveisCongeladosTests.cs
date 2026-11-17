namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os valores que o candidato pode escolher em cada fato de seleção coletado. A oferta vazia de um fato de
/// escopo-processo é recusa da conferência da configuração (<c>ProcessoSeletivo.FatoColetadoSemValoresOfertados</c>), que
/// a conferência julga antes de canonicalizar; o resolvedor só recusa o fato do catálogo sem valor vigente.
/// </summary>
public sealed class ResolvedorValoresSelecionaveisCongeladosTests
{
    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PS Resolvedor", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static FatoCandidatoView FatoCategoricoDeEscopoProcesso(string codigo) => new(
        Id: Guid.CreateVersion7(),
        Codigo: codigo,
        Nome: codigo,
        Descricao: "Descrição do fato.",
        Dominio: "CATEGORICO",
        Origem: "DECLARADO",
        Cardinalidade: "MULTIVALORADO",
        ValoresDominio: null,
        PontoResolucao: "INSCRICAO",
        Binding: $"CAMPO_FORMULARIO:{codigo}",
        ValoresDominioDeclarados: null, FonteValores: "PROCESSO", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    private static Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> ResolverOsFatos(ProcessoSeletivo processo, params string[] fatos) =>
        ResolvedorValoresSelecionaveisCongelados.Resolver(
            processo, fatos.ToDictionary(static f => f, FatoCategoricoDeEscopoProcesso, StringComparer.Ordinal));

    [Fact(DisplayName = "Resolver congela, para fato de fonte do processo, as opções que o processo declarou, na ordem declarada")]
    public void Resolver_FatoDeFonteDoProcesso_CongelaAsOpcoesDeclaradas()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens(
            [FatoColetado.Criar("FATO_OPCAO_DE_TESTE", 0, "Fato de opção do processo", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null, origemValores: OrigemValoresColeta.OpcoesDoProcesso, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!],
            PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas(
            "FATO_OPCAO_DE_TESTE",
            [OpcaoDeclaradaFato.Criar("FATO_OPCAO_DE_TESTE", "2025", "Opção 2025", 0).Value!,
             OpcaoDeclaradaFato.Criar("FATO_OPCAO_DE_TESTE", "2024", "Opção 2024", 1).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Dictionary<string, FatoCandidatoView> catalogo = new(StringComparer.Ordinal)
        {
            ["FATO_OPCAO_DE_TESTE"] = FatoCategoricoDeEscopoProcesso("FATO_OPCAO_DE_TESTE"),
        };

        IReadOnlyList<ValorDominioDeclaradoCongelado>? valores =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogo).Value!["FATO_OPCAO_DE_TESTE"];

        valores!.Select(static v => v.Codigo).Should().Equal("2025", "2024");
        valores![0].Descricao.Should().Be("Opção 2025");
    }

    private static ProcessoSeletivo ProcessoComListaDeEsperaFormadaPelaFonte(string[] opcoesDaFonte)
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens(
        [
            FatoColetado.Criar("OPCAO_CURSO_1", 0, "1ª opção", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null,
                origemValores: OrigemValoresColeta.OpcoesDoProcesso, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
            FatoColetado.Criar("OPCAO_LISTA_ESPERA", 1, "Lista de espera", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Nunca, null,
                origemValores: OrigemValoresColeta.OpcoesDoProcesso, restricoes: [new OpcoesDasRespostas(["OPCAO_CURSO_1"])], classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
        ], PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();
        DefinirAsOpcoesDeclaradas(processo, opcoesDaFonte);

        return processo;
    }

    private static void DefinirAsOpcoesDeclaradas(ProcessoSeletivo processo, string[] opcoesDaFonte)
    {
        processo.DefinirOpcoesDeclaradas("OPCAO_CURSO_1",
            [.. opcoesDaFonte.Select(static (o, i) => OpcaoDeclaradaFato.Criar("OPCAO_CURSO_1", o, o, i).Value!)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas("OPCAO_LISTA_ESPERA",
            [OpcaoDeclaradaFato.Criar("OPCAO_LISTA_ESPERA", "MEDICINA", "MEDICINA", 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "Resposta de campo que forma as opções de outro precisa ser opção desse outro na publicação")]
    [InlineData(new[] { "MEDICINA" }, true)]
    [InlineData(new[] { "MEDICINA", "DIREITO" }, false)]
    public void Resolver_OpcoesDasRespostas_ExigemAsOpcoesDaFonteNoAlvo(string[] opcoesDaFonte, bool aceita)
    {
        ProcessoSeletivo processo = ProcessoComListaDeEsperaFormadaPelaFonte(opcoesDaFonte);

        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> resultado =
            ResolverOsFatos(processo, "OPCAO_CURSO_1", "OPCAO_LISTA_ESPERA");

        if (aceita)
        {
            resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        }
        else
        {
            resultado.Error!.Code.Should().Be(ItemFormularioErrorCodes.OpcoesDeOutroDominio);
            resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("formularios[HABILITACAO].itens[1].restricoes");
        }
    }

    [Fact(DisplayName = "Resolver congela, para fato de fonte dos municípios do bônus, os municípios da área em ordem de nome")]
    public void Resolver_FatoDosMunicipiosDoBonus_CongelaOsMunicipiosDaArea()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirBonusRegional(aplica: true, bonus: ConfiguracaoBonusRegional.Criar(ReferenciaRegra.De(RegraBonusCodigo.Multiplicativo, "v1", new string('a', 64)), 1.20m, null, null, new BaseLegalCongelada(Guid.NewGuid(), "PORTARIA_UNIFESSPA_NO_2514_2023", "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Institui inclusão regional", [("1505536", "Parauapebas", "PA"), ("1504208", "Marabá", "PA"), ("1500131", "Água Azul do Norte", "PA")])).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirItens(
            [FatoColetado.Criar("MUNICIPIO_EM_AREA_BONUS", 0, "Município", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null,
                origemValores: OrigemValoresColeta.MunicipiosDoBonus, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!],
            PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

        Dictionary<string, FatoCandidatoView> catalogo = new(StringComparer.Ordinal)
        {
            ["MUNICIPIO_EM_AREA_BONUS"] = FatoCategoricoDeEscopoProcesso("MUNICIPIO_EM_AREA_BONUS") with { FonteValores = "MUNICIPIOS_BONUS" },
        };

        IReadOnlyList<ValorDominioDeclaradoCongelado>? valores =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogo).Value!["MUNICIPIO_EM_AREA_BONUS"];

        valores!.Select(static v => (v.Codigo, v.Descricao)).Should().Equal(
            ("1500131", "Água Azul do Norte/PA"), ("1504208", "Marabá/PA"), ("1505536", "Parauapebas/PA"));
    }

    [Fact(DisplayName = "Resolver congela, para fato de fonte das UFs do Geo, as 27 UFs pela sigla, em ordem de nome")]
    public void Resolver_FatoDasUfsDoGeo_CongelaAsVinteESeteUfs()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens(
            [FatoColetado.Criar("NATURALIDADE_UF", 0, "UF de nascimento", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null,
                origemValores: OrigemValoresColeta.UnidadesFederativas, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!],
            PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

        Dictionary<string, FatoCandidatoView> catalogo = new(StringComparer.Ordinal)
        {
            ["NATURALIDADE_UF"] = FatoCategoricoDeEscopoProcesso("NATURALIDADE_UF") with { Cardinalidade = "ESCALAR", FonteValores = "GEO_UF" },
        };

        IReadOnlyList<ValorDominioDeclaradoCongelado>? valores =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogo).Value!["NATURALIDADE_UF"];

        valores.Should().HaveCount(27);
        valores!.Take(4).Select(static v => (v.Codigo, v.Descricao)).Should().Equal(("AC", "Acre"), ("AL", "Alagoas"), ("AP", "Amapá"), ("AM", "Amazonas"));
        valores.Select(static v => v.Ordem).Should().BeInAscendingOrder();
        valores.Select(static v => v.Codigo).Where(static c => c is "PA" or "PB" or "PR" or "SC" or "SP" or "SE")
            .Should().Equal(["PA", "PB", "PR", "SC", "SP", "SE"], "a ordem é alfabética sem acento: Pará, Paraíba, Paraná; Santa Catarina, São Paulo, Sergipe");
    }

    private static ProcessoSeletivo ProcessoComTipoDeDeficienciaColetado()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens(
            [FatoColetado.Criar("TIPO_DEFICIENCIA", 0, "Tipo de deficiência", TipoRenderizacao.SelecaoMultipla, Obrigatoriedade.Nunca, null, classificacaoProtecao: "SENSIVEL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!],
            PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static Dictionary<string, FatoCandidatoView> CatalogoComTiposDeDeficiencia(bool soTiposRemovidos) =>
        new(StringComparer.Ordinal)
        {
            ["TIPO_DEFICIENCIA"] = FatoCategoricoDeEscopoProcesso("TIPO_DEFICIENCIA") with
            {
                FonteValores = "TIPOS_DEFICIENCIA",
                ValoresDominioDeclarados = soTiposRemovidos ? [new FatoValorDominioViewItem("TEA", "Transtorno do espectro autista", 0, Ativo: false)] : null,
            },
        };

    [Theory(DisplayName = "Resolver recusa o tipo de deficiência quando o cadastro de tipos não tem tipo ativo, dizendo onde corrigir")]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolver_TipoDeficienciaSemTipoAtivoNoCadastro_RecusaApontandoOCadastro(bool soTiposRemovidos)
    {
        ProcessoSeletivo processo = ProcessoComTipoDeDeficienciaColetado();

        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> resultado =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, CatalogoComTiposDeDeficiencia(soTiposRemovidos));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FatoColetadoSemValoresOfertados");
        resultado.Error.Message.Should().Contain("cadastro de tipos de deficiência");
        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("formularios[HABILITACAO].itens[0].fatoCodigo");
    }

    [Fact(DisplayName = "Resolver recusa os campos na ordem do documento, e não na ordem em que o processo os guarda")]
    public void Resolver_CamposSemOfertaDefinidosForaDeOrdem_RecusaPelaPosicaoNoDocumento()
    {
        ProcessoSeletivo processo = ProcessoComDoisCamposDeSelecao(ordemDoPrimeiro: 1, ordemDoSegundo: 0);

        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> resultado =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, CatalogoSemValores("CAMPO_A", "CAMPO_B"));

        resultado.Errors.Select(static e => e.Field).Should().Equal(
            "formularios[HABILITACAO].itens[0].fatoCodigo", "formularios[HABILITACAO].itens[1].fatoCodigo");
        resultado.Errors.Select(static e => e.Error.Message).Should().SatisfyRespectively(
            static primeira => primeira.Should().Contain("'CAMPO_B'"),
            static segunda => segunda.Should().Contain("'CAMPO_A'"));
    }

    private static ProcessoSeletivo ProcessoComDoisCamposDeSelecao(int ordemDoPrimeiro, int ordemDoSegundo)
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens(
            [CampoDeSelecao("CAMPO_A", ordemDoPrimeiro), CampoDeSelecao("CAMPO_B", ordemDoSegundo)],
            PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static FatoColetado CampoDeSelecao(string codigo, int ordem) => FatoColetado.Criar(
        codigo, ordem, codigo, TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Nunca, null, classificacaoProtecao: "PESSOAL",
        hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    private static Dictionary<string, FatoCandidatoView> CatalogoSemValores(params string[] fatos) =>
        fatos.ToDictionary(static f => f, static f => FatoCategoricoDeEscopoProcesso(f) with { FonteValores = "GLOBAL" }, StringComparer.Ordinal);

    [Fact(DisplayName = "Resolver devolve lista vazia para CONDICAO_ATENDIMENTO sem nenhuma condição ofertada: a recusa é da conferência da configuração")]
    public void Resolver_CondicaoAtendimentoSemOferta_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirItens(
            [FatoColetado.Criar("CONDICAO_ATENDIMENTO", 0, "Condição de atendimento", TipoRenderizacao.SelecaoMultipla, Obrigatoriedade.Nunca, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!],
            PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();


        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> resultado = ResolverOsFatos(processo, "CONDICAO_ATENDIMENTO");

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!["CONDICAO_ATENDIMENTO"].Should().BeEmpty();
    }

    [Fact(DisplayName = "Resolver aceita CONDICAO_ATENDIMENTO coletável com condição ofertada e devolve lista não vazia")]
    public void Resolver_CondicaoAtendimentoComOferta_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar(
                [CondicaoOfertada.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência")], [], []).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirItens(
            [FatoColetado.Criar("CONDICAO_ATENDIMENTO", 0, "Condição de atendimento", TipoRenderizacao.SelecaoMultipla, Obrigatoriedade.Nunca, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!],
            PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

        Dictionary<string, FatoCandidatoView> catalogo = new(StringComparer.Ordinal)
        {
            ["CONDICAO_ATENDIMENTO"] = FatoCategoricoDeEscopoProcesso("CONDICAO_ATENDIMENTO"),
        };

        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> resultado =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!["CONDICAO_ATENDIMENTO"].Should().ContainSingle(v => v.Codigo == "PCD");
    }
}
