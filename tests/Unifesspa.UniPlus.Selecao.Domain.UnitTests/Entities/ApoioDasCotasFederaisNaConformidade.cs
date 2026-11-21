namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioConformidadePublicabilidadeEstrutural;

/// <summary>As ofertas com as cotas federais, a cascata de remanejamento e as ações afirmativas, montadas sobre o processo conforme.</summary>
internal static class ApoioDasCotasFederaisNaConformidade
{
    /// <summary>A matriz legal completa (8×7), fallback AC — cada origem resolve nas outras sete, todas ofertadas.</summary>
    internal static ConfiguracaoCascataRemanejamento CascataCompleta()
    {
        List<DestinoRemanejamento> destinos = [];
        foreach (string origem in OrigensFederais)
        {
            string[] ordemDestinos = [.. OrigensFederais.Where(o => o != origem)];
            for (int i = 0; i < ordemDestinos.Length; i++)
            {
                destinos.Add(Destino(origem, i + 1, ordemDestinos[i]));
            }
        }

        return ConfiguracaoCascataRemanejamento.Criar(RegraCascataRemanejamento(), ModalidadesFederaisLei12711.Ac, destinos).Value!;
    }

    /// <summary>A ação afirmativa institucional, que não se soma às cotas da lei.</summary>
    internal const string AcaoAfirmativaInstitucional = "AC_I";

    /// <summary>
    /// As oito cotas da lei, a ampla e uma ação afirmativa retirando duas vagas da ampla, cada uma com a
    /// regra de aplicabilidade dada. Sem o código, a ação afirmativa é a institucional.
    /// </summary>
    internal static ProcessoSeletivo ProcessoComCotaEAcaoAfirmativa(
        PredicadoDnf regraDasCotas, PredicadoDnf regraDaAcaoAfirmativa, string codigoDaAcaoAfirmativa = AcaoAfirmativaInstitucional) =>
        ProcessoComCotasEAcoesAfirmativas(regraDasCotas, (codigoDaAcaoAfirmativa, regraDaAcaoAfirmativa));

    /// <summary>
    /// As oito cotas da lei, a ampla e cada ação afirmativa dada, retirando duas vagas da ampla, com a
    /// regra de aplicabilidade das cotas e a de cada ação afirmativa.
    /// </summary>
    internal static ProcessoSeletivo ProcessoComCotasEAcoesAfirmativas(
        PredicadoDnf regraDasCotas, params (string Codigo, PredicadoDnf Regra)[] acoesAfirmativas)
    {
        ConfiguracaoDistribuicaoVagas oferta = OfertaComCotasEAcoesAfirmativas(regraDasCotas, acoesAfirmativas);
        ProcessoSeletivo processo = ProcessoConforme([oferta]);
        processo.DefinirCascataRemanejamento(CascataCompleta()).IsSuccess.Should().BeTrue();
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        return processo;
    }

