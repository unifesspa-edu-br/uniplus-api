namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using Unifesspa.UniPlus.Configuracao.Application.Commands.Predicados;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A regra de aplicabilidade como chega no comando e o catálogo contra o qual ela é conferida. A
/// regra ausente segue nula para o agregado recusar; a lista vazia é a regra incondicional.
/// </summary>
internal static class RegraDeAplicabilidadeDaEntrada
{
    private const string Campo = "regraDeAplicabilidade";

    public static Result<PredicadoDnf?> Montar(IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoInput>>? entrada)
    {
        if (entrada is null)
        {
            return Result<PredicadoDnf?>.Success(null);
        }

        Result<PredicadoDnf> predicado = MontagemDoPredicado.Montar(entrada, new DomainError(
            ModalidadeErrorCodes.RegraDeAplicabilidadeMalformada, "Uma condição da regra de aplicabilidade veio nula."));
        return predicado.IsSuccess
            ? Result<PredicadoDnf?>.Success(predicado.Value)
            : Result<PredicadoDnf?>.ValidationFailure([new FieldError(Campo, predicado.Error!)]);
    }

    /// <summary>
    /// A regra malformada segue nula para o agregado conferir os demais campos (ADR-0125), e ele a
    /// recusaria como ausente: no campo da regra, vale a recusa da montagem.
    /// </summary>
    public static List<FieldError> ComAsRecusasDaMontagem(Result<PredicadoDnf?> montagem, IEnumerable<FieldError> recusasDoAgregado) =>
        montagem.IsSuccess
            ? [.. recusasDoAgregado]
            : [.. recusasDoAgregado.Where(static e => e.Field != Campo), .. montagem.Errors];

    public static async Task<CatalogoDeFatos> CarregarCatalogoAsync(
        IFatoCandidatoRepository fatoRepository,
        IPrecedenciaFaseRepository precedenciaRepository,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoCandidato> fatos = await fatoRepository.ListarTodosAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<PrecedenciaFase> precedencias = await precedenciaRepository.ListarVivasAsync(cancellationToken).ConfigureAwait(false);
        return new CatalogoDeFatos(fatos, precedencias);
    }
}
