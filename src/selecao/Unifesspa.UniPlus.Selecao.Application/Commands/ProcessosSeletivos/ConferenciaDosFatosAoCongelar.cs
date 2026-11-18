namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

internal static class ConferenciaDosFatosAoCongelar
{
    /// <summary>
    /// Reconfere os fatos contra o catálogo vivo e congela o metadado dos que as condições citam.
    /// O catálogo pode reclassificar a origem de um fato depois que ele virou fato coletado, e um
    /// vínculo morto congelado numa versão append-only seria irreparável. Os fatos dos gatilhos
    /// continuam no processo e conhecidos até a fase de cada exigência, porque itens, formulários e
    /// derivações mudam depois que a exigência os citou.
    /// </summary>
    /// <remarks>Pressupõe os fatos coletados coletáveis: quem chama confere a coletabilidade antes.</remarks>
    /// <returns>O metadado congelado dos fatos citados em condição.</returns>
    internal static Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?> Conferir(
        ProcessoSeletivo processo, IReadOnlyDictionary<string, FatoCandidatoView> catalogoPorCodigo, FatosDasVersoesPublicadas? publicadas)
    {
        Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?> metadados =
            ResolvedorMetadadosFatosCongelados.Resolver(processo, catalogoPorCodigo);
        DomainError? incoerente = metadados.IsFailure
            ? null
            : ConferenciaDosFatosDosGatilhos.Conferir(processo, catalogoPorCodigo, publicadas) ?? ConferenciaDoFatoDeElegibilidade.Conferir(processo, catalogoPorCodigo);

        return incoerente is null ? metadados : Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?>.Failure(incoerente);
    }
}
