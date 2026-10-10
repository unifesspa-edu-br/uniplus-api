namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// No congelamento, o fato que decide quem recebe o acréscimo é conferido com o ponto de resolução
/// que o catálogo dá a cada fato: a fase de um derivado depende da das suas dependências
/// (ADR-0142). O checklist faz a mesma conferência sem o catálogo.
/// </summary>
internal static class ConferenciaDoFatoDeElegibilidade
{
    public static DomainError? Conferir(
        ProcessoSeletivo processo, IReadOnlyDictionary<string, FatoCandidatoView> catalogo)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(catalogo);

        return processo.RecusaDoFatoDeElegibilidade(
            VocabularioDeFatos.PontoResolucaoPorFato(catalogo.Values),
            VocabularioDeFatos.MembroPorAgregado(catalogo.Values));
    }
}
