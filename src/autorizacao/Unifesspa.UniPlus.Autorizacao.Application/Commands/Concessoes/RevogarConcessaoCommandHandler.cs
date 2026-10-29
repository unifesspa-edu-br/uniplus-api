namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da revogação. Quem revoga é o sujeito autenticado. A concessão de gestor de acessos
/// só se revoga se sobrarem ao menos dois gestores distintos com concessão ativa.
/// </summary>
public static class RevogarConcessaoCommandHandler
{
    private const int MinimoDeGestores = 2;

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

        Concessao? concessao = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (concessao is null)
        {
            return Result.Failure(ConcessaoHandlerErros.NaoEncontrada(command.Id));
        }

        DateTimeOffset agora = timeProvider.GetUtcNow();

        // Só a concessão que hoje conta como gestor reduz o número de gestores; revogar uma
        // vencida ou fora da instituição não muda a conta.
        bool contaComoGestor = concessao.EstaAtiva(agora) && concessao.EscopoTipo == TipoDeEscopo.Instituicao;

        Result revogar = concessao.Revogar(revogadaPor, command.Motivo, agora);
        if (revogar.IsFailure)
        {
            return revogar;
        }

        if (contaComoGestor
            && await repository.ObterGovernancaDoPerfilAsync(concessao.PerfilId, cancellationToken).ConfigureAwait(false)
                == TipoDeGovernanca.GestorDeAcessos)
        {
            // A contagem roda sob a trava, depois que ela é obtida: duas revogações simultâneas de
            // gestores diferentes (de três para um) não podem passar as duas.
            await repository.TravarGovernancaParaEscritaAsync(cancellationToken).ConfigureAwait(false);

            int restantes = await repository
                .ContarGestoresAtivosAsync(concessao.PerfilId, concessao.Id, agora, cancellationToken)
                .ConfigureAwait(false);

            if (restantes < MinimoDeGestores)
            {
                // A concessão já foi mutada em memória e segue rastreada: sem o descarte, a
                // gravação que o outbox dispara depois do handler persistiria a revogação.
                unitOfWork.DescartarAlteracoesNaoSalvas();
                return Result.Failure(ConcessaoHandlerErros.MinimoDeGestores());
            }
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
