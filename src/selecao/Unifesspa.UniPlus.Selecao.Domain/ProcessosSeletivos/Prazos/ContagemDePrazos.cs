namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As regras que cercam a contagem dos prazos do certame (UNI-REQ-0111, UNI-REQ-0112 e
/// UNI-REQ-0116): o identificador legível, o município cujo calendário rege a contagem e a
/// convenção de contagem. As declarações se recusam na escrita; as pendências recusam a geração
/// de versão.
/// </summary>
public static class ContagemDePrazos
{
    /// <summary>
    /// Se alguma janela de recurso, da fase ou da etapa, conta prazo que distingue dia útil. É a
    /// pergunta única de que a conferência da convenção, a do calendário e a leitura do registro
    /// da versão publicada dependem, para que nenhuma delas olhe só um dos dois lugares onde o recurso mora.
    /// </summary>
    /// <remarks>
    /// Basta existir regra de recurso: toda regra declara prazo de interposição, e as duas
    /// unidades declaráveis nele correm sobre dia útil — dias úteis por definição, e horas porque
    /// só as horas situadas em dia útil avançam o relógio. O prazo da 2ª instância usa as mesmas
    /// unidades, mas nunca sozinho, já que não existe prazo da 2ª instância sem a regra que o carrega.
    /// A janela de solicitação de isenção não é regra de recurso e não aciona nada disto.
    /// </remarks>
    public static bool ContagemDistingueDiaUtil(IEnumerable<FaseCronograma> fases, IEnumerable<EtapaProcesso> etapas)
    {
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(etapas);

        return fases.Any(static fase => fase.RegraRecurso is not null)
            || etapas.Any(static etapa => etapa.Recursos.Count > 0);
    }

    internal static DomainError? RecusaDaLocalidadeAusente(LocalidadeRegente? localidade) =>
        localidade is null
            ? new DomainError(
                "ProcessoSeletivo.LocalidadeAusente",
                "A localidade que rege a contagem dos prazos é obrigatória.")
            : null;

    internal static DomainError? RecusaDaConvencaoAusente(ReferenciaRegra? algoritmo) =>
        algoritmo is null
            ? new DomainError(
                "ProcessoSeletivo.AlgoritmoContagemPrazoNaoDeclarado",
                "A convenção de contagem dos prazos é obrigatória.")
            : null;

    internal static DomainError? PendenciaDoIdentificadorLegivel(ConfiguracaoDeclarada configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return configuracao.Identidade.IdentificadorLegivel is null && !IdentificadorLegivelRecusado(configuracao)
            ? new DomainError(
                ProcessoSeletivoErrorCodes.IdentificadorLegivelAusente,
                "O identificador legível é obrigatório para gerar versão publicada.")
            : null;
    }

    /// <summary>
    /// A localidade é exigida desde a criação, então chegar aqui sem ela não é estado alcançável
    /// pelo fluxo público: a recusa defende contra caminho de escrita que a contorne. É também a
    /// fonte única do item que o checklist de conformidade projeta, para que checklist e
    /// publicação não divirjam.
    /// </summary>
    internal static DomainError? PendenciaDaLocalidade(ConfiguracaoDeclarada configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return configuracao.Cronograma.Localidade is null && !LocalidadeRecusada(configuracao)
            ? new DomainError(
                "ProcessoSeletivo.LocalidadeAusente",
                "A localidade que rege a contagem dos prazos é obrigatória para gerar versão publicada.")
            : null;
    }

    internal static DomainError? PendenciaDoAlgoritmoDeContagem(ConfiguracaoDeclarada configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        if (configuracao.Cronograma.AlgoritmoContagemPrazo is not null || AlgoritmoRecusado(configuracao))
        {
            return null;
        }

        return AlgumaContagemDistingueDiaUtil(configuracao)
            ? new DomainError(
                "ProcessoSeletivo.AlgoritmoContagemPrazoNaoDeclarado",
                "O processo tem prazo de recurso cuja contagem distingue dia útil, e não declarou a convenção de contagem que usa.")
            : null;
    }

    /// <summary>
    /// Mesmo gatilho da convenção de contagem, com outra origem do dado: a convenção o processo
    /// declara, o calendário vem do módulo Configuração e chega pelo contexto. As duas causas são
    /// distintas e cada uma tem erro próprio, porque reportar uma pela outra mandaria quem
    /// publica corrigir o lugar errado.
    /// </summary>
    internal static DomainError? PendenciaDoCalendarioVigente(ConfiguracaoDeclarada configuracao, ContextoDeContagemDePrazos contexto)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        if (contexto.CalendarioVigente is not null || !AlgumaContagemDistingueDiaUtil(configuracao))
        {
            return null;
        }

        // Dataset vigente que veio incoerente do cadastro: a causa é essa, não a ausência.
        // Reportar "não há calendário" mandaria cadastrar um que já está lá.
        return contexto.FalhaDoCalendarioVigente
            ?? new DomainError(
                "ProcessoSeletivo.CalendarioVigenteAusente",
                "O processo tem prazo de recurso cuja contagem distingue dia útil, e não há calendário de dias úteis vigente cadastrado.");
    }

    /// <summary>Se a leitura do documento recusou o identificador legível: a causa é a da leitura, e o item do painel fica pendente.</summary>
    internal static bool IdentificadorLegivelRecusado(ConfiguracaoDeclarada configuracao) =>
        configuracao.Origem.TemParteRecusadaEm(IdentidadeDoProcessoDeclarada.CampoDoIdentificadorLegivel);

    /// <summary>Se a leitura do documento recusou o município da localidade: a causa é a da leitura, e o item do painel fica pendente.</summary>
    internal static bool LocalidadeRecusada(ConfiguracaoDeclarada configuracao) =>
        configuracao.Origem.TemParteRecusadaEm(IdentidadeDoProcessoDeclarada.CampoDaLocalidade);

    /// <summary>Se a leitura do documento recusou a regra citada como convenção de contagem: a causa é a da leitura, e o item do painel fica pendente.</summary>
    internal static bool AlgoritmoRecusado(ConfiguracaoDeclarada configuracao) =>
        configuracao.Origem.TemParteRecusadaEm(IdentidadeDoProcessoDeclarada.CampoDaConvencaoDeContagem);

    /// <summary>
    /// Se a contagem que distingue dia útil não se pode afirmar nem negar: nenhuma regra de recurso conhecida a distingue, e a leitura
    /// recusou uma fase, ou a regra de recurso de uma fase, que podia distingui-la. A convenção e o calendário ficam sem julgar, e os
    /// itens do painel, pendentes em vez de aprovados por omissão.
    /// </summary>
    internal static bool ContagemIndeterminadaPelaLeitura(ConfiguracaoDeclarada configuracao) =>
        !AlgumaContagemDistingueDiaUtil(configuracao) && RegraDeRecursoRecusada(configuracao);

    private static bool RegraDeRecursoRecusada(ConfiguracaoDeclarada configuracao) =>
        configuracao.Origem.TemParteRecusadaEm(CronogramaDoProcesso.ListaDasFases)
        || Enumerable.Range(0, configuracao.Cronograma.CronogramaFases.Count)
            .Any(posicao => configuracao.Origem.TemParteRecusadaEm($"fases[{posicao}].{FaseDeclarada.CampoDaRegraDeRecurso}"));

    private static bool AlgumaContagemDistingueDiaUtil(ConfiguracaoDeclarada configuracao) =>
        ContagemDistingueDiaUtil(configuracao.Cronograma.CronogramaFases, configuracao.Avaliacao.Etapas);
}
