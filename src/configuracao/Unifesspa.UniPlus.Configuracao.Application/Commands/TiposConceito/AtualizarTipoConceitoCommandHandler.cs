namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Valida a definição sem I/O antes de buscar o tipo: a violação de campo vence o "não encontrado", e um
/// payload mal formado não consulta o repositório (ADR-0125, item 5).
/// </summary>
public static class AtualizarTipoConceitoCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarTipoConceitoCommand command,
        ITipoConceitoRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<DefinicaoValidada> definicao = DefinicaoDoComando.De(command).Validar();
        return definicao.IsFailure
            ? Result.ValidationFailure(definicao.Errors)
            : await Aplicar(command.Id, definicao.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> Aplicar(
        Guid id, DefinicaoValidada definicao, ITipoConceitoRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        TipoConceito? tipo = await repository.ObterPorIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return Result.Failure(RecusasDoTipoConceito.NaoEncontrado());
        }

        tipo.Atualizar(definicao);
        return await Salvar(unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    // Endpoint com [RequiresIdempotencyKey]: o conflito do xmin não propaga (ADR-0119). O descarte
    // impede que o SaveChangesAsync do outbox reencontre a substituição dos valores depois do retorno.
    private static async Task<Result> Salvar(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex) || ColisaoDeSubstituicaoConcorrente.Ocorreu(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(RecusasDoTipoConceito.ConflitoDeConcorrencia());
        }
    }
}
