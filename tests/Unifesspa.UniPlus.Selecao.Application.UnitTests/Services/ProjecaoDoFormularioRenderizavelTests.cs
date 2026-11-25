namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A projeção única do formulário renderizável (ADR-0139): a apresentação sai das entidades e dos
/// valores selecionáveis, e as regras, do recorte da definição avaliável; o certame divulgado e o
/// rascunho passam pelo mesmo caminho.
/// </summary>
public sealed class ProjecaoDoFormularioRenderizavelTests
{
    private const string Secao = "DADOS";

    [Fact(DisplayName = "Sem formulário da finalidade, não há o que projetar")]
    public void Projetar_SemFormularioDaFinalidade_Nulo()
    {
        Configuracao configuracao = new([Formulario(FinalidadeFormulario.Inscricao)], [], [], []);

        configuracao.Projetar(FinalidadeFormulario.Habilitacao).Should().BeNull();
    }

    [Fact(DisplayName = "A seção e o termo trazem o código com que aparecem nas regras; o bloco não tem")]
    public void Projetar_SecaoETermo_TrazemOCodigoNasRegras()
    {
        Configuracao configuracao = new(
            [Formulario(FinalidadeFormulario.Inscricao)],
            [Campo("NOME_SOCIAL", 0, TipoDeCampo.Texto, FinalidadeFormulario.Inscricao, formato: "NOME_PESSOA")],
            [],
            [Termo("VERACIDADE", FinalidadeFormulario.Inscricao)]);

        FormularioRenderizavel formulario = configuracao.Projetar(FinalidadeFormulario.Inscricao)!;

        formulario.Etapas.Select(static e => (e.Codigo, e.CodigoNasRegras)).Should().Equal(
            (Secao, DefinicaoDoProcesso.CodigoDaEtapa(FinalidadeFormulario.Inscricao, Secao)), ("REVISAO", (string?)null));
        formulario.Termos.Should().ContainSingle().Which.CodigoNasRegras.Should().Be(
            DefinicaoDoProcesso.CodigoDoTermo(FinalidadeFormulario.Inscricao, "VERACIDADE"));
        formulario.Regras.Etapas.Select(static e => e.Codigo).Should().Contain(formulario.Etapas[0].CodigoNasRegras);
    }

    [Fact(DisplayName = "O NOME congelado com formato é publicado sem formato; o nome da mãe segue com o formato")]
    public void Projetar_NomeCivilComFormatoCongelado_PublicaSemFormato()
    {
        Configuracao configuracao = new(
            [Formulario(FinalidadeFormulario.Inscricao)],
            [
                Campo("NOME", 0, TipoDeCampo.Texto, FinalidadeFormulario.Inscricao, formato: "NOME_PESSOA"),
                Campo("NOME_MAE", 1, TipoDeCampo.Texto, FinalidadeFormulario.Inscricao, formato: "NOME_PESSOA"),
            ],
            [],
            []);

        FormularioRenderizavel formulario = configuracao.Projetar(FinalidadeFormulario.Inscricao)!;

        formulario.Coleta.Itens.Single(static f => f.FatoCodigo == "NOME").Formato.Should().BeNull();
        formulario.Coleta.Itens.Single(static f => f.FatoCodigo == "NOME_MAE").Formato.Should().Be("NOME_PESSOA");
    }

    [Fact(DisplayName = "O NOME_SOCIAL congelado sem a orientação é publicado com ela; os demais campos seguem com a ajuda congelada")]
    public void Projetar_NomeSocialSemAjudaCongelada_PublicaAOrientacaoDaConta()
    {
        Configuracao configuracao = new(
            [Formulario(FinalidadeFormulario.Inscricao)],
            [
                Campo("NOME_SOCIAL", 0, TipoDeCampo.Texto, FinalidadeFormulario.Inscricao, formato: "NOME_PESSOA"),
                Campo("NOME_MAE", 1, TipoDeCampo.Texto, FinalidadeFormulario.Inscricao, formato: "NOME_PESSOA"),
            ],
            [],
            []);

        FormularioRenderizavel formulario = configuracao.Projetar(FinalidadeFormulario.Inscricao)!;

        formulario.Coleta.Itens.Single(static f => f.FatoCodigo == "NOME_SOCIAL").Apresentacao.Ajuda
            .Should().Be(ConjuntoBasicoDaInscricao.Itens.Single(static i => i.FatoCodigo == "NOME_SOCIAL").Apresentacao!.Ajuda);
        formulario.Coleta.Itens.Single(static f => f.FatoCodigo == "NOME_MAE").Apresentacao.Ajuda.Should().BeNull();
    }

