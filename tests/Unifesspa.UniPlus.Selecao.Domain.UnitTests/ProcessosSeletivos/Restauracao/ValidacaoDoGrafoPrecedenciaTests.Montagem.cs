namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ProcessoSeletivoRestaurarConfiguracaoTests;

public sealed partial class ValidacaoDoGrafoPrecedenciaTests
{
    /// <summary>
    /// O grafo congelado válido, com as alterações que cada violação faz sobre ele. As violações são
    /// aditivas, para que um mesmo grafo viole várias regras ao mesmo tempo sem uma desfazer a outra.
    /// </summary>
    private sealed class GrafoEmMontagem
    {
        public List<EtapaProcesso> EtapasAdicionais { get; } = [];

        public bool ProvaCompoeANota { get; set; } = true;

        public string? FaseDasEtapas { get; set; }

        public bool SemDistribuicao { get; set; }

        public List<ConfiguracaoDistribuicaoVagas> OfertasAdicionais { get; } = [];

        public List<ModalidadeSelecionada> ModalidadesDaOfertaBase { get; } = [];

        public int? OpcoesDeCurso { get; set; } = 1;

        public List<CriterioDesempate> Criterios { get; } = [];

        public List<RegraEliminacao> Eliminacoes { get; } = [];

        public bool ClassificacaoPeloEnem { get; set; }

        public List<FatoColetado> Fatos { get; } = [];

        public bool SemCronograma { get; set; }

        public bool FaseDaBaseAgrupaEtapas { get; set; } = true;

        public List<FaseCronograma> FasesAdicionais { get; } = [];

        public List<NoExigencia> Nos { get; } = [];

        public static GrafoEmMontagem Com(params Violacao[] violacoes)
        {
            GrafoEmMontagem grafo = new();
            foreach (Violacao violacao in violacoes)
            {
                Recusas[violacao].Provoca(grafo);
            }

            return grafo;
        }

        public GrafoConfiguracao Montar() => new(
            etapas: Etapas(),
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: Distribuicao(),
            bonusRegional: null,
            criteriosDesempate: Criterios,
            classificacao: ClassificacaoPeloEnem ? ClassificacaoEnemMediaPonderada(Eliminacoes) : Classificacao(Eliminacoes),
            cronogramaFases: Fases(),
            documentosExigidos: [],
            nosExigencia: Nos,
            referenciaTemporalFatos: null,
            fatosColetados: Fatos,
            numeroDeOpcoesDeCurso: OpcoesDeCurso);

        private List<EtapaProcesso> Etapas()
        {
            EtapaProcesso prova = ProvaCompoeANota ? EtapaDaProvaCongelada(Id(1), 1) : EtapaSemNotaCongelada(Id(1), 1);
            return [.. new[] { prova }.Concat(EtapasAdicionais).Select(etapa => FaseDasEtapas is null ? etapa : NaFase(etapa, FaseDasEtapas))];
        }

        private List<ConfiguracaoDistribuicaoVagas> Distribuicao() =>
            SemDistribuicao ? [] : [Oferta(Id(100), null, [.. ModalidadesDaOfertaBase]), .. OfertasAdicionais];

        private List<FaseCronograma> Fases() =>
            SemCronograma ? [] : [Fase(1, Id(200), FaseDaBaseAgrupaEtapas, FaseDaBase), .. FasesAdicionais];
    }
}
