namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da revogação. Quem revoga é o sujeito autenticado. A concessão de gestor de acessos só se revoga se sobrarem ao
/// menos dois gestores distintos com concessão ativa. A de Representante do controlador se revoga sob a trava do papel, e
/// a de qualquer perfil de governança, sob a trava da pessoa do beneficiário, ambas tomadas antes da trava da concessão
/// (ADR-0162).
/// </summary>
public static class RevogarConcessaoCommandHandler
{
    public static async Task<Result> Handle(
        RevogarConcessaoCommand command,
        IConcessaoRepository repository,
        TravasDaRevogacao travas,
        IAutorizacaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(travas);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        // O ponto de decisão já exige emissor e sub, mas o handler não presume: sem autor
        // identificado a revogação não é gravada, e a recusa não vira 500.
        if (IdentidadeDoToken.De(userContext) is not { } revogadaPor)
        {
            return Result.Failure(ConcessaoHandlerErros.AutorNaoIdentificado());
        }

        return await TravarERevogarAsync(new(command, revogadaPor, timeProvider), repository, travas, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> TravarERevogarAsync(
        Pedido pedido, IConcessaoRepository repository, TravasDaRevogacao travas, IAutorizacaoUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Concessao? concessao = await travas.TravarAsync(pedido.Command.Id, cancellationToken).ConfigureAwait(false);
        return concessao is null
            ? Result.Failure(ConcessaoHandlerErros.NaoEncontrada(pedido.Command.Id))
            : await RevogarAsync(new(pedido.Command, concessao, pedido.Por, pedido.Clock.GetUtcNow()), repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> RevogarAsync(
        Revogacao r, IConcessaoRepository repository, IAutorizacaoUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        // A concessão já revogada responde antes: confirmar a pessoa de algo encerrado não leva a nada.
        if (r.Concessao.RevogadaEm is null && !r.Concessao.ConfirmaEmailDoBeneficiario(r.Command.ConfirmacaoEmail))
        {
            return Result.ValidationFailure([new FieldError("confirmacaoEmail", ConcessaoHandlerErros.ConfirmacaoDivergente())]);
        }

        Result revogada = await RevogacaoDaConcessao
            .AplicarAsync(
                r.Concessao, r.Agora, () => r.Concessao.Revogar(new AtoDoAgente(r.Por, r.Agora), r.Command.Motivo), repository, unitOfWork, cancellationToken)
            .ConfigureAwait(false);

        return revogada.IsFailure ? revogada : await GravarAsync(unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> GravarAsync(IAutorizacaoUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (ConcessaoViolacoes.EhConflitoDeConcorrencia(ex))
        {
            // Duas revogações a partir da mesma versão não se misturam. Traduzir aqui, e não
            // deixar propagar, devolve 409 em vez de 500 e libera a chave de idempotência
            // para nova tentativa, já que nada foi gravado (ADR-0119, ADR-0027).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(ConcessaoHandlerErros.ConflitoDeConcorrencia());
        }
    }

    private sealed record Pedido(RevogarConcessaoCommand Command, Identidade Por, TimeProvider Clock);

    private sealed record Revogacao(RevogarConcessaoCommand Command, Concessao Concessao, Identidade Por, DateTimeOffset Agora);
}
