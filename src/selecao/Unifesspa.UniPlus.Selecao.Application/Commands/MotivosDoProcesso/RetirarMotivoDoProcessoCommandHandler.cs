namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDoProcesso;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>Handler convention-based da retirada de motivo do processo.</summary>
public static class RetirarMotivoDoProcessoCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        RetirarMotivoDoProcessoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IMotivoDoProcessoRepository motivoDoProcessoRepository,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(motivoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository.ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        if (processo is null)
        {
            return EscritaDaEscolha.ProcessoNaoEncontrado(command.ProcessoSeletivoId);
        }

        IReadOnlyList<MotivoDoProcesso> escolhidos = await motivoDoProcessoRepository.ListarDoProcessoAsync(processo.Id, cancellationToken).ConfigureAwait(false);
        MotivoDoProcesso? escolhido = escolhidos.FirstOrDefault(e => e.MotivoDeDecisaoOrigemId == command.MotivoDeDecisaoId);
        return escolhido is null
            ? Result<MutacaoAceita>.Failure(NaoEscolhido(command.MotivoDeDecisaoId))
            : await RetirarAsync(processo, escolhido, command, motivoDoProcessoRepository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<MutacaoAceita>> RetirarAsync(
        ProcessoSeletivo processo,
        MotivoDoProcesso escolhido,
        RetirarMotivoDoProcessoCommand command,
        IMotivoDoProcessoRepository repository,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result retirada = EscolhaDosMotivos.Retirar(processo, escolhido, command.Precondicao);
        if (retirada.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<MutacaoAceita>.Failure(retirada.Error!);
        }

        repository.Remover(escolhido);
        return await EscritaDaEscolha.GravarAsync(processo, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static DomainError NaoEscolhido(Guid motivoDeDecisaoId) =>
        new(MotivoDeDecisaoErrorCodes.NaoEscolhido, $"O processo não escolheu o motivo de decisão {motivoDeDecisaoId}.");
}
