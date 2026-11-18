namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDoProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Handler convention-based da inclusão de motivo numa fase ou etapa do processo. A regra de quando incluir é do domínio
/// (<see cref="EscolhaDosMotivos"/>); aqui só se carrega o que ela confere e se grava.
/// </summary>
public static class IncluirMotivoNoProcessoCommandHandler
{
    public static async Task<Result> Handle(
        IncluirMotivoNoProcessoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IMotivoDeDecisaoRepository motivoDeDecisaoRepository,
        IMotivoDoProcessoRepository motivoDoProcessoRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(motivoDeDecisaoRepository);
        ArgumentNullException.ThrowIfNull(motivoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<InclusaoPedida> pedido = await CarregarAsync(command, processoSeletivoRepository, motivoDeDecisaoRepository, cancellationToken).ConfigureAwait(false);
        return pedido.IsFailure
            ? Result.Failure(pedido.Error!)
            : await IncluirAsync(pedido.Value!, motivoDoProcessoRepository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<InclusaoPedida>> CarregarAsync(
        IncluirMotivoNoProcessoCommand command,
        IProcessoSeletivoRepository processos,
        IMotivoDeDecisaoRepository motivos,
        CancellationToken cancellationToken)
    {
        ProcessoSeletivo? processo = await processos.ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        if (RecusaAntesDeLerOCadastro(processo, command) is { } recusa)
        {
            return Result<InclusaoPedida>.Failure(recusa);
        }

        MotivoDeDecisao? origem = await motivos.ObterPorIdAsync(command.MotivoDeDecisaoId, cancellationToken).ConfigureAwait(false);
        return origem is null
            ? Result<InclusaoPedida>.Failure(MotivoDeDecisaoHandlerErros.NaoEncontrado(command.MotivoDeDecisaoId))
            : Result<InclusaoPedida>.Success(new InclusaoPedida(command, processo!, origem));
    }

    private static DomainError? RecusaAntesDeLerOCadastro(ProcessoSeletivo? processo, IncluirMotivoNoProcessoCommand command) =>
        processo is null
            ? DefinirFormularioCommandHandler.ProcessoNaoEncontrado(command.ProcessoSeletivoId)
            : EscolhaDosMotivos.RecusaDoLugarOuDoBloqueio(processo, command.Lugar);

    private static async Task<Result> IncluirAsync(
        InclusaoPedida pedido,
        IMotivoDoProcessoRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MotivoDoProcesso> escolhaAtual = await repository.ListarEscolhaAtualAsync(pedido.Processo.Id, cancellationToken).ConfigureAwait(false);
        Result<MotivoDoProcesso> incluida = EscolhaDosMotivos.Incluir(new InclusaoDeMotivo(pedido.Processo, pedido.Command.Lugar, escolhaAtual, pedido.Origem));
        if (incluida.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(incluida.Error!);
        }

        await repository.AdicionarAsync(incluida.Value!, cancellationToken).ConfigureAwait(false);
        return await EscritaDaEscolha.GravarAsync(pedido.Processo, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private sealed record InclusaoPedida(IncluirMotivoNoProcessoCommand Command, ProcessoSeletivo Processo, MotivoDeDecisao Origem);
}
