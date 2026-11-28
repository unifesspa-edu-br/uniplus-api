namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A projeção única do formulário apresentado (ADR-0139): a apresentação sai das entidades e dos
/// valores selecionáveis, e as regras, do recorte da definição avaliável; o certame divulgado e o
/// rascunho passam pelo mesmo caminho.
/// </summary>
public sealed class ProjecaoDoFormularioApresentadoTests
{
    private const string Secao = "DADOS";

    private const string CodigoDaInscricao = "INSCRICAO";

    private const string CodigoDaHabilitacao = "HABILITACAO";

    [Fact(DisplayName = "Sem formulário do código, não há o que projetar")]
    public void Projetar_SemFormularioDoCodigo_Nulo()
    {
        Cenario cenario = new();
        Configuracao configuracao = new(cenario, [cenario.Inscricao], [], [], []);

        configuracao.Projetar(CodigoDaHabilitacao).Should().BeNull();
    }

    [Fact(DisplayName = "A seção e o termo trazem o código com que aparecem nas regras; o bloco não tem")]
    public void Projetar_SecaoETermo_TrazemOCodigoNasRegras()
    {
        Cenario cenario = new();
        Configuracao configuracao = new(
            cenario, [cenario.Inscricao], [CampoDeNome("NOME_SOCIAL", 0, cenario.Inscricao)], [], [Termo("VERACIDADE", cenario.Inscricao)]);

        FormularioApresentado formulario = configuracao.Projetar(CodigoDaInscricao)!;

        formulario.Paginas.Select(static e => (e.Codigo, e.CodigoNasRegras)).Should().Equal(
            (Secao, DefinicaoDoProcesso.CodigoDaPagina(cenario.Inscricao.Codigo, Secao)), ("REVISAO", (string?)null));
        formulario.Termos.Should().ContainSingle().Which.CodigoNasRegras.Should().Be(
            DefinicaoDoProcesso.CodigoDoTermo(cenario.Inscricao.Codigo, "VERACIDADE"));
        formulario.Regras.Paginas!.Select(static e => e.Codigo).Should().Contain(formulario.Paginas[0].CodigoNasRegras);
    }

    [Fact(DisplayName = "Cada formulário traz só os itens, os grupos e os termos que são dele")]
    public void Projetar_ComDoisFormularios_SeparaOsItensPorFormulario()
    {
        Cenario cenario = new();
        Configuracao configuracao = new(
            cenario,
            [cenario.Inscricao, cenario.Habilitacao],
            [Campo("COR_RACA", 0, TipoDeCampo.EscolhaUnica, cenario.Inscricao), Campo("CERTIFICADO", 0, TipoDeCampo.SimOuNao, cenario.Habilitacao)],
            [],
            [Termo("VERACIDADE", cenario.Inscricao), Termo("COMPROMISSO", cenario.Habilitacao)]);

        FormularioApresentado habilitacao = configuracao.Projetar(CodigoDaHabilitacao)!;

        habilitacao.Coleta.Itens.Select(static i => i.FatoCodigo).Should().Equal("CERTIFICADO");
        habilitacao.Termos.Select(static t => t.Codigo).Should().Equal("COMPROMISSO");
    }

    [Fact(DisplayName = "O NOME congelado com formato é publicado sem formato; o nome da mãe segue com o formato")]
    public void Projetar_NomeCivilComFormatoCongelado_PublicaSemFormato()
    {
        Cenario cenario = new();
        Configuracao configuracao = new(
            cenario, [cenario.Inscricao], [CampoDeNome("NOME", 0, cenario.Inscricao), CampoDeNome("NOME_MAE", 1, cenario.Inscricao)], [], []);

        FormularioApresentado formulario = configuracao.Projetar(CodigoDaInscricao)!;

        formulario.Coleta.Itens.Single(static f => f.FatoCodigo == "NOME").Formato.Should().BeNull();
        formulario.Coleta.Itens.Single(static f => f.FatoCodigo == "NOME_MAE").Formato.Should().Be("NOME_PESSOA");
    }

