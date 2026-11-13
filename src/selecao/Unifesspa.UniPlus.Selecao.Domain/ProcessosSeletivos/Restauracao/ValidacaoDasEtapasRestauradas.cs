namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ValidacaoDasEtapasRestauradas
{
    // Lista de etapas vazia é estado válido (processo sem prova, como o SiSU). As recusas da coleção
    // são as mesmas da edição das etapas, na mesma posição: sem elas, um envelope restaurado
    // reintroduziria o estado que a gravação recusa.
    internal static DomainError? Recusa(GrafoConfiguracao grafo) =>
        IdAusente(grafo)
        ?? IdDuplicado(grafo)
        ?? RegrasDasEtapas.RecusaDaColecao(grafo.Etapas, grafo.Classificacao);

    private static DomainError? IdAusente(GrafoConfiguracao grafo) =>
        grafo.Etapas.Any(static e => e.Id == Guid.Empty)
            ? new DomainError(
                "ProcessoSeletivo.IdEtapaAusente",
                "Toda etapa restaurada deve declarar o Id congelado no envelope.")
            : null;

    // O Id vem congelado do envelope e nada além do handler de gravação garantia a unicidade: um
    // envelope com dois ids iguais produziria duas etapas que o etapa_ref não consegue distinguir,
    // e o INSERT colidiria na chave primária.
    private static DomainError? IdDuplicado(GrafoConfiguracao grafo) =>
        grafo.Etapas.Select(static e => e.Id).Distinct().Count() != grafo.Etapas.Count
            ? new DomainError(
                "ProcessoSeletivo.IdEtapaDuplicado",
                "O mesmo Id de etapa não pode aparecer mais de uma vez na configuração restaurada.")
            : null;
}