    [Fact(DisplayName = "Fora da inscrição, o NOME_SOCIAL segue com a ajuda congelada: a conferência com a conta é da inscrição")]
    public void Projetar_NomeSocialForaDaInscricao_SegueComAAjudaCongelada()
    {
        Configuracao configuracao = new(
            [Formulario(FinalidadeFormulario.Habilitacao)],
            [Campo("NOME_SOCIAL", 0, TipoDeCampo.Texto, FinalidadeFormulario.Habilitacao, formato: "NOME_PESSOA")],
            [],
            []);

        FormularioRenderizavel formulario = configuracao.Projetar(FinalidadeFormulario.Habilitacao)!;

        formulario.Coleta.Itens.Single(static f => f.FatoCodigo == "NOME_SOCIAL").Apresentacao.Ajuda.Should().BeNull();
    }

    [Fact(DisplayName = "O campo de seleção traz os valores na ordem de apresentação, com a orientação congelada; o de texto, o formato e nenhum valor")]
    public void Projetar_Campos_TrazemValoresEFormato()
    {
        Configuracao configuracao = ConfiguracaoComValores();

        FormularioRenderizavel formulario = configuracao.Projetar(FinalidadeFormulario.Inscricao)!;

        formulario.Coleta.Itens[0].ValoresSelecionaveis!.Select(static v => v.Codigo).Should().Equal("BRANCA", "AMARELA", "PRETA");
        formulario.Coleta.Itens[0].ValoresSelecionaveis!.Select(static v => v.Orientacao)
            .Should().Equal(null, null, "Passa pela heteroidentificação.");
        formulario.Coleta.Itens[1].ValoresSelecionaveis.Should().BeNull();
        formulario.Coleta.Itens[1].Formato.Should().Be("NOME_PESSOA");
        formulario.Regras.Etapas.SelectMany(static e => e.Itens).Single(static i => i.FatoCodigo == "COR_RACA").Oferta
            .Should().BeEquivalentTo(["AMARELA", "BRANCA", "PRETA"]);
    }

    [Fact(DisplayName = "O pressuposto respondido em outro formulário traz a apresentação dele; o agregado de grupo de outro formulário e o derivado do sistema dizem de que são calculados")]
    public void Projetar_Pressupostos_DescritosPelaOrigem()
    {
        Configuracao configuracao = ConfiguracaoComPressupostos();

        IReadOnlyList<PressupostoRenderizavel> pressupostos = configuracao.Projetar(FinalidadeFormulario.Habilitacao)!.Pressupostos.Fatos;

        pressupostos.Should().BeEquivalentTo(
            [
                new PressupostoRenderizavel("COR_RACA", "COR_RACA", "ESCOLHA_UNICA", null, [new ValorSelecionavel("PRETA", "Preta", 0)], null),
                new PressupostoRenderizavel("FAIXA_ETARIA", null, null, null, null, ["DATA_NASCIMENTO"]),
                new PressupostoRenderizavel("RURAL_NA_FAMILIA", null, null, null, null, ["TRABALHADOR_RURAL"]),
            ]);
    }

    private static Configuracao ConfiguracaoComValores() => new(
        [Formulario(FinalidadeFormulario.Inscricao)],
        [Campo("COR_RACA", 0, TipoDeCampo.EscolhaUnica, FinalidadeFormulario.Inscricao),
         Campo("NOME_SOCIAL", 1, TipoDeCampo.Texto, FinalidadeFormulario.Inscricao, formato: "NOME_PESSOA")], [], [])
    {
        Valores = new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(StringComparer.Ordinal)
        {
            ["COR_RACA"] = [new("PRETA", "Preta", 1, "Passa pela heteroidentificação."), new("BRANCA", "Branca", 0), new("AMARELA", "Amarela", 1)],
            ["NOME_SOCIAL"] = null,
        },
    };

    private static Configuracao ConfiguracaoComPressupostos() => new(
        [Formulario(FinalidadeFormulario.Inscricao), Formulario(FinalidadeFormulario.Habilitacao)],
        [Campo("COR_RACA", 0, TipoDeCampo.EscolhaUnica, FinalidadeFormulario.Inscricao),
         Campo("CERTIFICADO", 0, TipoDeCampo.SimOuNao, FinalidadeFormulario.Habilitacao, quando: [("COR_RACA", "PRETA")]),
         Campo("DECLARACAO_RURAL", 1, TipoDeCampo.SimOuNao, FinalidadeFormulario.Habilitacao, quando: [("RURAL_NA_FAMILIA", true)]),
         Campo("DECLARACAO_MAIORIDADE", 2, TipoDeCampo.SimOuNao, FinalidadeFormulario.Habilitacao, quando: [("FAIXA_ETARIA", 18)])],
        [Familia()], [])
    {
        Valores = new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(StringComparer.Ordinal) { ["COR_RACA"] = [new("PRETA", "Preta", 0)] },
        Agregados = [new DefinicaoAgregado("RURAL_NA_FAMILIA", "COMPOSICAO_FAMILIAR", "TRABALHADOR_RURAL", OperacaoAgregado.Existe)],
    };

