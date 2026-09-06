namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Valida nome e descrição (sem I/O) antes de buscar a entidade: sem o validator
/// removido, um payload mal formado não pode chegar a <c>ObterPorIdAsync</c>
/// primeiro — validação sempre vence sobre "não encontrado".
/// </summary>
public static class AtualizarTipoProcessoCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarTipoProcessoCommand command,
        ITipoProcessoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<(string Nome, string? Descricao)> validacao = TipoProcesso.ValidarCamposEditaveis(command.Nome, command.Descricao);
        if (validacao.IsFailure)
        {
            return Result.ValidationFailure(validacao.Errors);
        }

        TipoProcesso? tipo = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return Result.Failure(new DomainError(TipoProcessoErrorCodes.NaoEncontrado, "Tipo de processo seletivo não encontrado."));
        }

        // Revalida por dentro (barato, sem I/O) com exatamente os mesmos argumentos
        // já confirmados acima, então sempre terá sucesso aqui; esta chamada só
        // serve para aplicar a mutação.
        tipo.Atualizar(command.Nome, command.Descricao);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            // Corrida com outra escrita no mesmo tipo (edição concorrente,
            // desativação, reativação), vista pelo xmin. Catch local em vez de
            // propagação porque o endpoint tem [RequiresIdempotencyKey]
            // (ADR-0119); o descarte impede que o SaveChangesAsync do outbox
            // reencontre a entidade modificada depois do retorno.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(
                TipoProcessoErrorCodes.ConflitoDeConcorrencia,
                "Este tipo de processo seletivo foi alterado concorrentemente. Tente novamente."));
        }

        return Result.Success();
    }
}
