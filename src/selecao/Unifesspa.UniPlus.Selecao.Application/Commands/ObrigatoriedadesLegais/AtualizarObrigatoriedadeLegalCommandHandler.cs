namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based do <see cref="AtualizarObrigatoriedadeLegalCommand"/>.
/// Semântica full-replace (ADR-0058 Emenda 1) + RBAC área-scoped: o caller
/// deve administrar a área <strong>atual</strong> da regra E a área
/// <strong>nova</strong> (impede transferência cross-área por admin não
/// platform-wide). Reconciliação da junction temporal pelo repositório.
/// </summary>
public static class AtualizarObrigatoriedadeLegalCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarObrigatoriedadeLegalCommand command,
        IObrigatoriedadeLegalRepository repository,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        ObrigatoriedadeLegal? regra = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (regra is null)
        {
            return Result.Failure(new DomainError(
                "ObrigatoriedadeLegal.NaoEncontrada",
                $"ObrigatoriedadeLegal {command.Id} não encontrada."));
        }

        Result authzAtual = AreaScopedAuthorization.Autorizar(userContext, regra.Proprietario);
        if (authzAtual.IsFailure)
        {
            return authzAtual;
        }

        Result<HashSet<AreaCodigo>> areasResult =
            CriarObrigatoriedadeLegalCommandHandler.ConverterAreas(command.AreasDeInteresse);
        if (areasResult.IsFailure)
        {
            return Result.Failure(areasResult.Error!);
        }

        Result<AreaCodigo?> proprietarioResult =
            CriarObrigatoriedadeLegalCommandHandler.ConverterProprietario(command.Proprietario);
        if (proprietarioResult.IsFailure)
        {
            return Result.Failure(proprietarioResult.Error!);
        }

        if (regra.Proprietario != proprietarioResult.Value)
        {
            Result authzNovo = AreaScopedAuthorization.Autorizar(userContext, proprietarioResult.Value);
            if (authzNovo.IsFailure)
            {
                return authzNovo;
            }
        }

        bool duplicado = await repository.ExisteRegraCodigoAtivoAsync(
            command.RegraCodigo,
            excluirId: command.Id,
            cancellationToken).ConfigureAwait(false);
        if (duplicado)
        {
            return Result.Failure(new DomainError(
                "ObrigatoriedadeLegal.RegraCodigoDuplicada",
                $"Já existe outra regra ativa com RegraCodigo '{command.RegraCodigo}'."));
        }

        Result atualizado = regra.Atualizar(
            tipoEditalCodigo: command.TipoEditalCodigo,
            categoria: command.Categoria,
            regraCodigo: command.RegraCodigo,
            predicado: command.Predicado,
            descricaoHumana: command.DescricaoHumana,
            baseLegal: command.BaseLegal,
            vigenciaInicio: command.VigenciaInicio,
            vigenciaFim: command.VigenciaFim,
            atoNormativoUrl: command.AtoNormativoUrl,
            portariaInternaCodigo: command.PortariaInternaCodigo,
            proprietario: proprietarioResult.Value,
            areasDeInteresse: areasResult.Value);
        if (atualizado.IsFailure)
        {
            return atualizado;
        }

        await repository.ReconciliarBindingsAsync(
            regra,
            areasResult.Value!,
            timeProvider.GetUtcNow(),
            userContext.UserId ?? "system",
            cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint)
        {
            // Mesmo tratamento do Criar: race entre o ExisteRegraCodigoAtivoAsync
            // e o UPDATE (caller troca RegraCodigo para um valor que outra escrita
            // concorrente acabou de assumir) dispara a constraint do banco.
            if (UniqueConstraintViolation.IsRegraCodigoConflict(constraint))
            {
                return Result.Failure(new DomainError(
                    "ObrigatoriedadeLegal.RegraCodigoDuplicada",
                    $"Já existe outra regra ativa com RegraCodigo '{command.RegraCodigo}'."));
            }
            if (UniqueConstraintViolation.IsHashConflict(constraint))
            {
                return Result.Failure(new DomainError(
                    "ObrigatoriedadeLegal.HashColisao",
                    "Já existe regra ativa com o mesmo conteúdo canônico após esta atualização."));
            }
            throw;
        }

        return Result.Success();
    }
}
