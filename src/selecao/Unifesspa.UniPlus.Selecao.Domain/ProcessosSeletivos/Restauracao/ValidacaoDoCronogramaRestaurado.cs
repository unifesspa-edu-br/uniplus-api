namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O cronograma do grafo restaurado, com o vínculo das etapas a ele: a estrutura das fases e a fase que cada etapa declara,
/// que tem de existir. O grafo de precedências e a resolução de regra e de ato âncora não são reconferidos: são I/O, e o
/// congelamento dos parâmetros na publicação (UNI-REQ-0019) proíbe reinterpretar um passado publicado contra o catálogo
/// atual. A etapa sem fase e a fase que agrupa etapas sem etapa são completude, recusadas só ao congelar.
/// </summary>
internal static class ValidacaoDoCronogramaRestaurado
{
    internal static IReadOnlyList<FieldError> Validar(GrafoConfiguracao grafo)
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(grafo.CronogramaFases);

        return
        [
            .. cronograma.ConferirEstrutura(),
            .. FaseDeCadaEtapa(grafo, cronograma),
        ];
    }

    private static IReadOnlyList<FieldError> FaseDeCadaEtapa(GrafoConfiguracao grafo, CronogramaDoProcesso cronograma) =>
        ValidacaoDasEtapasRestauradas.HaEtapaSemId(grafo)
            ? []
            : EtapasNoCronograma.ConferirFaseDasEtapas(cronograma, EtapasDoProcesso.Das(grafo.Etapas));
}
