namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

internal static class ReconferenciaDasCopiasEUnidades
{
    /// <summary>
    /// O catálogo é editável depois que o item copiou dele a origem dos valores, o formato e a
    /// proteção, e os gates avaliam a cópia: ela é reconferida e recopiada antes deles. A unidade
    /// responsável de cada fase é conferida no cadastro vivo e recopiada no mesmo passo, porque o
    /// congelamento é da publicação.
    /// </summary>
    /// <param name="fatosJaColetados">Os fatos que alguma versão publicada já coletou; nulo na publicação inicial.</param>
    internal static async Task<Result> AplicarAsync(
        ProcessoSeletivo processo,
        IReadOnlyList<FatoCandidatoView> catalogoDeFatos,
        IReadOnlySet<string>? fatosJaColetados,
        IResolvedorDaUnidadeResponsavel unidadeResponsavelResolvedor,
        CancellationToken cancellationToken) =>
        ReconferenciaDasUnidadesDasFases.Combinar(
            ReconferenciaDasCopiasDoItem.Aplicar(processo, catalogoDeFatos, fatosJaColetados),
            await ReconferenciaDasUnidadesDasFases.AplicarAsync(processo, unidadeResponsavelResolvedor, cancellationToken).ConfigureAwait(false));
}
