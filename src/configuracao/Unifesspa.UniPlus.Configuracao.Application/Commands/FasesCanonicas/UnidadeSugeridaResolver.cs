namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Resolve a unidade sugerida de uma fase canônica no cadastro de unidades (ADR-0056) e a
/// devolve como snapshot-copy (ADR-0061). Sem id, não há sugestão; id que o cadastro não
/// reconhece como viva é recusado. Na edição, reenviar a unidade já gravada preserva a cópia
/// congelada: a unidade pode ter sido desativada depois, e isso não impede editar os outros
/// campos. Só a troca é conferida no cadastro.
/// </summary>
internal static class UnidadeSugeridaResolver
{
    public static Task<Result<UnidadeSugerida?>> ResolverTrocaAsync(
        UnidadeSugerida? gravada,
        Guid? unidadeSugeridaId,
        IUnidadeReader unidadeReader,
        CancellationToken cancellationToken) =>
        unidadeSugeridaId == gravada?.OrigemId
            ? Task.FromResult(Result<UnidadeSugerida?>.Success(gravada))
            : ResolverAsync(unidadeSugeridaId, unidadeReader, cancellationToken);

    public static async Task<Result<UnidadeSugerida?>> ResolverAsync(
        Guid? unidadeSugeridaId,
        IUnidadeReader unidadeReader,
        CancellationToken cancellationToken)
    {
        if (unidadeSugeridaId is not { } id)
        {
            return Result<UnidadeSugerida?>.Success(null);
        }

        UnidadeView? unidade = await unidadeReader
            .ObterPorIdAsync(id, cancellationToken)
            .ConfigureAwait(false);
        if (unidade is null)
        {
            return Result<UnidadeSugerida?>.ValidationFailure([UnidadeNaoEncontrada(id)]);
        }

        return Result<UnidadeSugerida?>.Success(new UnidadeSugerida(unidade.Id, unidade.Sigla, unidade.Nome));
    }

    private static FieldError UnidadeNaoEncontrada(Guid id) =>
        new("unidadeSugeridaId", new DomainError(
            FaseCanonicaErrorCodes.UnidadeSugeridaNaoEncontrada,
            $"Unidade sugerida {id} não encontrada ou não está mais viva."));
}
