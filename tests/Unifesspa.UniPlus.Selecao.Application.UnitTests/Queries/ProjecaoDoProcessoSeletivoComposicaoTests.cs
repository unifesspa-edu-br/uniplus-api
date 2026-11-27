namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A projeção completa do processo reúne, em cada lista, todos os itens configurados, cada um uma só vez,
/// na ordem de leitura: etapas e fases pela ordem declarada, distribuições de vagas pela ordem em que o
/// processo as guarda. Os itens são declarados fora de ordem para que a ordem lida não seja a da declaração.
/// </summary>
public sealed class ProjecaoDoProcessoSeletivoComposicaoTests
{
    private static readonly Guid OfertaDeMedicinaId = new("01a0d640-0000-7000-8000-000000000b01");
    private static readonly Guid OfertaDeDireitoId = new("01a0d640-0000-7000-8000-000000000b02");
    private static readonly Guid OfertaDeAgronomiaId = new("01a0d640-0000-7000-8000-000000000b03");

    [Fact(DisplayName = "As etapas saem todas, cada uma uma vez, pela ordem declarada")]
    public void Projetar_VariasEtapas_TodasUmaVezNaOrdemDeclarada()
    {
        ProcessoSeletivoDto dto = ProjecaoDoProcessoSeletivo.Projetar(ProcessoComListasForaDeOrdem());

        dto.Etapas.Select(static e => e.Identificacao.Nome)
            .Should().Equal("Prova objetiva", "Prova discursiva", "Entrevista", "Análise de histórico");
    }

    [Fact(DisplayName = "As fases do cronograma saem todas, cada uma uma vez, pela ordem declarada")]
    public void Projetar_VariasFases_TodasUmaVezNaOrdemDeclarada()
    {
        ProcessoSeletivoDto dto = ProjecaoDoProcessoSeletivo.Projetar(ProcessoComListasForaDeOrdem());

        dto.CronogramaFases.Select(static f => f.Codigo).Should().Equal("INSCRICAO", "HOMOLOGACAO_INSCRICOES", "RESULTADO_FINAL");
        dto.CronogramaFases.Select(static f => f.Cronograma.Ordem).Should().Equal(1, 2, 3);
    }

    [Fact(DisplayName = "As distribuições de vagas saem todas, cada uma uma vez, na ordem em que o processo as guarda")]
    public void Projetar_VariasDistribuicoes_TodasUmaVezNaOrdemDoProcesso()
    {
        ProcessoSeletivo processo = ProcessoComListasForaDeOrdem();

        ProcessoSeletivoDto dto = ProjecaoDoProcessoSeletivo.Projetar(processo);

        dto.DistribuicaoVagas.Select(static d => d.OfertaCursoOrigemId)
            .Should().Equal(processo.DistribuicaoVagas.Select(static d => d.OfertaCursoOrigemId))
            .And.BeEquivalentTo([OfertaDeMedicinaId, OfertaDeDireitoId, OfertaDeAgronomiaId]);
    }

    private static ProcessoSeletivo ProcessoComListasForaDeOrdem()
    {
        ProcessoSeletivo processo = ProcessosDeTeste.SemConfiguracao("Processo seletivo de composição", OrigemCandidatos.InscricaoPropria);
        processo.DefinirEtapas([Etapa("Entrevista", 3), Etapa("Análise de histórico", 4), Etapa("Prova objetiva", 1), Etapa("Prova discursiva", 2)])
            .IsSuccess.Should().BeTrue();
        processo.DefinirCronogramaFases([Fase("RESULTADO_FINAL", 3), Fase("INSCRICAO", 1), Fase("HOMOLOGACAO_INSCRICOES", 2)], [])
            .IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas(
            [Distribuicao(OfertaDeDireitoId), Distribuicao(OfertaDeMedicinaId), Distribuicao(OfertaDeAgronomiaId)],
            FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static ConfiguracaoDistribuicaoVagas Distribuicao(Guid ofertaCursoOrigemId) =>
        ProcessoSeletivoConformeBuilder.DistribuicaoAmplaConcorrencia(ofertaCursoOrigemId: ofertaCursoOrigemId);

    private static FaseCronograma Fase(string codigo, int ordem) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.Da(
            Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static EtapaProcesso Etapa(string nome, int ordem)
    {
        TipoEtapaSnapshot tipo = TipoEtapaSnapshot.Criar(
            Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

        return EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(nome, CaraterEtapa.Ambas, tipo, ordem, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(
                Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null,
                RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
    }
}
