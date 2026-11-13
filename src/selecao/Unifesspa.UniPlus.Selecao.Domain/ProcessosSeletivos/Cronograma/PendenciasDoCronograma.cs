namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class PendenciasDoCronograma
{
    /// <summary>
    /// Pendências do cronograma que não cabem no checklist booleano de
    /// <see cref="ChecklistDeConformidade.PendenciaDeConformidade"/>: cada uma tem o seu próprio <c>DomainError</c>
    /// nomeado. Avaliado pela prontidão do congelamento, sempre depois da conformidade.
    /// </summary>
    /// <remarks>
    /// Cada passo abaixo testa um predicado nomeado (<c>Ha*</c>) em vez de uma expressão inline, porque
    /// <see cref="ChecklistDeConformidade.AvaliarConformidade"/> chama os mesmos predicados para projetar um
    /// item por razão. A precedência (qual falha é devolvida primeiro) é decidida só aqui, pela ordem dos
    /// operandos de <c>??</c>; o checklist não reordena nem filtra.
    /// <para>
    /// Público porque o handler o antecipa antes de canonicalizar: sem isso, um
    /// processo com fase de coleta sem janela E referência temporal irresolvível receberia pelo
    /// endpoint a recusa da referência, diagnóstico obscuro para quem só precisa datar a inscrição.
    /// </para>
    /// </remarks>
    public static DomainError? PendenciaDoCronograma(EstadoDaConfiguracao estado, TimeZoneInfo? fusoInstitucional)
    {
        ArgumentNullException.ThrowIfNull(estado);

        return PendenciaDaAvaliacaoDasEtapas(estado)
            ?? PendenciaDaInscricao(estado)
            ?? RecusaDaFaseSemUnidadeResponsavel(estado)
            ?? PendenciaDaJanelaDeIsencao(estado, fusoInstitucional)
            ?? RecusaDasVagasSemFaseQueProduzResultado(estado)
            ?? PendenciaDoRecursoEDasBancas(estado)
            ?? JanelaDaEtapaNaFase.PendenciaDaJanelaDaEtapaNaFase(estado);
    }

    /// <summary>
    /// A fase de avaliação sem etapa e a etapa sem fase declarada (§3.5). Desde que a etapa passou a
    /// declarar a própria fase, a gravação do cronograma não recusa mais o primeiro estado na hora — ele é
    /// legítimo enquanto se monta o certame, e quem o barra é este gate, na publicação.
    /// </summary>
    private static DomainError? PendenciaDaAvaliacaoDasEtapas(EstadoDaConfiguracao estado) =>
        HaFaseDeAvaliacaoSemEtapa(estado) ? AvaliacaoSemEtapa() : RecusaDaEtapaSemFaseDeclarada(estado);

    /// <summary>A origem por inscrição própria sem fase que coleta, e a fase que coleta sem janela.</summary>
    private static DomainError? PendenciaDaInscricao(EstadoDaConfiguracao estado) =>
        RecusaDaInscricaoPropriaSemFaseDeColeta(estado) ?? RecusaDaFaseQueColetaInscricaoSemJanela(estado);

    // §3.4 — piso mínimo derivado da ORIGEM DOS CANDIDATOS, nunca do tipo.
    private static DomainError? RecusaDaInscricaoPropriaSemFaseDeColeta(EstadoDaConfiguracao estado) =>
        HaInscricaoPropriaSemFaseDeColeta(estado)
            ? new DomainError(
                "ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta",
                "A origem dos candidatos é inscrição própria, e nenhuma fase do cronograma coleta inscrição.")
            : null;

    // A fase que coleta inscrição declara o prazo que o Edital publica, então precisa de janela mesmo
    // quando a origem da data é NA_EXECUCAO. Nas demais fases, "sem data" é estado válido, porque a data
    // não é fixada no edital e a unidade responsável a define na execução. Um Edital que admite inscrição
    // sem prazo definido, esse não se sustenta.
    private static DomainError? RecusaDaFaseQueColetaInscricaoSemJanela(EstadoDaConfiguracao estado) =>
        FaseQueColetaInscricaoSemJanela(estado) is { } fase
            ? new DomainError(
                "ProcessoSeletivo.FaseQueColetaInscricaoSemJanela",
                $"A fase '{fase.Codigo}' coleta inscrição e precisa de início e fim definidos para que o Edital declare o período.")
            : null;

    private static DomainError? RecusaDaFaseSemUnidadeResponsavel(EstadoDaConfiguracao estado) =>
        FaseSemUnidadeResponsavel(estado) is { } fase
            ? new DomainError(
                "ProcessoSeletivo.FaseSemUnidadeResponsavel",
                $"A fase '{fase.Codigo}' não tem unidade responsável definida — toda fase publicada responde a uma unidade da Unifesspa.")
            : null;

    // §3.4 — havendo vagas ofertadas, o cronograma precisa de ao menos uma fase que produza resultado.
    private static DomainError? RecusaDasVagasSemFaseQueProduzResultado(EstadoDaConfiguracao estado) =>
        HaVagasSemFaseQueProduzResultado(estado)
            ? new DomainError(
                "ProcessoSeletivo.VagasSemFaseQueProduzResultado",
                "Há vagas ofertadas, e nenhuma fase do cronograma produz resultado.")
            : null;

    /// <summary>
    /// A conclusão do ciclo recursal por matéria, a âncora do prazo de recurso da fase e da etapa e o
    /// recorte de competência das bancas.
    /// </summary>
    /// <remarks>
    /// A gravação do cronograma e das etapas já recusa cada um, mas o estado violado é materializável pela
    /// hidratação do EF, que não passa pelas fábricas e lê direto das linhas as regras de recurso, os
    /// produtos, as bancas requeridas e os recursos da etapa. Sem esta metade, um certame carregado do banco
    /// nesse estado seria publicado com o ciclo recursal em aberto, com o prazo de interposição sem
    /// resolver ou com duas bancas do mesmo tipo indistinguíveis — e a incoerência só apareceria no
    /// primeiro parecer ou na restauração, longe da causa.
    /// </remarks>
    private static DomainError? PendenciaDoRecursoEDasBancas(EstadoDaConfiguracao estado) =>
        CicloRecursalDoCronograma.PendenciaDaConclusaoDoCicloRecursal(estado)
        ?? CicloRecursalDoCronograma.PendenciaDaAncoraDoRecurso(estado)
        ?? CicloRecursalDoCronograma.PendenciaDaAncoraDoRecursoDasEtapas(estado)
        ?? CicloRecursalDoCronograma.PendenciaDoRecorteDeCompetencia(estado);

    /// <summary>Há fase que agrupa etapas, e nenhuma etapa acontece nela (§3.5).</summary>
    internal static bool HaFaseDeAvaliacaoSemEtapa(EstadoDaConfiguracao estado) =>
        HaFaseDeAvaliacaoSemEtapa(estado.CronogramaFases, estado.Etapas);

    // §3.5 — a bicondicional por sinalizador sobrevive apenas para a etapa que NÃO
    // declara a própria fase. Quando a etapa declara (EtapaProcesso.FaseCodigo), o
    // vínculo é quem responde por onde ela vive, e qualquer fase pode subdividir-se —
    // não só a que o cadastro marcava como agrupadora.
    internal static bool HaFaseDeAvaliacaoSemEtapa(IEnumerable<FaseCronograma> fases, IReadOnlyCollection<EtapaProcesso> etapas) =>
        fases.Any(f => f.AgrupaEtapas
            && !etapas.Any(e => string.Equals(e.FaseCodigo, f.Codigo, StringComparison.Ordinal))
            && !etapas.Any(static e => e.FaseCodigo is null));

    internal static DomainError AvaliacaoSemEtapa() => new(
        "ProcessoSeletivo.AvaliacaoSemEtapa",
        "Há uma fase que agrupa etapas no cronograma, mas nenhuma etapa acontece nela.");

    // A etapa segue a unidade da fase que a contém, então toda etapa publicada declara a fase em que
    // acontece; o rascunho a aceita sem fase. A etapa sem fase é recusada aqui, citando a etapa.
    private static DomainError? RecusaDaEtapaSemFaseDeclarada(EstadoDaConfiguracao estado) =>
        EtapaSemFaseDeclarada(estado) is { } etapa
            ? new DomainError(
                "ProcessoSeletivo.EtapaSemFaseDeclarada",
                $"A etapa '{etapa.Nome}' não declara a fase em que acontece — declare a fase antes de publicar ou retificar o processo.")
            : null;

    /// <summary>A primeira etapa, na ordem, que não declara a fase em que acontece.</summary>
    internal static EtapaProcesso? EtapaSemFaseDeclarada(EstadoDaConfiguracao estado) =>
        estado.Etapas
            .OrderBy(static e => e.Ordem ?? int.MaxValue)
            .ThenBy(static e => e.Nome, StringComparer.Ordinal)
            .FirstOrDefault(static e => e.FaseCodigo is null);

    /// <summary>Origem InscricaoPropria sem nenhuma fase que colete inscrição (§3.4).</summary>
    internal static bool HaInscricaoPropriaSemFaseDeColeta(EstadoDaConfiguracao estado) =>
        estado.OrigemCandidatos == OrigemCandidatos.InscricaoPropria && !estado.CronogramaFases.Any(static f => f.ColetaInscricao);

    /// <summary>
    /// A projeção da janela para o checklist. Sem zona resolvida o item fica verde: quem recusa a
    /// publicação nesse caso é <c>fuso_institucional_nao_reconhecido</c>, que já está vermelho pelo
    /// mesmo motivo — a bicondicional continua de pé, com a causa nomeada uma vez só.
    /// </summary>
    internal static bool JanelaDeIsencaoConforme(EstadoDaConfiguracao estado, ContextoDeContagemDePrazos contexto) =>
        contexto.FusoInstitucional is null
        || PendenciaDaJanelaDeIsencao(estado, contexto.FusoInstitucional) is null;

    /// <summary>
    /// As três regras de contorno da janela de isenção (UNI-REQ-0106): abre junto com a inscrição,
    /// fecha antes dela, e dura no mínimo cinco dias corridos.
    /// </summary>
    /// <remarks>
    /// A contagem é em dias corridos e não passa pelo calendário de dias úteis nem pelo algoritmo de
    /// contagem do processo — a janela é período do cronograma, não interposição de recurso. É o
    /// ponto mais fácil de errar por analogia com o prazo recursal, que segue outro contrato.
    /// </remarks>
    internal static DomainError? PendenciaDaJanelaDeIsencao(EstadoDaConfiguracao estado, TimeZoneInfo? fusoInstitucional)
    {
        if (estado.CronogramaFases.FirstOrDefault(static f => f.ColetaSolicitacaoIsencao) is not { } isencao)
        {
            return null;
        }

        if (isencao.Inicio is not { } inicioIsencao || isencao.Fim is not { } fimIsencao)
        {
            return new DomainError(
                "ProcessoSeletivo.JanelaDeIsencaoSemPrazo",
                $"A fase '{isencao.Codigo}' abre a solicitação de isenção e precisa de início e fim definidos.");
        }

        // Sem zona resolvida a duração não é verificável, e deixar passar transformaria a ausência
        // do fuso em permissão para publicar janela curta. A zona irresolvível já é recusada como
        // defeito de instalação antes daqui; chegar sem ela é o handler não tê-la passado.
        if (fusoInstitucional is not { } fuso)
        {
            throw new InvalidOperationException(
                "A duração da janela de isenção depende do fuso institucional, e ele não foi informado ao gate do cronograma.");
        }

        // A duração é independente da inscrição, e por isso vem antes: um certame de importação
        // externa não tem fase de coleta, e sem esta ordem publicaria janela de um dia sem que
        // nada acusasse.
        if (fimIsencao < UltimoInstanteDoQuintoDia(inicioIsencao, fuso))
        {
            return new DomainError(
                "ProcessoSeletivo.JanelaDeIsencaoMenorQueCincoDias",
                "A janela de solicitação de isenção precisa de ao menos cinco dias corridos, contados a partir do dia seguinte à abertura.");
        }

        // As duas regras seguintes posicionam a janela em relação às inscrições, e só existem
        // quando há inscrição a que se referir.
        if (ReferenciaTemporalDosFatos.FaseQueAncoraOPeriodoDeInscricao(estado) is not { Inicio: { } inicioInscricao, Fim: { } fimInscricao })
        {
            return null;
        }

        if (inicioIsencao != inicioInscricao)
        {
            return new DomainError(
                "ProcessoSeletivo.JanelaDeIsencaoNaoAbreComAInscricao",
                "A janela de solicitação de isenção abre no mesmo instante em que abrem as inscrições.");
        }

        if (fimIsencao >= fimInscricao)
        {
            return new DomainError(
                "ProcessoSeletivo.JanelaDeIsencaoNaoTerminaAntesDaInscricao",
                "A solicitação de isenção precisa terminar antes do fim das inscrições — quem tem o pedido indeferido ainda se inscreve pagando.");
        }

        return null;
    }

    /// <summary>
    /// O instante em que o quinto dia corrido se completa: o dia da abertura é excluído, o primeiro
    /// é o dia seguinte, e o quinto só termina no último instante do dia, no fuso institucional.
    /// </summary>
    /// <remarks>
    /// O piso é <c>23:59:59</c>, não o último tick do dia. A comparação é <c>&gt;=</c>, então uma
    /// janela gravada com fração de segundo continua aceita; exigir o tick final é que recusaria o
    /// caso comum, porque é em segundos que o operador declara a janela e que o envelope a congela.
    /// </remarks>
    internal static DateTimeOffset UltimoInstanteDoQuintoDia(DateTimeOffset abertura, TimeZoneInfo fuso)
    {
        DateTime fimDoQuintoDiaLocal = TimeZoneInfo.ConvertTime(abertura, fuso).Date.AddDays(6).AddSeconds(-1);

        return new DateTimeOffset(fimDoQuintoDiaLocal, fuso.GetUtcOffset(fimDoQuintoDiaLocal));
    }

    /// <summary>A fase que ancora o período de inscrição, quando existe e está sem janela.</summary>
    /// <remarks>
    /// Incide sobre a MESMA fase que <see cref="ReferenciaTemporalDosFatos.FaseQueAncoraOPeriodoDeInscricao"/> elege, e não
    /// sobre "alguma fase de coleta": fossem conjuntos diferentes, a projeção poderia tirar o
    /// período de uma fase enquanto a recusa olhasse outra.
    /// </remarks>
    internal static FaseCronograma? FaseQueColetaInscricaoSemJanela(EstadoDaConfiguracao estado) =>
        ReferenciaTemporalDosFatos.FaseQueAncoraOPeriodoDeInscricao(estado) is { } ancora && (ancora.Inicio is null || ancora.Fim is null)
            ? ancora
            : null;

    /// <summary>A primeira fase do cronograma, na ordem, sem unidade responsável.</summary>
    internal static FaseCronograma? FaseSemUnidadeResponsavel(EstadoDaConfiguracao estado) =>
        estado.CronogramaFases.OrderBy(static f => f.Ordem).FirstOrDefault(static f => f.UnidadeResponsavelOrigemId is null);

    /// <summary>Há vagas ofertadas (VoBase > 0), mas nenhuma fase do cronograma produz resultado (§3.4).</summary>
    internal static bool HaVagasSemFaseQueProduzResultado(EstadoDaConfiguracao estado) =>
        estado.DistribuicaoVagas.Any(static d => d.VoBase > 0) && !estado.CronogramaFases.Any(static f => f.ProduzResultado);
}
