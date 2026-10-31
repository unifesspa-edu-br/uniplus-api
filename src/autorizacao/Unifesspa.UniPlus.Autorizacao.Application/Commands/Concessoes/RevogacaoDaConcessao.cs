namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A regra que toda revogação de concessão cumpre, qualquer que seja o caminho que a pede: a concessão de gestor de
/// acessos só se revoga se sobrarem ao menos dois gestores distintos com concessão ativa (ADR-0152).
/// </summary>
internal static class RevogacaoDaConcessao
{
    private const int MinimoDeGestores = 2;

    /// <summary>
    /// Aplica a revogação e confere o mínimo de gestores, sem gravar: quem chama grava, junto do que mais a revogação
    /// altera. Na recusa o rastreamento é descartado, porque a concessão já foi mutada em memória e a gravação que o outbox
    /// dispara depois do handler a persistiria.
    /// </summary>
    /// <param name="revogar">Aplica a revogação à <paramref name="concessao"/>; falha sem tê-la mutado.</param>
    public static async Task<Result> AplicarAsync(
        Concessao concessao,
        DateTimeOffset agora,
        Func<Result> revogar,
        IConcessaoRepository repository,
        IAutorizacaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(concessao);
        ArgumentNullException.ThrowIfNull(revogar);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        // Só a concessão que hoje conta como gestor reduz o número de gestores; revogar uma
        // vencida ou fora da instituição não muda a conta.
        bool contaComoGestor = concessao.EstaAtiva(agora) && concessao.EscopoTipo == TipoDeEscopo.Instituicao;

        Result revogada = revogar();
        if (revogada.IsFailure)
        {
            return revogada;
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
                unitOfWork.DescartarAlteracoesNaoSalvas();
                return Result.Failure(ConcessaoHandlerErros.MinimoDeGestores());
            }
        }

        return Result.Success();
    }
}
