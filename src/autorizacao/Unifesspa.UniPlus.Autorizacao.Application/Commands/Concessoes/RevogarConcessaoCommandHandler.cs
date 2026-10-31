namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da revogação. Quem revoga é o sujeito autenticado. A concessão de gestor de acessos
/// só se revoga se sobrarem ao menos dois gestores distintos com concessão ativa.
/// </summary>
public static class RevogarConcessaoCommandHandler
{
    public static async Task<Result> Handle(
        RevogarConcessaoCommand command,
        IConcessaoRepository repository,
        IAutorizacaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        // O ponto de decisão já exige emissor e sub, mas o handler não presume: sem autor
        // identificado a revogação não é gravada, e a recusa não vira 500.
        if (IdentidadeDoToken.De(userContext) is not { } revogadaPor)
        {
            return Result.Failure(ConcessaoHandlerErros.AutorNaoIdentificado());
        }

        // A linha da concessão é travada antes da trava da governança, na mesma ordem da revogação pela revisão:
        // as duas revogações do mesmo gestor esperam uma pela outra em vez de travarem em ciclo.
        Concessao? concessao = await repository.ObterParaAlterarAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (concessao is null)
        {
            return Result.Failure(ConcessaoHandlerErros.NaoEncontrada(command.Id));
        }

        // A concessão já revogada responde antes: confirmar a pessoa de algo encerrado não leva a nada.
        if (concessao.RevogadaEm is null && !concessao.ConfirmaEmailDoBeneficiario(command.ConfirmacaoEmail))
        {
            return Result.ValidationFailure([new FieldError("confirmacaoEmail", ConcessaoHandlerErros.ConfirmacaoDivergente())]);
        }

        DateTimeOffset agora = timeProvider.GetUtcNow();

        Result revogacao = await RevogacaoDaConcessao
            .AplicarAsync(
                concessao, agora, () => concessao.Revogar(revogadaPor, command.Motivo, agora), repository, unitOfWork, cancellationToken)
            .ConfigureAwait(false);
        if (revogacao.IsFailure)
        {
            return revogacao;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ConcessaoViolacoes.EhConflitoDeConcorrencia(ex))
        {
            // Duas revogações a partir da mesma versão não se misturam. Traduzir aqui, e não
            // deixar propagar, devolve 409 em vez de 500 e libera a chave de idempotência
            // para nova tentativa, já que nada foi gravado (ADR-0119, ADR-0027).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(ConcessaoHandlerErros.ConflitoDeConcorrencia());
        }

        return Result.Success();
    }
}