    [Fact(DisplayName = "O NOME_SOCIAL congelado sem a orientação é publicado com ela; os demais campos seguem com a ajuda congelada")]
    public void Projetar_NomeSocialSemAjudaCongelada_PublicaAOrientacaoDaConta()
    {
        Cenario cenario = new();
        Configuracao configuracao = new(
            cenario, [cenario.Inscricao], [CampoDeNome("NOME_SOCIAL", 0, cenario.Inscricao), CampoDeNome("NOME_MAE", 1, cenario.Inscricao)], [], []);

        FormularioApresentado formulario = configuracao.Projetar(CodigoDaInscricao)!;

        formulario.Coleta.Itens.Single(static f => f.FatoCodigo == "NOME_SOCIAL").Apresentacao.Ajuda
            .Should().Be(ConjuntoBasicoDaInscricao.Itens.Single(static i => i.FatoCodigo == "NOME_SOCIAL").Apresentacao!.Ajuda);
        formulario.Coleta.Itens.Single(static f => f.FatoCodigo == "NOME_MAE").Apresentacao.Ajuda.Should().BeNull();
    }

    [Fact(DisplayName = "Fora da fase de inscrição, o NOME_SOCIAL segue com a ajuda congelada: a conferência com a conta é da inscrição")]
    public void Projetar_NomeSocialForaDaInscricao_SegueComAAjudaCongelada()
    {
        Cenario cenario = new();
        Configuracao configuracao = new(
            cenario,
            [cenario.Habilitacao],
            [Campo("NOME_SOCIAL", 0, TipoDeCampo.Texto, cenario.Habilitacao, formato: "NOME_PESSOA")],
            [],
            []);

        FormularioApresentado formulario = configuracao.Projetar(CodigoDaHabilitacao)!;

        formulario.Coleta.Itens.Single(static f => f.FatoCodigo == "NOME_SOCIAL").Apresentacao.Ajuda.Should().BeNull();
    }

    [Fact(DisplayName = "O campo de seleção traz os valores na ordem de apresentação, com a orientação congelada; o de texto, o formato e nenhum valor")]
    public void Projetar_Campos_TrazemValoresEFormato()
    {
        Configuracao configuracao = ConfiguracaoComValores();

        FormularioApresentado formulario = configuracao.Projetar(CodigoDaInscricao)!;

        formulario.Coleta.Itens[0].ValoresSelecionaveis!.Select(static v => v.Codigo).Should().Equal("BRANCA", "AMARELA", "PRETA");
        formulario.Coleta.Itens[0].ValoresSelecionaveis!.Select(static v => v.Orientacao)
            .Should().Equal(null, null, "Passa pela heteroidentificação.");
        formulario.Coleta.Itens[1].ValoresSelecionaveis.Should().BeNull();
        formulario.Coleta.Itens[1].Formato.Should().Be("NOME_PESSOA");
        formulario.Regras.Paginas!.SelectMany(static e => e.Itens).Single(static i => i.FatoCodigo == "COR_RACA").Oferta
            .Should().BeEquivalentTo(["AMARELA", "BRANCA", "PRETA"]);
    }

    [Fact(DisplayName = "O pressuposto respondido em outro formulário traz a apresentação dele; o agregado de grupo de outro formulário e o derivado do sistema dizem de que são calculados")]
    public void Projetar_Pressupostos_DescritosPelaOrigem()
    {
        Configuracao configuracao = ConfiguracaoComPressupostos();

        IReadOnlyList<FatoPressuposto> pressupostos = configuracao.Projetar(CodigoDaHabilitacao)!.Pressupostos.Fatos;

        pressupostos.Should().BeEquivalentTo(
            [
                new FatoPressuposto("COR_RACA", "COR_RACA", "ESCOLHA_UNICA", null, [new ValorSelecionavel("PRETA", "Preta", 0)], null),
                new FatoPressuposto("FAIXA_ETARIA", null, null, null, null, ["DATA_NASCIMENTO"]),
                new FatoPressuposto("RURAL_NA_FAMILIA", null, null, null, null, ["TRABALHADOR_RURAL"]),
            ]);
    }