    internal static bool ItemDaExclusividadeOk(ProcessoSeletivo processo) =>
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo))
            .Single(i => i.Codigo == "derivacao_cota_e_acao_afirmativa_juntas").Ok;

    internal static ProcessoSeletivo CotaEAcaoAfirmativaPor(string fato, object valorDaCota, object valorDaAcaoAfirmativa) =>
        ProcessoComCotaEAcaoAfirmativa(Exige((fato, valorDaCota)), Exige((fato, valorDaAcaoAfirmativa)));

    internal static PredicadoDnf Exige(params (string Fato, object Valor)[] atomos) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([.. atomos.Select(static a => (0, CondicaoDnf.Criar(
            a.Fato, Operador.Igual, JsonSerializer.SerializeToElement(a.Valor)).Value!))]).Value!;

    internal static ReferenciaRegra RegraLei12711() => Regra(RegraDistribuicaoVagasCodigo.Lei12711, 'f');

    internal static ReferenciaRegra RegraCascataRemanejamento() => Regra(RegraRemanejamentoCodigo.Cascata, 'e');

    internal static DestinoRemanejamento Destino(string origem, int ordem, string destino) =>
        DestinoRemanejamento.Criar(origem, ordem, destino).Value!;

    internal static ReferenciaReservaDemograficaSnapshot Demografica() =>
        ReferenciaReservaDemograficaSnapshot.Criar(Guid.CreateVersion7(), "2022", 79m, 1.5m, 8.5m, "Censo 2022").Value!;

    internal static readonly string[] OrigensFederais =
    [
        ModalidadesFederaisLei12711.LbPpi, ModalidadesFederaisLei12711.LbQ, ModalidadesFederaisLei12711.LbPcd, ModalidadesFederaisLei12711.LbEp,
        ModalidadesFederaisLei12711.LiPpi, ModalidadesFederaisLei12711.LiQ, ModalidadesFederaisLei12711.LiPcd, ModalidadesFederaisLei12711.LiEp,
    ];

    /// <summary>
    /// As 8 modalidades federais (SegueCascata) + AC — a Lei 12.711 exige o conjunto completo sob Lei
    /// 12.711. Sem <c>quantidadeDeclarada</c>: sob o ramo federal, DENTRO_DA_RESERVA_LEGAL e RESTANTE_DAS_VAGAS_OFERTADAS
    /// são CALCULADAS pela fórmula do art. 10 — declarar quantidade aqui é recusado
    /// (QuantidadeCalculadaNaoInformavel).
    /// </summary>
    internal static List<ModalidadeSelecionada> AsOitoFederaisMaisAc() =>
    [
        .. OrigensFederais.Select(codigo =>
            Modalidade(codigo, NaturezaLegalModalidade.CotaReservada, RegraRemanejamentoModalidade.SegueCascata, quantidadeDeclarada: null)),
        Modalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.Nenhuma, quantidadeDeclarada: null),
    ];

    internal static ReferenciaRegra RegraAjusteArt11() => Regra("RECONCILIACAO-VAGAS-ARTIGO-11-PARAGRAFO-UNICO", 'd');

    internal static ConfiguracaoDistribuicaoVagas OfertaFederalCompleta() => ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
        new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 0.5m, RegraDeDistribuicao = RegraLei12711(), RegraDeAjuste = RegraAjusteArt11(), ReferenciaDemografica = Demografica(), Modalidades = AsOitoFederaisMaisAc() },
        new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    private static ConfiguracaoDistribuicaoVagas OfertaComCotasEAcoesAfirmativas(
        PredicadoDnf regraDasCotas, IEnumerable<(string Codigo, PredicadoDnf Regra)> acoesAfirmativas)
    {
        ModalidadeSelecionada[] acoes = [.. acoesAfirmativas.Select(static a => AcaoAfirmativaQueRetiraDaAmpla(a.Codigo, a.Regra))];
        ConfiguracaoDistribuicaoVagas oferta = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada
            {
                VagasOfertadasBase = 40,
                PercentualDaReservaLegal = 0.5m,
                RegraDeDistribuicao = RegraLei12711(),
                RegraDeAjuste = RegraAjusteArt11(),
                ReferenciaDemografica = Demografica(),
                Modalidades = ModalidadesComCotasEAcoesAfirmativas(regraDasCotas, acoes),
            },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        return oferta;
    }

    private static ModalidadeSelecionada AcaoAfirmativaQueRetiraDaAmpla(string codigo, PredicadoDnf regra) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade,
            ModalidadesFederaisLei12711.Ac, RegraRemanejamentoModalidade.DestinoUnico, ModalidadesFederaisLei12711.Ac, null, null, regra, null, "base legal", 2).Value!;

    private static IReadOnlyList<ModalidadeSelecionada> ModalidadesComCotasEAcoesAfirmativas(
        PredicadoDnf regraDasCotas, ModalidadeSelecionada[] acoesAfirmativas) =>
    [
        .. OrigensFederais.Select(codigo =>
            Modalidade(codigo, NaturezaLegalModalidade.CotaReservada, RegraRemanejamentoModalidade.SegueCascata, quantidadeDeclarada: null, regra: regraDasCotas)),
        Modalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.Nenhuma, quantidadeDeclarada: null),
        .. acoesAfirmativas,
    ];
}
