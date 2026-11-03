namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
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
    /// pergunta única de que o gate da convenção, o do calendário e a leitura do envelope
    /// congelado dependem, para que nenhum deles olhe só um dos dois lugares onde o recurso mora.
    /// </summary>
    /// <remarks>
    /// Basta existir regra de recurso: toda regra declara prazo de interposição, e as duas
    /// unidades declaráveis nele correm sobre dia útil — dias úteis por definição, e horas porque
    /// só as horas situadas em dia útil avançam o relógio. A suspensividade em dias úteis depende
    /// igualmente, mas nunca sozinha, já que não existe suspensividade sem a regra que a carrega.
    /// A janela de solicitação de isenção não é regra de recurso e não aciona nada disto.
    /// </remarks>
    public static bool ContagemDistingueDiaUtil(IEnumerable<FaseCronograma> fases, IEnumerable<EtapaProcesso> etapas)
    {
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(etapas);

        return fases.Any(static fase => fase.RegraRecurso is not null)
            || etapas.Any(static etapa => etapa.Recursos.Count > 0);
    }

    /// <summary>
    /// Depois da primeira publicação o que vale é a versão congelada: se a versão sobre a qual a
    /// sessão foi aberta já traz o identificador, ele não muda mais — o endereço da página e a
    /// chave do documento no acervo derivam dele, e o público já o conhece. Antes dela, ou quando
    /// a versão-base não o traz, o identificador é livre.
    /// </summary>
    /// <remarks>A unicidade entre processos depende de consulta e fica com o handler.</remarks>
    internal static DomainError? RecusaDeTrocaDoIdentificador(RascunhoRetificacao? sessao) =>
        sessao is { VersaoBaseComIdentificadorLegivel: true }
            ? new DomainError(
                ProcessoSeletivoErrorCodes.IdentificadorLegivelImutavel,
                "O identificador legível já consta em versão publicada e não pode ser alterado.")
            : null;

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

    internal static DomainError? PendenciaDoIdentificadorLegivel(EstadoDaConfiguracao estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        return estado.IdentificadorLegivel is null
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
    internal static DomainError? PendenciaDaLocalidade(EstadoDaConfiguracao estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        return estado.Localidade is null
            ? new DomainError(
                "ProcessoSeletivo.LocalidadeAusente",
                "A localidade que rege a contagem dos prazos é obrigatória para gerar versão publicada.")
            : null;
    }

    internal static DomainError? PendenciaDoAlgoritmoDeContagem(EstadoDaConfiguracao estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        if (estado.AlgoritmoContagemPrazo is not null)
        {
            return null;
        }

        return AlgumaContagemDistingueDiaUtil(estado)
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
    internal static DomainError? PendenciaDoCalendarioVigente(EstadoDaConfiguracao estado, ContextoDeContagemDePrazos contexto)
    {
        ArgumentNullException.ThrowIfNull(estado);

        if (contexto.CalendarioVigente is not null || !AlgumaContagemDistingueDiaUtil(estado))
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

    private static bool AlgumaContagemDistingueDiaUtil(EstadoDaConfiguracao estado) =>
        ContagemDistingueDiaUtil(estado.CronogramaFases, estado.Etapas);
}