    private static Configuracao ConfiguracaoComValores()
    {
        Cenario cenario = new();
        return new(
            cenario,
            [cenario.Inscricao],
            [Campo("COR_RACA", 0, TipoDeCampo.EscolhaUnica, cenario.Inscricao),
             Campo("NOME_SOCIAL", 1, TipoDeCampo.Texto, cenario.Inscricao, formato: "NOME_PESSOA")], [], [])
        {
            Valores = new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(StringComparer.Ordinal)
            {
                ["COR_RACA"] = [new("PRETA", "Preta", 1, "Passa pela heteroidentificação."), new("BRANCA", "Branca", 0), new("AMARELA", "Amarela", 1)],
                ["NOME_SOCIAL"] = null,
            },
        };
    }

    private static Configuracao ConfiguracaoComPressupostos()
    {
        Cenario cenario = new();
        return new(
            cenario,
            [cenario.Inscricao, cenario.Habilitacao],
            [Campo("COR_RACA", 0, TipoDeCampo.EscolhaUnica, cenario.Inscricao),
             Campo("CERTIFICADO", 0, TipoDeCampo.SimOuNao, cenario.Habilitacao, quando: [("COR_RACA", "PRETA")]),
             Campo("DECLARACAO_RURAL", 1, TipoDeCampo.SimOuNao, cenario.Habilitacao, quando: [("RURAL_NA_FAMILIA", true)]),
             Campo("DECLARACAO_MAIORIDADE", 2, TipoDeCampo.SimOuNao, cenario.Habilitacao, quando: [("FAIXA_ETARIA", 18)])],
            [Familia(cenario.Inscricao)], [])
        {
            Valores = new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(StringComparer.Ordinal) { ["COR_RACA"] = [new("PRETA", "Preta", 0)] },
            Agregados = [new DefinicaoAgregado("RURAL_NA_FAMILIA", "COMPOSICAO_FAMILIAR", "TRABALHADOR_RURAL", OperacaoAgregado.Existe)],
        };
    }

    private static GrupoColetado Familia(FormularioProcesso formulario) => GrupoColetadoDeTeste.Agrupando("COMPOSICAO_FAMILIAR", 1)
        .NaSecao(Secao)
        .ComOcorrencias(0, null)
        .ComCampos(Campo("TRABALHADOR_RURAL", 0, TipoDeCampo.SimOuNao, formulario, pagina: null))
        .Construir();

    /// <summary>
    /// As fases do processo e dois formulários dele: o de inscrição, ligado à fase que coleta inscrição, e o de habilitação,
    /// ligado a uma fase que não a coleta.
    /// </summary>
    private sealed class Cenario
    {
        public Cenario()
        {
            Estrutura = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — SiSU", out Guid faseDaInscricao);
            FaseDaHabilitacao = HomologacaoDasInscricoesDeTeste.FaseQueHomologaComRecurso(2, codigo: "HABILITACAO");
            Fases = [.. Estrutura.CronogramaFases, FaseDaHabilitacao];
            Cronograma = CronogramaDoProcesso.Do(Fases);
            Inscricao = Formulario(CodigoDaInscricao, faseDaInscricao);
            Habilitacao = Formulario(CodigoDaHabilitacao, FaseDaHabilitacao.Id);
        }

        public ProcessoSeletivo Estrutura { get; }

        public FaseCronograma FaseDaHabilitacao { get; }

        public IReadOnlyList<FaseCronograma> Fases { get; }

        public CronogramaDoProcesso Cronograma { get; }

        public FormularioProcesso Inscricao { get; }

        public FormularioProcesso Habilitacao { get; }

        private FormularioProcesso Formulario(string codigo, Guid faseId) =>
            FormularioProcesso.Criar(
                new FormularioDeclarado(CodigoDoFormulario.Criar(codigo).Value!, faseId, Obrigatorio: true), Cronograma, null,
                [
                    PaginaDoFormulario.Criar(new PaginaEstrutura(Secao, 0, TipoPaginaFormulario.Secao, null), "Dados", null, null).Value!,
                    PaginaDoFormulario.Criar(new PaginaEstrutura("REVISAO", 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite), "Revisão e aceite", null, null).Value!,
                ]).Value!;
    }

