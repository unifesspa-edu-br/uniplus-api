namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da revogação. Quem revoga é o sujeito autenticado. A concessão de gestor de acessos só se revoga se sobrarem ao
/// menos dois gestores distintos com concessão ativa. A de Representante do controlador se revoga sob a trava do papel,
/// tomada antes da trava da concessão (ADR-0162).
/// </summary>
public static class RevogarConcessaoCommandHandler
{
    public static async Task<Result> Handle(
        RevogarConcessaoCommand command,
        IConcessaoRepository repository,
        IPapelDoRepresentante papel,
        IAutorizacaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(papel);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        // O ponto de decisão já exige emissor e sub, mas o handler não presume: sem autor
        // identificado a revogação não é gravada, e a recusa não vira 500.
        if (IdentidadeDoToken.De(userContext) is not { } revogadaPor)
        {
            return Result.Failure(ConcessaoHandlerErros.AutorNaoIdentificado());
        }

        return await TravarERevogarAsync(new(command, revogadaPor, timeProvider), repository, papel, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> TravarERevogarAsync(
        Pedido pedido, IConcessaoRepository repository, IPapelDoRepresentante papel, IAutorizacaoUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Concessao? concessao = await TravarAsync(pedido.Command.Id, repository, papel, cancellationToken).ConfigureAwait(false);
        return concessao is null
            ? Result.Failure(ConcessaoHandlerErros.NaoEncontrada(pedido.Command.Id))
            : await RevogarAsync(new(pedido.Command, concessao, pedido.Por, pedido.Clock.GetUtcNow()), repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    // A trava do papel vem antes da linha da concessão, e a linha da concessão antes da trava da governança, na mesma
    // ordem da revogação pela revisão: as revogações concorrentes esperam umas pelas outras em vez de travarem em ciclo.
    private static async Task<Concessao?> TravarAsync(
        Guid concessaoId, IConcessaoRepository repository, IPapelDoRepresentante papel, CancellationToken cancellationToken)
    {
        ConcessaoComPerfil? lida = await repository.ObterComPerfilAsync(concessaoId, cancellationToken).ConfigureAwait(false);
        if (lida?.Concessao.PerfilId == PerfisDeSistema.RepresentanteDoControladorId)
        {
            await papel.TravarAsync(cancellationToken).ConfigureAwait(false);
        }

        return lida is null ? null : await repository.ObterParaAlterarAsync(concessaoId, cancellationToken).ConfigureAwait(false);
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
                r.Concessao, r.Agora, () => r.Concessao.Revogar(r.Por, r.Command.Motivo, r.Agora), repository, unitOfWork, cancellationToken)
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
