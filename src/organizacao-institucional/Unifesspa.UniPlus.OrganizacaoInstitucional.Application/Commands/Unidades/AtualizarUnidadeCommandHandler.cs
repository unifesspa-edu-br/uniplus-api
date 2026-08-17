namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

/// <summary>
/// Handler do <see cref="AtualizarUnidadeCommand"/>. Valida o payload por
/// inteiro (incluindo o formato do Slug) ANTES de qualquer I/O — validação
/// sempre vence 404 — e só então busca a Unidade por Id, confere unicidade e
/// hierarquia.
/// </summary>
public static class AtualizarUnidadeCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarUnidadeCommand command,
        IUnidadeRepository repository,
        IOrganizacaoInstitucionalUnitOfWork unitOfWork,
        IUnidadeCacheInvalidator cacheInvalidator,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(cacheInvalidator);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result validacao = Unidade.ValidarCampos(
            command.Nome,
            command.Alias,
            command.Slug,
            command.Sigla,
            command.Codigo,
            command.Tipo,
            // VigenciaInicio não é editável — a checagem de coerência com VigenciaFim
            // exige o valor persistido, então roda de novo dentro de Atualizar
            // (após o fetch), com o VigenciaInicio real da Unidade existente.
            DateOnly.MinValue,
            null,
            command.CidadeCodigoIbge,
            command.CidadeNome,
            command.CidadeUf);
        if (validacao.IsFailure)
        {
            return Result.ValidationFailure(validacao.Errors);
        }

        Unidade? unidade = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (unidade is null)
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.NaoEncontrada,
                "Unidade não encontrada."));
        }

        // O payload já foi confirmado válido acima, com o mesmo Slug — não pode
        // falhar de novo.
        Slug slug = Slug.From(command.Slug).Value!;

        if (!string.Equals(slug.Valor, unidade.Slug.Valor, StringComparison.OrdinalIgnoreCase)
            && await repository.SlugExisteEntreLivosAsync(slug, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.SlugJaExiste,
                $"Já existe uma Unidade viva com o slug '{slug}'."));
        }

        if (!string.Equals(command.Sigla, unidade.Sigla, StringComparison.OrdinalIgnoreCase)
            && await repository.SiglaExisteEntreLivosAsync(command.Sigla!, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.SiglaJaExiste,
                $"Já existe uma Unidade viva com a sigla '{command.Sigla}'."));
        }

        // Codigo é case-sensitive (o agregado preserva a caixa e o índice único é
        // case-sensitive) — compara com Ordinal para que ABC→abc conte como
        // mudança e dispare a checagem, em vez de estourar no índice (500).
        if (!string.Equals(command.Codigo!.Trim(), unidade.Codigo, StringComparison.Ordinal)
            && await repository.CodigoExisteEntreLivosAsync(command.Codigo, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.CodigoJaExiste,
                $"Já existe uma Unidade viva com o código '{command.Codigo}'."));
        }

        if (command.UnidadeSuperiorId.HasValue)
        {
            if (command.UnidadeSuperiorId.Value == command.Id)
            {
                return Result.Failure(new DomainError(
                    UnidadeErrorCodes.SuperiorFormaCiclo,
                    "Uma Unidade não pode ser superior de si mesma."));
            }

            Unidade? superior = await repository.ObterPorIdAsync(
                command.UnidadeSuperiorId.Value, cancellationToken).ConfigureAwait(false);

            if (superior is null)
            {
                return Result.Failure(new DomainError(
                    UnidadeErrorCodes.SuperiorNaoEncontrado,
                    "A Unidade superior informada não foi encontrada."));
            }

            // Ciclo: o superior proposto é descendente da própria unidade editada.
            if (await repository.EhDescendenteAsync(command.UnidadeSuperiorId.Value, command.Id, cancellationToken).ConfigureAwait(false))
            {
                return Result.Failure(new DomainError(
                    UnidadeErrorCodes.SuperiorFormaCiclo,
                    "A Unidade superior informada é descendente da Unidade sendo editada — formaria ciclo na hierarquia."));
            }
        }

        DateOnly dataAtual = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        Result atualizarResult = unidade.Atualizar(
            command.Nome,
            command.Alias,
            command.Slug,
            command.Sigla,
            command.Codigo,
            command.UnidadeSuperiorId,
            command.Tipo,
            command.UnidadeAcademica,
            command.VigenciaFim,
            dataAtual,
            command.MotivoMudancaIdentificador,
            command.CidadeCodigoIbge,
            command.CidadeNome,
            command.CidadeUf);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        await cacheInvalidator.InvalidarAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
