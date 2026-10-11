namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As modalidades em que o candidato concorre, pelas regras de aplicabilidade das modalidades
/// ofertadas (UNI-REQ-0076), com a opção pelas cotas da lei e o egresso de escola pública derivado
/// da origem escolar (UNI-REQ-0148): o que se aplica, o que falta responder e o que a avaliação diz
/// quando a configuração não deriva a modalidade.
/// </summary>
public sealed class AvaliacaoDaInscricaoTests
{
    private const string Secao = "RESERVA";
    private static readonly string Etapa = DefinicaoDoProcesso.CodigoDaEtapa(FinalidadeFormulario.Inscricao, Secao);

    private static readonly string[] Perguntas =
    [
        OrigemEscolar.FatoOndeCursou, OrigemEscolar.FatoFormaDeConclusao,
        "CONCORRER_PCD", "CONCORRER_EP", "CONCORRER_PPI", "CONCORRER_Q", "CONCORRER_RENDA",
    ];

    [Fact(DisplayName = "Egresso de escola pública que opta pelas cotas de renda e PPI concorre a AC, LI_EP, LB_EP, LI_PPI e LB_PPI")]
    public void EgressoComCotasDeRendaEPpi_ConcorreAsModalidadesDaLei()
    {
        ModalidadesDeConcorrencia modalidades = Avaliar(Configuracao(derivaModalidade: true), Respostas(
            ondeCursou: OrigemEscolar.SomentePublica, pcd: false, ep: true, ppi: true, q: false, renda: true)).Modalidades;

        modalidades.Derivada.Should().BeTrue();
        modalidades.Situacao.Should().Be(SituacaoDasModalidades.Concorre);
        modalidades.Itens.Select(static m => m.Codigo).Should().Equal("AC", "LB_EP", "LB_PPI", "LI_EP", "LI_PPI");
        modalidades.Itens.Single(static m => m.Codigo == "LB_PPI").Nome.Should().Be("Nome de LB_PPI");
        modalidades.Pendencias.Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem a opção pelas cotas, o candidato concorre só à ampla concorrência")]
    public void SemOpcaoPelasCotas_ConcorreSoAAmpla()
    {
        ModalidadesDeConcorrencia modalidades = Avaliar(Configuracao(derivaModalidade: true), Respostas(
            ondeCursou: OrigemEscolar.SomentePublica, pcd: false, ep: false, ppi: false, q: false, renda: false)).Modalidades;

        modalidades.Situacao.Should().Be(SituacaoDasModalidades.Concorre);
        modalidades.Itens.Select(static m => m.Codigo).Should().Equal("AC");
    }

    [Fact(DisplayName = "Origem escolar em branco deixa faltar resposta, e a origem escolar é o que falta, com rótulo e seção")]
    public void OrigemEscolarEmBranco_FaltaResponderAOrigem()
    {
        ModalidadesDeConcorrencia modalidades = Avaliar(Configuracao(derivaModalidade: true), Respostas(
            ondeCursou: null, pcd: false, ep: true, ppi: false, q: false, renda: false)).Modalidades;

        modalidades.Situacao.Should().Be(SituacaoDasModalidades.FaltaResponder);
        modalidades.Itens.Should().BeEmpty();
        modalidades.Pendencias.Should().Contain(new PendenciaDaAvaliacao(OrigemEscolar.FatoOndeCursou, $"Rótulo de {OrigemEscolar.FatoOndeCursou}", Secao));
        modalidades.Pendencias.Select(static p => p.FatoCodigo).Should().NotContain("CONCORRER_EP", "o que já foi respondido não falta");
    }

    [Fact(DisplayName = "Sem derivação da modalidade, a avaliação diz que não deriva, sem situação nem item")]
    public void SemDerivacao_NaoDeriva()
    {
        ModalidadesDeConcorrencia modalidades = Avaliar(Configuracao(derivaModalidade: false), Respostas(
            ondeCursou: OrigemEscolar.SomentePublica, pcd: false, ep: true, ppi: true, q: false, renda: true)).Modalidades;

        modalidades.Should().Be(ModalidadesDeConcorrencia.SemDerivacao);
    }

    [Fact(DisplayName = "As dependências da avaliação são os campos que a matriz alcança, inclusive pela origem escolar")]
    public void Dependencias_SaoOsCamposQueAMatrizAlcanca()
    {
        DependenciasDaAvaliacao dependencias = AvaliacaoDaInscricao.Dependencias(Configuracao(derivaModalidade: true));

        dependencias.Fatos.Should().BeEquivalentTo(Perguntas);
        dependencias.Grupos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem derivação da modalidade, a avaliação não depende de campo nenhum")]
    public void SemDerivacao_SemDependencias()
    {
        DependenciasDaAvaliacao dependencias = AvaliacaoDaInscricao.Dependencias(Configuracao(derivaModalidade: false));

        dependencias.Fatos.Should().BeEmpty();
        dependencias.Grupos.Should().BeEmpty();
    }

    [Fact(DisplayName = "A resposta que decide se um campo da matriz aparece também é dependência, e é ela que falta enquanto não se responde")]
    public void CampoDaMatrizCondicionado_ARespostaQueOExibeEDependencia()
    {
        ConfiguracaoAvaliavel configuracao = Configuracao(derivaModalidade: true, rendaSoParaBaixaRenda: true);
        Dictionary<string, JsonElement> semBaixaRenda = Respostas(
            ondeCursou: OrigemEscolar.SomentePublica, pcd: false, ep: true, ppi: false, q: false, renda: true);

        AvaliacaoDaInscricao.Dependencias(configuracao).Fatos.Should().Contain(BaixaRenda);
        Avaliar(configuracao, semBaixaRenda).Modalidades.Pendencias.Select(static p => p.FatoCodigo)
            .Should().Equal([BaixaRenda], "o campo de renda ainda não aparece: falta a resposta que decide se ele aparece");
    }

    private static ResultadoDaAvaliacaoDaInscricao Avaliar(ConfiguracaoAvaliavel configuracao, Dictionary<string, JsonElement> respostas)
    {
        Kernel.Results.Result<ResultadoDaAvaliacaoDaInscricao> resultado = AvaliacaoDaInscricao.Avaliar(
            configuracao,
            new EntradaDaAvaliacaoDaInscricao(
                respostas,
                new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal),
                new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                Envio: null));
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        return resultado.Value!;
    }

    private static Dictionary<string, JsonElement> Respostas(string? ondeCursou, bool pcd, bool ep, bool ppi, bool q, bool renda)
    {
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(pcd),
            ["CONCORRER_EP"] = JsonSerializer.SerializeToElement(ep),
            ["CONCORRER_PPI"] = JsonSerializer.SerializeToElement(ppi),
            ["CONCORRER_Q"] = JsonSerializer.SerializeToElement(q),
            ["CONCORRER_RENDA"] = JsonSerializer.SerializeToElement(renda),
        };
        if (ondeCursou is not null)
        {
            respostas[OrigemEscolar.FatoFormaDeConclusao] = JsonSerializer.SerializeToElement(OrigemEscolar.Regular);
            respostas[OrigemEscolar.FatoOndeCursou] = JsonSerializer.SerializeToElement(ondeCursou);
        }

        return respostas;
    }

