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
    private const string Campo = "etapas";

    internal static IReadOnlyList<FieldError> Validar(GrafoConfiguracao grafo) =>
        HaEtapaSemId(grafo)
            ? CausasDaRecusa.Da(IdAusente(), Campo)
            :
            [
                .. EtapasDoProcesso.Das(grafo.Etapas).Conferir(),
                .. CausasDaRecusa.Da(RegrasDasEtapas.EtapaNotaEnemSemClassificacaoEnem(grafo.Etapas, grafo.Classificacao), Campo),
            ];

    // O Id vem congelado do envelope e nada além do handler de gravação garantia a unicidade: sem Id, a declaração da etapa
    // nem se monta, e dois ids iguais colidiriam na chave primária. Sem Id, as demais regras das etapas não têm o que julgar.
    internal static bool HaEtapaSemId(GrafoConfiguracao grafo) => grafo.Etapas.Any(static etapa => etapa.Id == Guid.Empty);

    private static DomainError IdAusente() => new(
        "ProcessoSeletivo.IdEtapaAusente",
        "Toda etapa restaurada deve declarar o Id congelado no envelope.");
}
