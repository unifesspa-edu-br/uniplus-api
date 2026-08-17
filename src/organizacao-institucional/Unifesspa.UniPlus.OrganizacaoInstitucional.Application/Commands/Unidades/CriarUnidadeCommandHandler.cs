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
/// Sequência: valida o payload por inteiro primeiro (sem I/O), incluindo o
/// formato do Slug — validação sempre vence I/O — só então confere a
/// unicidade de Slug, Sigla e Codigo entre unidades vivas e a hierarquia
/// (superior existe), cria o agregado via <see cref="Unidade.Criar"/> (que
/// não pode falhar de novo, já validado) e persiste; ao final, invalida o
/// cache do reader cross-módulo (ADR-0056).
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

        Result validacao = Unidade.ValidarCampos(
            command.Nome,
            command.Alias,
            command.Slug,
            command.Sigla,
            command.Codigo,
            command.Tipo,
            command.VigenciaInicio,
            command.VigenciaFim,
            command.CidadeCodigoIbge,
            command.CidadeNome,
            command.CidadeUf);
        if (validacao.IsFailure)
        {
            return Result<Guid>.ValidationFailure(validacao.Errors);
        }

        // O payload já foi confirmado válido acima, com o mesmo Slug — não pode
        // falhar de novo.
        Slug slug = Slug.From(command.Slug).Value!;

        if (await repository.SlugExisteEntreLivosAsync(slug, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(new DomainError(
                UnidadeErrorCodes.SlugJaExiste,
                $"Já existe uma Unidade viva com o slug '{slug}'."));
        }

        if (await repository.SiglaExisteEntreLivosAsync(command.Sigla!, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(new DomainError(
                UnidadeErrorCodes.SiglaJaExiste,
                $"Já existe uma Unidade viva com a sigla '{command.Sigla}'."));
        }

        if (await repository.CodigoExisteEntreLivosAsync(command.Codigo!, null, cancellationToken).ConfigureAwait(false))
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

        Unidade unidade = Unidade.Criar(
            command.Nome,
            command.Alias,
            command.Slug,
            command.Sigla,
            command.Codigo,
            command.UnidadeSuperiorId,
            command.Tipo,
            command.UnidadeAcademica,
            command.VigenciaInicio,
            command.VigenciaFim,
            command.CidadeCodigoIbge,
            command.CidadeNome,
            command.CidadeUf).Value!;

        await repository.AdicionarAsync(unidade, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        await cacheInvalidator.InvalidarAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(unidade.Id);
    }
}
