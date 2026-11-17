namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDoProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Handler convention-based da retirada de motivo do processo. Achar a escolha no lugar e recusar a que não existe é do
/// domínio (<see cref="EscolhaDosMotivos"/>); aqui só se carrega e se grava.
/// </summary>
public static class RetirarMotivoDoProcessoCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        RetirarMotivoDoProcessoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IMotivoDoProcessoRepository motivoDoProcessoRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(motivoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository.ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return processo is null
            ? Result<MutacaoAceita>.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(command.ProcessoSeletivoId))
            : await RetirarAsync(processo, command, motivoDoProcessoRepository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<MutacaoAceita>> RetirarAsync(
        ProcessoSeletivo processo,
        RetirarMotivoDoProcessoCommand command,
        IMotivoDoProcessoRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MotivoDoProcesso> escolhaAtual = await repository.ListarEscolhaAtualAsync(processo.Id, cancellationToken).ConfigureAwait(false);
        Result<MotivoDoProcesso> retirada = EscolhaDosMotivos.Retirar(processo, escolhaAtual, command.Lugar, command.MotivoDeDecisaoId, command.Precondicao);
        if (retirada.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<MutacaoAceita>.Failure(retirada.Error!);
        }

        repository.Remover(retirada.Value!);
        return await EscritaDaEscolha.GravarAsync(processo, unitOfWork, cancellationToken).ConfigureAwait(false);
    }
}
