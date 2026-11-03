namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class PendenciasDoCronograma
{
    /// <summary>
    /// Pendências do cronograma que não cabem no checklist booleano ORIGINAL de
    /// <see cref="PendenciaDeConformidade"/> — cada uma tem o seu próprio <c>DomainError</c>
    /// nomeado (Story #851 §3.4/§3.5, CA-11/CA-13/CA-14). Chamado por
    /// <see cref="Publicar"/> e por <see cref="SucederVersao"/> (Retificar/FecharRetificacao),
    /// sempre <b>depois</b> de <see cref="PendenciaDeConformidade"/>.
    /// </summary>
    /// <remarks>
    /// Issue #1092: cada <c>if</c> abaixo testa um predicado privado nomeado
    /// (<c>Ha*</c>) em vez de uma expressão inline — <see cref="AvaliarConformidade"/>
    /// chama os MESMOS quatro predicados para projetar um item por razão. A precedência
    /// (qual falha é devolvida primeiro) continua decidida só aqui, pela ORDEM dos
    /// <c>if</c>; o checklist não reordena nem filtra — só projeta todos os quatro.
    /// <para>
    /// Público porque o handler o antecipa antes de canonicalizar (issue #1350): sem isso, um
    /// processo com fase de coleta sem janela E referência temporal irresolvível receberia pelo
    /// endpoint a recusa da referência, diagnóstico obscuro para quem só precisa datar a inscrição.
    /// </para>
    /// </remarks>
    public static DomainError? PendenciaDoCronograma(EstadoDaConfiguracao estado, TimeZoneInfo? fusoInstitucional)
    {
        ArgumentNullException.ThrowIfNull(estado);

        // §3.5, direção "fase de avaliação sem etapa". Desde que a etapa passou a declarar a
        // própria fase, a gravação do cronograma não recusa mais esse estado na hora — ele é
        // legítimo enquanto se monta o certame, e quem o barra é este gate, na publicação.
        if (HaFaseDeAvaliacaoSemEtapa(estado))
        {
            return new DomainError(
                "ProcessoSeletivo.AvaliacaoSemEtapa",
                "Há uma fase que agrupa etapas no cronograma, mas o processo não tem nenhuma etapa pontuada.");
        }

        // §3.5, direção "etapa sem fase de avaliação" — lazy por natureza (a etapa pode
        // ser declarada depois do cronograma).
        if (HaEtapaSemFaseDeAvaliacao(estado))
        {
            return new DomainError(
                "ProcessoSeletivo.EtapaSemFaseDeAvaliacao",
                "O processo tem etapa pontuada, mas nenhuma fase do cronograma agrupa etapas.");
        }

        // §3.4 — piso mínimo derivado da ORIGEM DOS CANDIDATOS, nunca do tipo.
        if (HaInscricaoPropriaSemFaseDeColeta(estado))
        {
            return new DomainError(
                "ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta",
                "A origem dos candidatos é inscrição própria, e nenhuma fase do cronograma coleta inscrição.");
        }

        // Issue #1350: a fase que coleta inscrição declara o prazo que o Edital publica, então
        // ela precisa de janela mesmo quando a origem da data é NA_EXECUCAO. É a única exceção à
        // §3.2 — nas demais fases, "sem data" continua sendo estado válido, porque a data não é
        // fixada no edital e a unidade responsável a define na execução. Um Edital que admite inscrição sem
        // prazo definido, esse não se sustenta.
        if (FaseQueColetaInscricaoSemJanela(estado) is { } faseSemJanela)
        {
            return new DomainError(
                "ProcessoSeletivo.FaseQueColetaInscricaoSemJanela",
                $"A fase '{faseSemJanela.Codigo}' coleta inscrição e precisa de início e fim definidos para que o Edital declare o período.");
        }

        if (FaseSemUnidadeResponsavel(estado) is { } faseSemUnidade)
        {
            return new DomainError(
                "ProcessoSeletivo.FaseSemUnidadeResponsavel",
                $"A fase '{faseSemUnidade.Codigo}' não tem unidade responsável definida — toda fase publicada responde a uma unidade da Unifesspa.");
        }

        if (PendenciaDaJanelaDeIsencao(estado, fusoInstitucional) is { } pendenciaIsencao)
        {
            return pendenciaIsencao;
        }

        // §3.4 — havendo vagas ofertadas, o cronograma precisa de ao menos uma fase que
        // produza resultado.
        if (HaVagasSemFaseQueProduzResultado(estado))
        {
            return new DomainError(
                "ProcessoSeletivo.VagasSemFaseQueProduzResultado",
                "Há vagas ofertadas, e nenhuma fase do cronograma produz resultado.");
        }

        // A conclusão do ciclo recursal por matéria. DefinirCronogramaFases já a recusa na
        // escrita, mas o estado violado é materializável pela hidratação do EF, que não
        // passa pela fábrica — mesmo raciocínio que já sustenta o item da taxa de inscrição
        // sem fundamento de isenção. Sem esta metade, um certame carregado do banco nesse
        // estado seria publicado com o ciclo recursal em aberto.
        if (CicloRecursalDoCronograma.PendenciaDaConclusaoDoCicloRecursal(estado) is { } pendenciaDaConclusao)
        {
            return pendenciaDaConclusao;
        }

        // A âncora do prazo de recurso, pela mesma razão: FaseCronograma.Criar a recusa na
        // escrita, e o EF hidrata regras_recurso_fase e produtos_da_fase direto das linhas,
        // sem passar pela fábrica. Sem esta metade, um certame carregado do banco com a
        // âncora apontando para produto que não é preliminar seu congelaria uma versão cujo
        // prazo de interposição não resolve — e a incoerência só apareceria na restauração,
        // longe da causa.
        if (CicloRecursalDoCronograma.PendenciaDaAncoraDoRecurso(estado) is { } pendenciaDaAncora)
        {
            return pendenciaDaAncora;
        }

        // A mesma conferência para a janela de cada etapa, pela mesma razão: o EF hidrata
        // recursos_da_etapa e produtos_da_etapa direto das linhas, sem passar pela gravação das
        // janelas. A etapa com a janela ancorada fora dos preliminares dela não resolveria o
        // prazo de interposição.
        if (CicloRecursalDoCronograma.PendenciaDaAncoraDoRecursoDasEtapas(estado) is { } pendenciaDaAncoraDaEtapa)
        {
            return pendenciaDaAncoraDaEtapa;
        }

        // O recorte de competência das bancas, pela mesma razão: FaseCronograma.Criar o
        // recusa na escrita, e o EF hidrata bancas_requeridas e categorias_julgadas direto
        // das linhas, sem passar pela fábrica. Sem esta metade, um certame carregado do
        // banco com duas bancas do mesmo tipo indistinguíveis seria publicado sem dizer
        // quem responde por qual matéria — e o defeito só apareceria no primeiro parecer ou
        // no primeiro recurso, longe da causa.
        if (CicloRecursalDoCronograma.PendenciaDoRecorteDeCompetencia(estado) is { } pendenciaDoRecorte)
        {
            return pendenciaDoRecorte;
        }

        // A etapa dentro da janela da fase que a abriga, pela mesma razão: as duas gravações
        // a recusam na escrita, e o EF hidrata etapas_processo e fases_cronograma direto das
        // linhas, sem passar por nenhuma das duas. Sem esta metade, um certame gravado antes
        // de a invariante existir seria publicado com uma prova marcada para fora da fase que
        // a contém — e o envelope congelaria a incoerência no que vai ao Diário Oficial.
        if (JanelaDaEtapaNaFase.PendenciaDaJanelaDaEtapaNaFase(estado) is { } pendenciaDaJanelaDaEtapa)
        {
            return pendenciaDaJanelaDaEtapa;
        }

        return null;
    }

    /// <summary>Fase que agrupa etapas existe, mas o processo não tem etapa pontuada (§3.5).</summary>
    // §3.5 — a bicondicional por sinalizador sobrevive apenas para a etapa que NÃO
    // declara a própria fase. Quando a etapa declara (EtapaProcesso.FaseCodigo), o
    // vínculo é quem responde por onde ela vive, e qualquer fase pode subdividir-se —
    // não só a que o cadastro marcava como agrupadora.
    internal static bool HaFaseDeAvaliacaoSemEtapa(EstadoDaConfiguracao estado) =>
        estado.CronogramaFases.Any(f => f.AgrupaEtapas
            && !estado.Etapas.Any(e => string.Equals(e.FaseCodigo, f.Codigo, StringComparison.Ordinal))
            && !estado.Etapas.Any(static e => e.FaseCodigo is null));

    internal static bool HaEtapaSemFaseDeAvaliacao(EstadoDaConfiguracao estado) =>
        estado.Etapas.Any(static e => e.FaseCodigo is null)
        && !estado.CronogramaFases.Any(static f => f.AgrupaEtapas);

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

    /// <summary>A fase que ancora o período de inscrição, quando existe e está sem janela (issue #1350).</summary>
    /// <remarks>
    /// Incide sobre a MESMA fase que <see cref="FaseQueAncoraOPeriodoDeInscricao"/> elege, e não
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
