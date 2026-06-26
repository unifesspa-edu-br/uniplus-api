namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

/// <summary>
/// Handler do <see cref="CriarUnidadeCommand"/>. Convention-based para Wolverine
/// — método estático <c>Handle</c> com dependências por parâmetro.
/// </summary>
/// <remarks>
/// Sequência:
/// <list type="number">
///   <item>Valida o Slug via <see cref="Slug.From"/>;</item>
///   <item>Confirma unicidade de Slug, Sigla e Codigo entre unidades vivas;</item>
///   <item>Valida hierarquia (superior existe e não forma ciclo), se informado;</item>
///   <item>Cria o agregado via <see cref="Unidade.Criar"/>;</item>
///   <item>Persiste + commit;</item>
///   <item>Invalida o cache do reader cross-módulo (ADR-0056).</item>
/// </list>
/// </remarks>
public static class CriarUnidadeCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarUnidadeCommand command,
        IUnidadeRepository repository,
        IOrganizacaoInstitucionalUnitOfWork unitOfWork,
        IUnidadeCacheInvalidator cacheInvalidator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(cacheInvalidator);

        Result<Slug> slugResult = Slug.From(command.Slug);
        if (slugResult.IsFailure)
        {
            return Result<Guid>.Failure(slugResult.Error!);
        }

        Slug slug = slugResult.Value!;

        if (await repository.SlugExisteEntreLivosAsync(slug, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(new DomainError(
                UnidadeErrorCodes.SlugJaExiste,
                $"Já existe uma Unidade viva com o slug '{slug}'."));
        }

        if (await repository.SiglaExisteEntreLivosAsync(command.Sigla, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(new DomainError(
                UnidadeErrorCodes.SiglaJaExiste,
                $"Já existe uma Unidade viva com a sigla '{command.Sigla}'."));
        }

        if (await repository.CodigoExisteEntreLivosAsync(command.Codigo, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(new DomainError(
                UnidadeErrorCodes.CodigoJaExiste,
                $"Já existe uma Unidade viva com o código '{command.Codigo}'."));
        }

        if (command.UnidadeSuperiorId.HasValue)
        {
            Unidade? superior = await repository.ObterPorIdAsync(
                command.UnidadeSuperiorId.Value, cancellationToken).ConfigureAwait(false);

            if (superior is null)
            {
                return Result<Guid>.Failure(new DomainError(
                    UnidadeErrorCodes.SuperiorNaoEncontrado,
                    "A Unidade superior informada não foi encontrada."));
            }
        }

        Result<Unidade> unidadeResult = Unidade.Criar(
            command.Nome,
            command.Alias,
            slug,
            command.Sigla,
            command.Codigo,
            command.UnidadeSuperiorId,
            command.Tipo,
            command.UnidadeAcademica,
            command.VigenciaInicio,
            command.VigenciaFim,
            command.Origem);

        if (unidadeResult.IsFailure)
        {
            return Result<Guid>.Failure(unidadeResult.Error!);
        }

        Unidade unidade = unidadeResult.Value!;
        await repository.AdicionarAsync(unidade, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        await cacheInvalidator.InvalidarAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(unidade.Id);
    }
}
