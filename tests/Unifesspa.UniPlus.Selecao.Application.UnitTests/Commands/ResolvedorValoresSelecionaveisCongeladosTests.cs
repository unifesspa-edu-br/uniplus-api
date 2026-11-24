namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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

    private static IReadOnlyList<ValorDominioDeclaradoCongelado>? ValoresCongeladosDe(ProcessoSeletivo processo, FatoCandidatoView fato) =>
        ResolvedorValoresSelecionaveisCongelados.Resolver(
            processo, new Dictionary<string, FatoCandidatoView>(StringComparer.Ordinal) { [fato.Codigo] = fato }).Value![fato.Codigo];

    private static void ColetarNaHabilitacao(ProcessoSeletivo processo, params FatoColetado[] fatos) =>
        processo.DefinirItens(fatos, finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

    private static FatoColetado CampoDeSelecaoComOpcoesDoProcesso(string codigo, int ordem, string rotulo) =>
        FatoColetadoDeTeste.Coletando(codigo, ordem)
            .ComRotulo(rotulo)
            .DoTipo(TipoRenderizacao.SelecaoUnica)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ComOrigem(OrigemValoresColeta.OpcoesDoProcesso)
            .Construir();

    [Fact(DisplayName = "Resolver congela, para fato de fonte do processo, as opções que o processo declarou, na ordem declarada")]
    public void Resolver_FatoDeFonteDoProcesso_CongelaAsOpcoesDeclaradas()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ColetarNaHabilitacao(processo, CampoDeSelecaoComOpcoesDoProcesso("FATO_OPCAO_DE_TESTE", 0, "Fato de opção do processo"));
        processo.DefinirOpcoesDeclaradas(
            "FATO_OPCAO_DE_TESTE",
            [OpcaoDeclaradaFato.Criar("FATO_OPCAO_DE_TESTE", "2025", "Opção 2025", 0).Value!,
             OpcaoDeclaradaFato.Criar("FATO_OPCAO_DE_TESTE", "2024", "Opção 2024", 1).Value!]).IsSuccess.Should().BeTrue();

        IReadOnlyList<ValorDominioDeclaradoCongelado>? valores = ValoresCongeladosDe(processo, FatoCategoricoDeEscopoProcesso("FATO_OPCAO_DE_TESTE"));

        valores!.Select(static v => v.Codigo).Should().Equal("2025", "2024");
        valores![0].Descricao.Should().Be("Opção 2025");
    }

    private static ProcessoSeletivo ProcessoComListaDeEsperaFormadaPelaFonte(string[] opcoesDaFonte)
    {
        ProcessoSeletivo processo = NovoProcesso();
        FatoColetado listaDeEspera = FatoColetadoDeTeste.Coletando("OPCAO_LISTA_ESPERA", 1)
            .ComRotulo("Lista de espera")
            .DoTipo(TipoRenderizacao.SelecaoUnica)
            .ComOrigem(OrigemValoresColeta.OpcoesDoProcesso)
            .ComRestricoes(new OpcoesDasRespostas(["OPCAO_CURSO_1"]))
            .Construir();
        ColetarNaHabilitacao(processo, CampoDeSelecaoComOpcoesDoProcesso("OPCAO_CURSO_1", 0, "1ª opção"), listaDeEspera);
        DefinirAsOpcoesDeclaradas(processo, opcoesDaFonte);

        return processo;
    }

    private static void DefinirAsOpcoesDeclaradas(ProcessoSeletivo processo, string[] opcoesDaFonte)
    {
        processo.DefinirOpcoesDeclaradas("OPCAO_CURSO_1",
            [.. opcoesDaFonte.Select(static (o, i) => OpcaoDeclaradaFato.Criar("OPCAO_CURSO_1", o, o, i).Value!)])
            .IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas("OPCAO_LISTA_ESPERA",
            [OpcaoDeclaradaFato.Criar("OPCAO_LISTA_ESPERA", "MEDICINA", "MEDICINA", 0).Value!])
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
            resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("formularios[0].itens[1].restricoes");
        }
    }

    [Fact(DisplayName = "Resolver congela, para fato de fonte dos municípios do bônus, os municípios da área em ordem de nome")]
    public void Resolver_FatoDosMunicipiosDoBonus_CongelaOsMunicipiosDaArea()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirBonusRegional(aplica: true, bonus: ConfiguracaoBonusRegional.Criar(ReferenciaRegra.De(RegraDeBonus.Multiplicativo.Codigo(), "v1", new string('a', 64)), 1.20m, null, null, new BaseLegalCongelada(Guid.NewGuid(), "PORTARIA_UNIFESSPA_NO_2514_2023", "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Institui inclusão regional", [("1505536", "Parauapebas", "PA"), ("1504208", "Marabá", "PA"), ("1500131", "Água Azul do Norte", "PA")])).Value!).IsSuccess.Should().BeTrue();
        ColetarNaHabilitacao(processo, FatoColetadoDeTeste.Coletando("MUNICIPIO_EM_AREA_BONUS", 0)
            .ComRotulo("Município")
            .DoTipo(TipoRenderizacao.SelecaoUnica)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ComOrigem(OrigemValoresColeta.MunicipiosDoBonus)
            .Construir());

        IReadOnlyList<ValorDominioDeclaradoCongelado>? valores = ValoresCongeladosDe(
            processo, FatoCategoricoDeEscopoProcesso("MUNICIPIO_EM_AREA_BONUS") with { FonteValores = "MUNICIPIOS_BONUS" });

        valores!.Select(static v => (v.Codigo, v.Descricao)).Should().Equal(
            ("1500131", "Água Azul do Norte/PA"), ("1504208", "Marabá/PA"), ("1505536", "Parauapebas/PA"));
    }

    [Fact(DisplayName = "Resolver congela, para fato de fonte das UFs do Geo, as 27 UFs pela sigla, em ordem de nome")]
    public void Resolver_FatoDasUfsDoGeo_CongelaAsVinteESeteUfs()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ColetarNaHabilitacao(processo, CampoDeUfDeNascimento());

        IReadOnlyList<ValorDominioDeclaradoCongelado>? valores = ValoresCongeladosDe(
            processo, FatoCategoricoDeEscopoProcesso("NATURALIDADE_UF") with { Cardinalidade = "ESCALAR", FonteValores = "GEO_UF" });

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
            [FatoColetadoDeTeste.Coletando("TIPO_DEFICIENCIA", 0)
                .ComRotulo("Tipo de deficiência")
                .DoTipo(TipoRenderizacao.SelecaoMultipla)
                .ComClassificacao("SENSIVEL")
                .Construir()], finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

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
        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("formularios[0].itens[0].fatoCodigo");
    }

    [Fact(DisplayName = "Resolver recusa os campos na ordem do documento, e não na ordem em que o processo os guarda")]
    public void Resolver_CamposSemOfertaDefinidosForaDeOrdem_RecusaPelaPosicaoNoDocumento()
    {
        ProcessoSeletivo processo = ProcessoComDoisCamposDeSelecao(ordemDoPrimeiro: 1, ordemDoSegundo: 0);

        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> resultado =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, CatalogoSemValores("CAMPO_A", "CAMPO_B"));

        resultado.Errors.Select(static e => e.Field).Should().Equal(
            "formularios[0].itens[0].fatoCodigo", "formularios[0].itens[1].fatoCodigo");
        resultado.Errors.Select(static e => e.Error.Message).Should().SatisfyRespectively(
            static primeira => primeira.Should().Contain("'CAMPO_B'"),
            static segunda => segunda.Should().Contain("'CAMPO_A'"));
    }

    private static ProcessoSeletivo ProcessoComDoisCamposDeSelecao(int ordemDoPrimeiro, int ordemDoSegundo)
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens(
            [CampoDeSelecao("CAMPO_A", ordemDoPrimeiro), CampoDeSelecao("CAMPO_B", ordemDoSegundo)], finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static FatoColetado CampoDeSelecao(string codigo, int ordem) => FatoColetadoDeTeste.Coletando(codigo, ordem)
        .DoTipo(TipoRenderizacao.SelecaoUnica)
        .Construir();

    private static Dictionary<string, FatoCandidatoView> CatalogoSemValores(params string[] fatos) =>
        fatos.ToDictionary(static f => f, static f => FatoCategoricoDeEscopoProcesso(f) with { FonteValores = "GLOBAL" }, StringComparer.Ordinal);

    [Fact(DisplayName = "Resolver devolve lista vazia para CONDICAO_ATENDIMENTO sem nenhuma condição ofertada: a recusa é da conferência da configuração")]
    public void Resolver_CondicaoAtendimentoSemOferta_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!)
            .IsSuccess.Should().BeTrue();
        ColetarNaHabilitacao(processo, CampoDeCondicaoDeAtendimento());

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
                [CondicaoOfertada.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência")], [], []).Value!).IsSuccess.Should().BeTrue();
        ColetarNaHabilitacao(processo, CampoDeCondicaoDeAtendimento());

        IReadOnlyList<ValorDominioDeclaradoCongelado>? valores =
            ValoresCongeladosDe(processo, FatoCategoricoDeEscopoProcesso("CONDICAO_ATENDIMENTO"));

        valores.Should().ContainSingle(v => v.Codigo == "PCD");
    }

    private static FatoColetado CampoDeUfDeNascimento() => FatoColetadoDeTeste.Coletando("NATURALIDADE_UF", 0)
        .ComRotulo("UF de nascimento")
        .DoTipo(TipoRenderizacao.SelecaoUnica)
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .ComOrigem(OrigemValoresColeta.UnidadesFederativas)
        .Construir();

    private static FatoColetado CampoDeCondicaoDeAtendimento() => FatoColetadoDeTeste.Coletando("CONDICAO_ATENDIMENTO", 0)
        .ComRotulo("Condição de atendimento")
        .DoTipo(TipoRenderizacao.SelecaoMultipla)
        .Construir();
}