    private static GrupoColetado Familia() => GrupoColetadoDeTeste.Agrupando("COMPOSICAO_FAMILIAR", 1)
        .NaSecao(Secao)
        .ComOcorrencias(0, null)
        .ComCampos(Campo("TRABALHADOR_RURAL", 0, TipoDeCampo.SimOuNao, FinalidadeFormulario.Inscricao, etapa: null))
        .DaFinalidade(FinalidadeFormulario.Inscricao)
        .Construir();

    /// <summary>A configuração de um processo, como a configuração publicada ou a configuração viva a dão à projeção.</summary>
    private sealed record Configuracao(
        IReadOnlyList<FormularioProcesso> Formularios,
        IReadOnlyList<FatoColetado> Fatos,
        IReadOnlyList<GrupoColetado> Grupos,
        IReadOnlyList<TermoExigidoFormulario> Termos)
    {
        public IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> Valores { get; init; } =
            new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(StringComparer.Ordinal);

        public IReadOnlyList<DefinicaoAgregado> Agregados { get; init; } = [];

        public FormularioRenderizavel? Projetar(FinalidadeFormulario finalidade)
        {
            ProcessoSeletivo estrutura = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — SiSU");
            EnvelopeReidratado envelope = new(
                ConfiguracaoPublicadaDe(estrutura),
                DadosEdital.Criar(
                    "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), Guid.CreateVersion7()).Value!,
                new string('a', 64), "America/Sao_Paulo", retificacao: null, conformidade: null,
                dadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(ValoresSelecionaveisCongelados: Valores, AgregadosDosGrupos: Agregados));

            return ProjecaoDoFormularioRenderizavel.Projetar(
                finalidade, DefinicaoAvaliavelDoProcesso.DoCongelado(envelope).Value!, Formularios, Fatos, Grupos, Termos, dataReferenciaFatos: null);
        }

        private ConfiguracaoPublicada ConfiguracaoPublicadaDe(ProcessoSeletivo estrutura)
        {
            return new ConfiguracaoPublicada(
                new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, [.. estrutura.CronogramaFases]),
                new AvaliacaoDeclarada([.. estrutura.Etapas], [.. estrutura.CriteriosDesempate], estrutura.Classificacao!),
                new VagasDeclaradas([.. estrutura.DistribuicaoVagas], 1, estrutura.BonusRegional is not null,
                    estrutura.BonusRegional, null, estrutura.AtendimentoEspecializado!),
                new ExigenciasDeclaradas([.. estrutura.DocumentosExigidos], []),
                new ColetaDeclarada(ConfiguracaoPublicadaDeTeste.CamposDaColeta(Fatos, Grupos), Grupos, Formularios, Termos,
                    [], [], null),
                new DivulgacaoETaxaDeclaradas(null, null), null);
        }
    }

    private static FormularioProcesso Formulario(FinalidadeFormulario finalidade) =>
        FormularioProcesso.Criar(
            finalidade, null, null,
            [
                EtapaFormulario.Criar(new EtapaEstrutura(Secao, 0, TipoEtapaFormulario.Secao, null), "Dados", null, null).Value!,
                EtapaFormulario.Criar(new EtapaEstrutura("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite), "Revisão e aceite", null, null).Value!,
            ]).Value!;

    private static FatoColetado Campo(
        string codigo,
        int ordem,
        TipoDeCampo tipo,
        FinalidadeFormulario finalidade,
        string? formato = null,
        string? etapa = Secao,
        (string Fato, object Valor)[]? quando = null) =>
        FatoColetadoDeTeste.Coletando(codigo, ordem)
            .DoTipo(tipo)
            .ComFormato(formato)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ExibidoQuando(quando is null ? null : ExibicaoDeTeste.QuandoTodas([.. quando.Select(static c => (c.Fato, Operador.Igual, c.Valor))]))
            .NaSecao(etapa)
            .DaFinalidade(finalidade)
            .Construir();

    private static TermoExigidoFormulario Termo(string codigo, FinalidadeFormulario finalidade) =>
        TermoExigidoFormulario.Criar(
            codigo, 0,
            new VersaoTermoEscolhida(Guid.CreateVersion7(), Guid.CreateVersion7(), "TERMO_DE_TESTE", 1, "Veracidade", "Declaro a veracidade.", "Lei 9.784/1999", FormaAceite.RegistroSemEnderecoIp, new string('a', 64)),
            null, Obrigatoriedade.Sempre, finalidade).Value!;
}
