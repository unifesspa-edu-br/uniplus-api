namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDoProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Handler convention-based da inclusão de motivo no processo. A regra de quando incluir é do
/// domínio (<see cref="EscolhaDosMotivos"/>); aqui só se carrega o que ela confere e se grava.
/// </summary>
public static class IncluirMotivoNoProcessoCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        IncluirMotivoNoProcessoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IMotivoDeDecisaoRepository motivoDeDecisaoRepository,
        IMotivoDoProcessoRepository motivoDoProcessoRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(motivoDeDecisaoRepository);
        ArgumentNullException.ThrowIfNull(motivoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        ProcessoSeletivo? processo = await processoSeletivoRepository.ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        MotivoDeDecisao? origem = await motivoDeDecisaoRepository.ObterPorIdAsync(command.MotivoDeDecisaoId, cancellationToken).ConfigureAwait(false);
        if (processo is null || origem is null)
        {
            return processo is null
                ? EscritaDaEscolha.ProcessoNaoEncontrado(command.ProcessoSeletivoId)
                : Result<MutacaoAceita>.Failure(MotivoDeDecisaoHandlerErros.NaoEncontrado(command.MotivoDeDecisaoId));
        }

        return await IncluirAsync(new InclusaoPedida(command, processo, origem, timeProvider.GetUtcNow()), motivoDoProcessoRepository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<MutacaoAceita>> IncluirAsync(
        InclusaoPedida pedido,
        IMotivoDoProcessoRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MotivoDoProcesso> escolhidos = await repository.ListarDoProcessoAsync(pedido.Processo.Id, cancellationToken).ConfigureAwait(false);

        // Nenhuma decisão que cite motivo é registrada ainda neste módulo: o início da avaliação é,
        // por ora, só o da janela da fase. As decisões, quando chegarem, informam aqui o primeiro registro.
        Result<MotivoDoProcesso> inclusao = EscolhaDosMotivos.Incluir(new InclusaoDeMotivo(
            pedido.Processo, escolhidos, pedido.Origem, pedido.Command.Justificativa, pedido.Command.Precondicao, pedido.Agora, PrimeiroRegistroDaDecisao: null));
        if (inclusao.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<MutacaoAceita>.Failure(inclusao.Error!);
        }

        await repository.AdicionarAsync(inclusao.Value!, cancellationToken).ConfigureAwait(false);
        return await EscritaDaEscolha.GravarAsync(pedido.Processo, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private sealed record InclusaoPedida(IncluirMotivoNoProcessoCommand Command, ProcessoSeletivo Processo, MotivoDeDecisao Origem, DateTimeOffset Agora);
}
