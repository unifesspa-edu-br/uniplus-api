namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As etapas do grafo restaurado, pelas mesmas regras da gravação do conjunto. A lista vazia é estado válido: o
/// processo sem prova, como o SiSU, não tem etapa.
/// </summary>
internal static class ValidacaoDasEtapasRestauradas
{
    internal static DomainError? Validar(GrafoConfiguracao grafo) =>
        IdAusente(grafo)
        ?? CausasDaRecusa.Primeira(EtapasDoProcesso.Das(grafo.Etapas).Conferir())
        ?? RegrasDasEtapas.EtapaNotaEnemSemClassificacaoEnem(grafo.Etapas, grafo.Classificacao);

    // O Id vem congelado do envelope e nada além do handler de gravação garantia a unicidade: sem Id, a declaração
    // da etapa nem se monta, e dois ids iguais colidiriam na chave primária.
    private static DomainError? IdAusente(GrafoConfiguracao grafo) =>
        grafo.Etapas.Any(static etapa => etapa.Id == Guid.Empty)
            ? new DomainError(
                "ProcessoSeletivo.IdEtapaAusente",
                "Toda etapa restaurada deve declarar o Id congelado no envelope.")
            : null;
}
