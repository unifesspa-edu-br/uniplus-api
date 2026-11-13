namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

// Checagens estruturais equivalentes às de DefinirCronogramaFases, sobre o grafo e não sobre o
// agregado corrente. O grafo de precedências e a resolução de regra e de ato âncora não são
// reconferidos: são I/O, e o congelamento dos parâmetros na publicação (UNI-REQ-0019) proíbe
// reinterpretar um passado publicado contra o catálogo atual.
internal static class ValidacaoDoCronogramaRestaurado
{
    internal static DomainError? Recusa(GrafoConfiguracao grafo) =>
        RegrasDoCronograma.RecusaDaColecao(grafo.CronogramaFases)
        ?? FaseAgrupadoraSemEtapa(grafo)
        ?? EtapaSemFaseDeAvaliacao(grafo);

    // A bicondicional fase×etapa é lida pelo vínculo, como na publicação: a etapa que declara a fase
    // em que acontece responde por si, e qualquer fase pode subdividir-se, não só a que o cadastro
    // marca como agrupadora. A regra global antiga recusava a restauração de um envelope que a
    // publicação aceita, e o descarte da retificação ficava impossível.
    private static DomainError? FaseAgrupadoraSemEtapa(GrafoConfiguracao grafo) =>
        PendenciasDoCronograma.HaFaseDeAvaliacaoSemEtapa(grafo.CronogramaFases, grafo.Etapas)
            ? PendenciasDoCronograma.AvaliacaoSemEtapa()
            : null;

    private static DomainError? EtapaSemFaseDeAvaliacao(GrafoConfiguracao grafo) =>
        grafo.Etapas.Any(static e => e.FaseCodigo is null) && !grafo.CronogramaFases.Any(static f => f.AgrupaEtapas)
            ? new DomainError(
                "ProcessoSeletivo.EtapaSemFaseDeAvaliacao",
                "Há etapa pontuada no grafo restaurado sem fase declarada, e nenhuma fase agrupa etapas.")
            : null;
}
