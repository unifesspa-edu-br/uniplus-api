namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

public static class PendenciasDoCronograma
{
    /// <summary>
    /// A primeira pendência do cronograma que impede a publicação, ou <see langword="null"/>: cada uma tem o seu próprio
    /// <c>DomainError</c> nomeado, e o checklist de conformidade projeta cada regra no item que lhe cabe. A ordem em que as
    /// regras orientam é a de <see cref="PendenciasDaPublicacao"/> e depois a de <see cref="PendenciasDoEstadoCarregado"/>.
    /// </summary>
    /// <remarks>
    /// Público porque o handler a antecipa antes de canonicalizar: sem isso, um processo com fase de coleta sem janela e
    /// referência temporal irresolvível receberia pelo endpoint a recusa da referência, diagnóstico obscuro para quem só
    /// precisa datar a inscrição.
    /// </remarks>
    public static DomainError? PendenciaDoCronograma(EstadoDaConfiguracao estado, TimeZoneInfo? fusoInstitucional)
    {
        ArgumentNullException.ThrowIfNull(estado);

        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(estado.CronogramaFases);
        EtapasDoProcesso etapas = EtapasDoProcesso.Das(estado.Etapas);

        return PendenciasDaPublicacao(estado, cronograma, etapas, fusoInstitucional)
            .Concat(PendenciasDoEstadoCarregado(cronograma, etapas))
            .Select(static pendencia => pendencia())
            .FirstOrDefault(static causa => causa is not null);
    }

    /// <summary>Inscrição própria sem nenhuma fase que colete inscrição: o piso mínimo vem da origem dos candidatos, nunca do tipo do processo.</summary>
    internal static bool HaInscricaoPropriaSemFaseDeColeta(EstadoDaConfiguracao estado) =>
        estado.OrigemCandidatos == OrigemCandidatos.InscricaoPropria && !estado.CronogramaFases.Any(static f => f.ColetaInscricao);

    /// <summary>Há vagas ofertadas, mas nenhuma fase do cronograma produz resultado.</summary>
    internal static bool HaVagasSemFaseQueProduzResultado(EstadoDaConfiguracao estado) =>
        estado.DistribuicaoVagas.Any(static d => d.VagasOfertadasBase > 0) && !estado.CronogramaFases.Any(static f => f.ProduzResultado);

    /// <summary>
    /// O que o cronograma e as etapas declaram para a publicação: a fase que agrupa etapas tem etapa que aconteça nela, toda
    /// etapa declara uma fase do cronograma, a origem dos candidatos tem fase de coleta, as fases estão completas (janela da
    /// inscrição, unidade responsável e janela de isenção) e as vagas têm fase que produza resultado.
    /// </summary>
    private static Func<DomainError?>[] PendenciasDaPublicacao(
        EstadoDaConfiguracao estado, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, TimeZoneInfo? fuso) =>
    [
        () => CausasDaRecusa.Primeira(EtapasNoCronograma.ConferirFasesQueAgrupamEtapas(cronograma, etapas)),
        () => CausasDaRecusa.Primeira(etapas.ConferirCompletude()),
        () => CausasDaRecusa.Primeira(EtapasNoCronograma.ConferirFaseDasEtapas(cronograma, etapas)),
        () => HaInscricaoPropriaSemFaseDeColeta(estado) ? InscricaoPropriaSemFaseDeColeta() : null,
        () => CausasDaRecusa.Primeira(cronograma.ConferirCompletude(fuso)),
        () => HaVagasSemFaseQueProduzResultado(estado) ? VagasSemFaseQueProduzResultado() : null,
    ];

    /// <summary>
    /// O que a gravação já recusa, mas que o estado carregado do banco pode violar, porque o EF hidrata as linhas sem passar
    /// pela fábrica nem pela gravação: o ciclo recursal, a âncora do prazo de recurso da fase e da etapa, o recorte de
    /// competência das bancas e a janela da etapa dentro da janela da fase. Sem esta metade, um certame carregado nesse estado
    /// seria publicado com o ciclo em aberto, com um prazo de interposição que não resolve ou com uma prova marcada para fora
    /// da fase que a contém, e o envelope congelaria a incoerência.
    /// </summary>
    private static Func<DomainError?>[] PendenciasDoEstadoCarregado(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
    [
        () => CausasDaRecusa.Primeira(CicloRecursalDoProcesso.Conferir(cronograma, etapas)),
        () => CausasDaRecusa.Primeira(cronograma.RecusasDasDeclaracoes(AncoraDoRecursoDaFase.Codigos)),
        () => CausasDaRecusa.Primeira(etapas.RecusasDasDeclaracoes(JanelasRecursaisDaEtapa.CodigosDaAncora)),
        () => CausasDaRecusa.Primeira(cronograma.RecusasDasDeclaracoes(RecorteDeCompetenciaDasBancas.Codigos)),
        () => CausasDaRecusa.Primeira(EtapasNoCronograma.ConferirJanelaDasEtapas(cronograma, etapas)),
    ];

    private static DomainError InscricaoPropriaSemFaseDeColeta() => new(
        "ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta",
        "A origem dos candidatos é inscrição própria, e nenhuma fase do cronograma coleta inscrição.");

    private static DomainError VagasSemFaseQueProduzResultado() => new(
        "ProcessoSeletivo.VagasSemFaseQueProduzResultado",
        "Há vagas ofertadas, e nenhuma fase do cronograma produz resultado.");
}
