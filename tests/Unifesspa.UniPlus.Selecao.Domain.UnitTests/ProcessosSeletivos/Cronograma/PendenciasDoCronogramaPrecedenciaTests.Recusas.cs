namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed partial class PendenciasDoCronogramaPrecedenciaTests
{
    /// <summary>As pendências do cronograma, na ordem em que o gate de publicação as confere.</summary>
    public enum Pendencia
    {
        AvaliacaoSemEtapa,
        EtapaSemFaseDeclarada,
        InscricaoPropriaSemFaseDeColeta,
        FaseDeColetaSemJanela,
        FaseSemUnidadeResponsavel,
        IsencaoSemPrazo,
        IsencaoMenorQueCincoDias,
        IsencaoNaoAbreComAInscricao,
        IsencaoNaoTerminaAntesDaInscricao,
        VagasSemFaseQueProduzResultado,
        ConclusaoDeclaradaSemPreliminar,
        AncoraDaFaseSemPreliminar,
        AncoraDaEtapaForaDosPreliminares,
        BancasSemRecorteDeCompetencia,
        EtapaComecaAntesDaFase,
    }

    private sealed record Recusa(string Codigo, string Mensagem, Action<CronogramaEmMontagem> Provoca);

    private static readonly Dictionary<Pendencia, Recusa> Recusas = new()
    {
        [Pendencia.AvaliacaoSemEtapa] = new(
            "ProcessoSeletivo.AvaliacaoSemEtapa",
            "Há uma fase que agrupa etapas no cronograma, mas nenhuma etapa acontece nela.",
            c => c.Fases.Add(FaseDeAvaliacao(c.ProximaOrdem))),
        [Pendencia.EtapaSemFaseDeclarada] = new(
            "ProcessoSeletivo.EtapaSemFaseDeclarada",
            "A etapa 'Prova' não declara a fase em que acontece — declare a fase antes de publicar ou retificar o processo.",
            c => c.Etapas.Add(EtapaNaFase(faseCodigo: null, ordem: 1))),
        [Pendencia.InscricaoPropriaSemFaseDeColeta] = new(
            "ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta",
            "A origem dos candidatos é inscrição própria, e nenhuma fase do cronograma coleta inscrição.",
            c => c.Origem = OrigemCandidatos.InscricaoPropria),
        [Pendencia.FaseDeColetaSemJanela] = new(
            "ProcessoSeletivo.FaseQueColetaInscricaoSemJanela",
            "A fase 'INSCRICAO' coleta inscrição e precisa de início e fim definidos para que o Edital declare o período.",
            c => c.Fases.Add(FaseDeInscricao(c.ProximaOrdem, inicio: null, fim: null))),
        [Pendencia.FaseSemUnidadeResponsavel] = new(
            "ProcessoSeletivo.FaseSemUnidadeResponsavel",
            "A fase 'FASE_SEM_UNIDADE' não tem unidade responsável definida — toda fase publicada responde a uma unidade da Unifesspa.",
            c => c.Fases.Add(FaseSemUnidade(c.ProximaOrdem))),
        [Pendencia.IsencaoSemPrazo] = new(
            "ProcessoSeletivo.JanelaDeIsencaoSemPrazo",
            "A fase 'SOLICITACAO_ISENCAO' abre a solicitação de isenção e precisa de início e fim definidos.",
            c => c.Fases.Add(FaseDeIsencao(c.ProximaOrdem, inicio: null, fim: null))),
        [Pendencia.IsencaoMenorQueCincoDias] = new(
            "ProcessoSeletivo.JanelaDeIsencaoMenorQueCincoDias",
            "A janela de solicitação de isenção precisa de ao menos cinco dias corridos, contados a partir do dia seguinte à abertura.",
            c => ComIsencaoDatada(c, AberturaDasInscricoes, InstanteEmBelem.Em(2026, 3, 3, 23, 59, 59))),
        [Pendencia.IsencaoNaoAbreComAInscricao] = new(
            "ProcessoSeletivo.JanelaDeIsencaoNaoAbreComAInscricao",
            "A janela de solicitação de isenção abre no mesmo instante em que abrem as inscrições.",
            c => ComIsencaoDatada(c, AberturaDasInscricoes.AddMinutes(1), InstanteEmBelem.Em(2026, 3, 20, 23, 59, 59))),
        [Pendencia.IsencaoNaoTerminaAntesDaInscricao] = new(
            "ProcessoSeletivo.JanelaDeIsencaoNaoTerminaAntesDaInscricao",
            "A solicitação de isenção precisa terminar antes do fim das inscrições — quem tem o pedido indeferido ainda se inscreve pagando.",
            c => ComIsencaoDatada(c, AberturaDasInscricoes, FimDasInscricoes)),
        [Pendencia.VagasSemFaseQueProduzResultado] = new(
            "ProcessoSeletivo.VagasSemFaseQueProduzResultado",
            "Há vagas ofertadas, e nenhuma fase do cronograma produz resultado.",
            c => c.ComVagas = true),
        [Pendencia.ConclusaoDeclaradaSemPreliminar] = new(
            "ProcessoSeletivo.ConclusaoDeclaradaSemPreliminar",
            "A fase 'FASE_COM_CONCLUSAO' declara a fase 'FASE_DA_BASE' como concluinte, mas não publica nenhum resultado preliminar — não há ciclo recursal a concluir.",
            c => c.Fases.Add(FaseComConclusaoSemPreliminar(c.ProximaOrdem))),
        [Pendencia.AncoraDaFaseSemPreliminar] = new(
            "RegraRecursoFase.FaseSemProdutoPreliminar",
            "A fase 'FASE_COM_RECURSO' admite recurso e não publica nenhum produto com papel preliminar — declare o resultado preliminar em que o prazo ancora.",
            c => c.Fases.Add(FaseComRecursoSemPreliminar(c.ProximaOrdem))),
        [Pendencia.AncoraDaEtapaForaDosPreliminares] = new(
            "EtapaProcesso.AncoraNaoEhProdutoPreliminarDaEtapa",
            "O recurso contra ato publicado ancora num resultado preliminar desta etapa — é a publicação dele que abre a janela.",
            c => c.Etapas.Add(EtapaComRecursoForaDosPreliminares())),
        [Pendencia.BancasSemRecorteDeCompetencia] = new(
            "BancaRequerida.RecorteDeCompetenciaObrigatorio",
            "A fase 'FASE_COM_BANCAS' requer mais de uma banca 'BANCA_ANALISE_RECURSOS' e cada uma precisa declarar as categorias de documento que julga.",
            c => c.Fases.Add(FaseComBancasIndistinguiveis(c.ProximaOrdem))),
        [Pendencia.EtapaComecaAntesDaFase] = new(
            "ProcessoSeletivo.EtapaComecaAntesDaFase",
            "A etapa \"Prova\" começa em 2026-02-28T08:00:00.0000000+00:00, antes da fase FASE_COM_JANELA (2026-03-01T00:00:00.0000000+00:00) — a etapa acontece dentro da fase.",
            ComEtapaQueComecaAntesDaFase),
    };

    private static void ComEtapaQueComecaAntesDaFase(CronogramaEmMontagem cronograma)
    {
        cronograma.Fases.Add(FaseComJanela(cronograma.ProximaOrdem));
        cronograma.Etapas.Add(EtapaQueComecaAntesDaFase());
    }
}
