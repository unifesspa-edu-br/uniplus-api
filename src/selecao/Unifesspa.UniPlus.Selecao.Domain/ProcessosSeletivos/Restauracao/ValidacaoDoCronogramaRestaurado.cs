namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O cronograma do grafo restaurado, com o vínculo das etapas a ele, pelas mesmas regras da gravação. O grafo de
/// precedências e a resolução de regra e de ato âncora não são reconferidos: são I/O, e o congelamento dos parâmetros na
/// publicação (UNI-REQ-0019) proíbe reinterpretar um passado publicado contra o catálogo atual.
/// </summary>
/// <remarks>
/// A bicondicional entre fase e etapa é lida pelo vínculo, como na publicação: qualquer fase pode subdividir-se, não só a
/// que o cadastro marca como agrupadora. Aplicar a regra global antiga recusava a restauração de um envelope que a
/// publicação aceita.
/// </remarks>
internal static class ValidacaoDoCronogramaRestaurado
{
    internal static DomainError? Validar(GrafoConfiguracao grafo)
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(grafo.CronogramaFases);

        return CausasDaRecusa.Primeira(cronograma.ConferirEstrutura())
            ?? CausasDaRecusa.Primeira(EtapasNoCronograma.ConferirFasesQueAgrupamEtapas(cronograma, EtapasDoProcesso.Das(grafo.Etapas)))
            ?? EtapaSemFaseEmCronogramaSemFaseAgrupadora(grafo);
    }

    private static DomainError? EtapaSemFaseEmCronogramaSemFaseAgrupadora(GrafoConfiguracao grafo) =>
        grafo.Etapas.Any(static etapa => etapa.FaseCodigo is null) && !grafo.CronogramaFases.Any(static fase => fase.AgrupaEtapas)
            ? new DomainError(
                "ProcessoSeletivo.EtapaSemFaseDeAvaliacao",
                "Há etapa pontuada no grafo restaurado sem fase declarada, e nenhuma fase agrupa etapas.")
            : null;
}