    private const string BaixaRenda = "BAIXA_RENDA";

    private static ConfiguracaoAvaliavel Configuracao(bool derivaModalidade, bool rendaSoParaBaixaRenda = false)
    {
        PredicadoDnf soComBaixaRenda = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(1, CondicaoDnf.Criar(BaixaRenda, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;
        string[] perguntas = rendaSoParaBaixaRenda ? [BaixaRenda, .. Perguntas] : Perguntas;
        RegrasDerivacaoFato egresso = RegrasDerivacaoFato.CriarBooleana(
            OrigemEscolar.FatoEgresso, OrigemEscolar.RegrasDoEgresso, OrigemEscolar.DependenciasDoEgresso).Value!;
        FatoColetado[] campos = [.. perguntas.Select(static (f, ordem) => FatoColetado.Criar(
            f, ordem, $"Rótulo de {f}", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, "SENSIVEL",
            etapaCodigo: Secao, finalidade: FinalidadeFormulario.Inscricao).Value!)];
        ModalidadeSelecionada[] ofertadas = [.. Ofertadas()];
        List<RegrasDerivacaoFato> derivacoes = [egresso];
        if (derivaModalidade)
        {
            derivacoes.AddRange(DerivacoesDaOferta.Efetivas([], ofertadas).Select(static d =>
                d.ParaRegrasDerivacao([.. d.Regras.Select(static r => r.Contribui).OfType<string>()]).Value!));
        }

        DefinicaoFormulario definicao = new(
            [new DefinicaoEtapa(Etapa, null, [.. perguntas.Select(f => new DefinicaoItem(
                f, rendaSoParaBaixaRenda && f == "CONCORRER_RENDA" ? soComBaixaRenda : null, Obrigatoriedade.Sempre, []))])],
            [],
            derivacoes);

        return new ConfiguracaoAvaliavel(
            definicao,
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal),
            ArvoreExigenciasCongelada.DeGrafoReidratado([]),
            campos,
            [],
            ofertadas,
            [],
            []);
    }

    /// <summary>
    /// As modalidades da Lei de Cotas, com regras no molde das do cadastro: cada cota exige a opção
    /// pelas cotas e, na dimensão dela, a opção por concorrer; a de renda exige também a renda.
    /// </summary>
    private static IEnumerable<ModalidadeSelecionada> Ofertadas()
    {
        (string, bool) optou = (OpcaoPelasCotasDaLei.Fato, true);
        yield return Modalidade("AC", NaturezaLegalModalidade.Ampla);
        yield return Modalidade("AC_PCD", NaturezaLegalModalidade.AcaoAfirmativa, (OpcaoPelasCotasDaLei.Fato, false), ("CONCORRER_PCD", true));
        foreach ((string sufixo, string? opcao) in new[] { ("EP", (string?)null), ("PPI", "CONCORRER_PPI"), ("Q", "CONCORRER_Q"), ("PCD", "CONCORRER_PCD") })
        {
            (string, bool)[] atomos = opcao is null ? [optou] : [optou, (opcao, true)];
            yield return Modalidade($"LI_{sufixo}", NaturezaLegalModalidade.CotaReservada, atomos);
            yield return Modalidade($"LB_{sufixo}", NaturezaLegalModalidade.CotaReservada, [.. atomos, ("CONCORRER_RENDA", true)]);
        }
    }

    private static ModalidadeSelecionada Modalidade(string codigo, NaturezaLegalModalidade natureza, params (string Fato, bool Valor)[] atomos) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, $"Nome de {codigo}", natureza, ComposicaoVagasModalidade.ResidualDoVo,
            null, natureza == NaturezaLegalModalidade.CotaReservada ? RegraRemanejamentoModalidade.SegueCascata : RegraRemanejamentoModalidade.Nenhuma,
            null, null, null,
            PredicadoDnf.CriarDeCondicoesAgrupadas([.. atomos.Select(static a => (0, CondicaoDnf.Criar(
                a.Fato, Operador.Igual, JsonSerializer.SerializeToElement(a.Valor)).Value!))]).Value!,
            null, "Lei 12.711/2012").Value!;
}