    /// <summary>A configuração de um processo, como a configuração publicada ou a configuração viva a dão à projeção.</summary>
    private sealed record Configuracao(
        Cenario Cenario,
        IReadOnlyList<FormularioProcesso> Formularios,
        IReadOnlyList<FatoColetado> Fatos,
        IReadOnlyList<GrupoColetado> Grupos,
        IReadOnlyList<TermoExigidoFormulario> Termos)
    {
        public IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> Valores { get; init; } =
            new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(StringComparer.Ordinal);

        public IReadOnlyList<DefinicaoAgregado> Agregados { get; init; } = [];

        public FormularioApresentado? Projetar(string codigo)
        {
            ConfiguracaoPublicada configuracao = ConfiguracaoPublicadaDe(Cenario);
            EnvelopeReidratado envelope = new(
                configuracao,
                DadosEdital.Criar(
                    "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), Guid.CreateVersion7()).Value!,
                new string('a', 64), "America/Sao_Paulo", retificacao: null, conformidade: null,
                dadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(ValoresSelecionaveisCongelados: Valores, AgregadosDosGrupos: Agregados));

            return ProjecaoDoFormularioApresentado.Projetar(
                CodigoDoFormulario.Criar(codigo).Value!, DefinicaoAvaliavelDoProcesso.DoCongelado(envelope).Value!, configuracao.Coleta, Cenario.Cronograma, dataReferenciaFatos: null);
        }

        private ConfiguracaoPublicada ConfiguracaoPublicadaDe(Cenario cenario)
        {
            ProcessoSeletivo estrutura = cenario.Estrutura;
            return new ConfiguracaoPublicada(
                new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, [.. cenario.Fases]),
                new AvaliacaoDeclarada([.. estrutura.Etapas], [.. estrutura.CriteriosDesempate], estrutura.Classificacao!),
                new VagasDeclaradas([.. estrutura.DistribuicaoVagas], 1, estrutura.Fada is not null,
                    estrutura.Fada, null, estrutura.AtendimentoEspecializado!),
                new ExigenciasDeclaradas([.. estrutura.DocumentosExigidos], []),
                new ColetaDeclarada(ConfiguracaoPublicadaDeTeste.CamposDaColeta(Fatos, Grupos), Grupos, Formularios, Termos,
                    [], [], null),
                new DivulgacaoETaxaDeclaradas(null, null), null);
        }
    }

    private static FatoColetado Campo(
        string codigo,
        int ordem,
        TipoDeCampo tipo,
        FormularioProcesso formulario,
        string? formato = null,
        string? pagina = Secao,
        (string Fato, object Valor)[]? quando = null) =>
        FatoColetadoDeTeste.Coletando(codigo, ordem)
            .DoTipo(tipo)
            .ComFormato(formato)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ExibidoQuando(quando is null ? null : ExibicaoDeTeste.QuandoTodas([.. quando.Select(static c => (c.Fato, Operador.Igual, c.Valor))]))
            .NaSecao(pagina)
            .Construir()
            .NoFormulario(formulario);

    private static FatoColetado CampoDeNome(string codigo, int ordem, FormularioProcesso formulario) =>
        Campo(codigo, ordem, TipoDeCampo.Texto, formulario, formato: "NOME_PESSOA");

    private static TermoExigidoFormulario Termo(string codigo, FormularioProcesso formulario) =>
        TermoExigidoFormulario.Criar(
            codigo, 0,
            new VersaoTermoEscolhida(Guid.CreateVersion7(), Guid.CreateVersion7(), "TERMO_DE_TESTE", 1, "Veracidade", "Declaro a veracidade.", "Lei 9.784/1999", FormaAceite.RegistroSemEnderecoIp, new string('a', 64)),
            null, Obrigatoriedade.Sempre, formulario).Value!;
}
