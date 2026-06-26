namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based do <see cref="AtualizarObrigatoriedadeLegalCommand"/>.
/// Semântica full-replace (ADR-0058 Emenda 1): o caller deve repassar todos os
/// campos. A regra é cross-cutting por tipo de processo — sem proprietário nem
/// áreas de interesse.
/// </summary>
public static class AtualizarObrigatoriedadeLegalCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarObrigatoriedadeLegalCommand command,
        IObrigatoriedadeLegalRepository repository,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ObrigatoriedadeLegal? regra = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (regra is null)
        {
            return Result.Failure(new DomainError(
                "ObrigatoriedadeLegal.NaoEncontrada",
                $"ObrigatoriedadeLegal {command.Id} não encontrada."));
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
            portariaInternaCodigo: command.PortariaInternaCodigo);
        if (atualizado.IsFailure)
        {
            return atualizado;
        }

        repository.Atualizar(regra);

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
