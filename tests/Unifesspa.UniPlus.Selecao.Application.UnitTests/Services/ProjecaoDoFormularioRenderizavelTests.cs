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
            [Campo("NOME_SOCIAL", 0, TipoRenderizacao.Texto, FinalidadeFormulario.Inscricao, formato: "NOME_PESSOA")],
            [],
            [Termo("VERACIDADE", FinalidadeFormulario.Inscricao)]);

        FormularioRenderizavel formulario = configuracao.Projetar(FinalidadeFormulario.Inscricao)!;

        formulario.Etapas.Select(static e => (e.Codigo, e.CodigoNasRegras)).Should().Equal(
            (Secao, DefinicaoDoProcesso.CodigoDaEtapa(FinalidadeFormulario.Inscricao, Secao)), ("REVISAO", (string?)null));
        formulario.Termos.Should().ContainSingle().Which.CodigoNasRegras.Should().Be(
            DefinicaoDoProcesso.CodigoDoTermo(FinalidadeFormulario.Inscricao, "VERACIDADE"));
        formulario.Regras.Etapas.Select(static e => e.Codigo).Should().Contain(formulario.Etapas[0].CodigoNasRegras);
    }

    [Fact(DisplayName = "O campo de seleção traz os valores na ordem de apresentação; o de texto, o formato e nenhum valor")]
    public void Projetar_Campos_TrazemValoresEFormato()
    {
        Configuracao configuracao = new(
            [Formulario(FinalidadeFormulario.Inscricao)],
            [
                Campo("COR_RACA", 0, TipoRenderizacao.SelecaoUnica, FinalidadeFormulario.Inscricao),
                Campo("NOME_SOCIAL", 1, TipoRenderizacao.Texto, FinalidadeFormulario.Inscricao, formato: "NOME_PESSOA"),
            ],
            [],
            [])
        {
            Valores = new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(StringComparer.Ordinal)
            {
                ["COR_RACA"] = [new("PRETA", "Preta", 1), new("BRANCA", "Branca", 0), new("AMARELA", "Amarela", 1)],
                ["NOME_SOCIAL"] = null,
            },
        };

        FormularioRenderizavel formulario = configuracao.Projetar(FinalidadeFormulario.Inscricao)!;

        formulario.FatosColetados[0].ValoresSelecionaveis!.Select(static v => v.Codigo).Should().Equal("BRANCA", "AMARELA", "PRETA");
        formulario.FatosColetados[1].ValoresSelecionaveis.Should().BeNull();
        formulario.FatosColetados[1].Formato.Should().Be("NOME_PESSOA");
        formulario.Regras.Etapas.SelectMany(static e => e.Itens).Single(static i => i.FatoCodigo == "COR_RACA").Oferta
            .Should().BeEquivalentTo(["AMARELA", "BRANCA", "PRETA"]);
    }

    [Fact(DisplayName = "O pressuposto respondido em outro formulário traz a apresentação dele; o agregado de grupo de outro formulário e o derivado do sistema dizem de que são calculados")]
    public void Projetar_Pressupostos_DescritosPelaOrigem()
    {
        GrupoColetado familia = GrupoColetado.Criar(
            "COMPOSICAO_FAMILIAR", 1, Secao, "Composição familiar", 0, null, null, Obrigatoriedade.Nunca,
            [Campo("TRABALHADOR_RURAL", 0, TipoRenderizacao.Booleano, FinalidadeFormulario.Inscricao, etapa: null)],
            FinalidadeFormulario.Inscricao).Value!;
        Configuracao configuracao = new(
            [Formulario(FinalidadeFormulario.Inscricao), Formulario(FinalidadeFormulario.Habilitacao)],
            [
                Campo("COR_RACA", 0, TipoRenderizacao.SelecaoUnica, FinalidadeFormulario.Inscricao),
                Campo("CERTIFICADO", 0, TipoRenderizacao.Booleano, FinalidadeFormulario.Habilitacao, quando: [("COR_RACA", "PRETA")]),
                Campo("DECLARACAO_RURAL", 1, TipoRenderizacao.Booleano, FinalidadeFormulario.Habilitacao, quando: [("RURAL_NA_FAMILIA", true)]),
                Campo("DECLARACAO_MAIORIDADE", 2, TipoRenderizacao.Booleano, FinalidadeFormulario.Habilitacao, quando: [("FAIXA_ETARIA", 18)]),
            ],
            [familia],
            [])
        {
            Valores = new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(StringComparer.Ordinal)
            {
                ["COR_RACA"] = [new("PRETA", "Preta", 0)],
            },
            Agregados = [new DefinicaoAgregado("RURAL_NA_FAMILIA", "COMPOSICAO_FAMILIAR", "TRABALHADOR_RURAL", OperacaoAgregado.Existe)],
        };

        IReadOnlyList<PressupostoRenderizavel> pressupostos = configuracao.Projetar(FinalidadeFormulario.Habilitacao)!.Pressupostos;

        pressupostos.Should().BeEquivalentTo(
            [
                new PressupostoRenderizavel("COR_RACA", "COR_RACA", "SELECAO_UNICA", null, [new ValorSelecionavel("PRETA", "Preta", 0)], null),
                new PressupostoRenderizavel("FAIXA_ETARIA", null, null, null, null, ["DATA_NASCIMENTO"]),
                new PressupostoRenderizavel("RURAL_NA_FAMILIA", null, null, null, null, ["TRABALHADOR_RURAL"]),
            ]);
    }

    /// <summary>A configuração de um processo, como o grafo reidratado ou a configuração viva a dão à projeção.</summary>
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
                new GrafoConfiguracao(
                    [.. estrutura.Etapas], estrutura.OfertaAtendimento!, [.. estrutura.DistribuicaoVagas], estrutura.BonusRegional,
                    [.. estrutura.CriteriosDesempate], estrutura.Classificacao!, [.. estrutura.CronogramaFases], [.. estrutura.DocumentosExigidos], [], null,
                    fatosColetados: Fatos, formularios: Formularios, termosExigidos: Termos, gruposColetados: Grupos),
                DadosEdital.Criar(
                    "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), Guid.CreateVersion7()).Value!,
                new string('a', 64), "America/Sao_Paulo", retificacao: null, conformidade: null,
                valoresSelecionaveisCongelados: Valores, agregadosDosGrupos: Agregados);

            return ProjecaoDoFormularioRenderizavel.Projetar(
                finalidade, DefinicaoAvaliavelDoProcesso.DoCongelado(envelope).Value!, Formularios, Fatos, Grupos, Termos, dataReferenciaFatos: null);
        }
    }

    private static FormularioProcesso Formulario(FinalidadeFormulario finalidade) =>
        FormularioProcesso.Criar(
            finalidade, null, null,
            [
                EtapaFormulario.Criar(Secao, 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null).Value!,
                EtapaFormulario.Criar("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão e aceite", null, null).Value!,
            ]).Value!;

    private static FatoColetado Campo(
        string codigo,
        int ordem,
        TipoRenderizacao tipo,
        FinalidadeFormulario finalidade,
        string? formato = null,
        string? etapa = Secao,
        (string Fato, object Valor)[]? quando = null) =>
        FatoColetado.Criar(
            codigo, ordem, codigo, tipo, Obrigatoriedade.Sempre,
            quando is null ? null : [.. quando.Select(static c => CondicaoPrecondicaoFato.Criar(0, c.Fato, Operador.Igual, JsonSerializer.SerializeToElement(c.Valor)).Value!)],
            etapaCodigo: etapa, finalidade: finalidade, formato: formato).Value!;

    private static TermoExigidoFormulario Termo(string codigo, FinalidadeFormulario finalidade) =>
        TermoExigidoFormulario.Criar(
            codigo, 0,
            new VersaoTermoEscolhida(Guid.CreateVersion7(), Guid.CreateVersion7(), "Veracidade", "Declaro a veracidade.", "Lei 9.784/1999", "REGISTRO_DIGITAL_SEM_LOG_IP", new string('a', 64)),
            null, Obrigatoriedade.Sempre, finalidade).Value!;
}
