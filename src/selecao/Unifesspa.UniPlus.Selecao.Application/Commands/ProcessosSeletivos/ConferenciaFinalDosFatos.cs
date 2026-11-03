namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

internal static class ConferenciaFinalDosFatos
{
    /// <summary>
    /// Reconfere os fatos contra o catálogo vivo e congela o metadado dos que as condições citam.
    /// O catálogo pode reclassificar a origem de um fato depois que ele virou fato coletado, e um
    /// vínculo morto congelado numa versão append-only seria irreparável. Os fatos dos gatilhos
    /// continuam no processo e conhecidos até a fase de cada exigência, porque itens, formulários e
    /// derivações mudam depois que a exigência os citou.
    /// </summary>
    /// <returns>O metadado congelado dos fatos citados em condição.</returns>
    internal static Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?> Conferir(
        ProcessoSeletivo processo, IReadOnlyDictionary<string, FatoCandidatoView> catalogoPorCodigo)
    {
        Result coletabilidade = ConferenciaDeColetabilidadeDeFatos.Conferir(processo, catalogoPorCodigo);
        if (coletabilidade.IsFailure)
        {
            return Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?>.Failure(coletabilidade.Error!);
        }

        Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?> metadados =
            ResolvedorMetadadosFatosCongelados.Resolver(processo, catalogoPorCodigo);
        if (metadados.IsFailure)
        {
            return metadados;
        }

        if (ConferenciaDosFatosDosGatilhos.Conferir(processo, catalogoPorCodigo) is { } gatilhoIncoerente)
        {
            return Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?>.Failure(gatilhoIncoerente);
        }

        return ConferenciaDoFatoDeElegibilidade.Conferir(processo, catalogoPorCodigo) is { } elegibilidadeIncoerente
            ? Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?>.Failure(elegibilidadeIncoerente)
            : metadados;
    }
}
