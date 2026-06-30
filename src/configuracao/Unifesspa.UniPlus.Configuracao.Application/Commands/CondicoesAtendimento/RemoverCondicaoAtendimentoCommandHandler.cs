namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CondicoesAtendimento;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="RemoverCondicaoAtendimentoCommand"/>. Soft-delete via
/// <c>SoftDeleteInterceptor</c>. A remoção é bloqueada apenas para o código
/// reservado <see cref="CodigoCondicao.Pcd"/> (<c>RemocaoBloqueadaCodigoProtegido</c>);
/// nunca é bloqueada por consumo de Seleção — o consumo cross-módulo é snapshot-copy
/// desacoplado (ADR-0061).
/// </summary>
public static class RemoverCondicaoAtendimentoCommandHandler
{
    public static async Task<Result> Handle(
        RemoverCondicaoAtendimentoCommand command,
        ICondicaoAtendimentoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        CondicaoAtendimentoEspecializado? condicao = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (condicao is null)
        {
            return Result.Failure(new DomainError(
                CondicaoAtendimentoErrorCodes.NaoEncontrada,
                "Condição de atendimento especializado não encontrada."));
        }

        if (condicao.Codigo.EhProtegido)
        {
            return Result.Failure(new DomainError(
                CondicaoAtendimentoErrorCodes.RemocaoBloqueadaCodigoProtegido,
                $"A condição reservada '{CodigoCondicao.Pcd}' não pode ser removida."));
        }

        repository.Remover(condicao);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
