namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDoProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Handler convention-based da inclusão de motivo numa fase ou etapa do processo. A regra de quando incluir é do domínio
/// (<see cref="EscolhaDosMotivos"/>); aqui só se carrega o que ela confere e se grava. O que já começou só se lê sob a
/// sessão de retificação, porque o rascunho nunca foi publicado.
/// </summary>
public static class IncluirMotivoNoProcessoCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        IncluirMotivoNoProcessoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IMotivoDeDecisaoRepository motivoDeDecisaoRepository,
        IMotivoDoProcessoRepository motivoDoProcessoRepository,
        IFasesIniciadasDoProcesso fasesIniciadasDoProcesso,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(motivoDeDecisaoRepository);
        ArgumentNullException.ThrowIfNull(motivoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(fasesIniciadasDoProcesso);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<InclusaoPedida> pedido = await CarregarAsync(command, processoSeletivoRepository, motivoDeDecisaoRepository, cancellationToken).ConfigureAwait(false);
        return pedido.IsFailure
            ? Result<MutacaoAceita>.Failure(pedido.Error!)
            : await IncluirAsync(pedido.Value!, motivoDoProcessoRepository, fasesIniciadasDoProcesso, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<InclusaoPedida>> CarregarAsync(
        IncluirMotivoNoProcessoCommand command,
        IProcessoSeletivoRepository processos,
        IMotivoDeDecisaoRepository motivos,
        CancellationToken cancellationToken)
    {
        ProcessoSeletivo? processo = await processos.ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        MotivoDeDecisao? origem = await motivos.ObterPorIdAsync(command.MotivoDeDecisaoId, cancellationToken).ConfigureAwait(false);
        if (processo is null)
        {
            return Result<InclusaoPedida>.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(command.ProcessoSeletivoId));
        }

        return origem is null
            ? Result<InclusaoPedida>.Failure(MotivoDeDecisaoHandlerErros.NaoEncontrado(command.MotivoDeDecisaoId))
            : Result<InclusaoPedida>.Success(new InclusaoPedida(command, processo, origem));
    }

    private static async Task<Result<MutacaoAceita>> IncluirAsync(
        InclusaoPedida pedido,
        IMotivoDoProcessoRepository repository,
        IFasesIniciadasDoProcesso fasesIniciadas,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        InclusaoDeMotivo inclusao = await MontarAsync(pedido, repository, fasesIniciadas, cancellationToken).ConfigureAwait(false);
        Result<MotivoDoProcesso> incluida = EscolhaDosMotivos.Incluir(inclusao);
        if (incluida.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<MutacaoAceita>.Failure(incluida.Error!);
        }

        await repository.AdicionarAsync(incluida.Value!, cancellationToken).ConfigureAwait(false);
        return await EscritaDaEscolha.GravarAsync(pedido.Processo, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<InclusaoDeMotivo> MontarAsync(
        InclusaoPedida pedido, IMotivoDoProcessoRepository repository, IFasesIniciadasDoProcesso fasesIniciadas, CancellationToken cancellationToken)
    {
        IReadOnlyList<MotivoDoProcesso> escolhaAtual = await repository.ListarEscolhaAtualAsync(pedido.Processo.Id, cancellationToken).ConfigureAwait(false);
        FasesIniciadas iniciadas = pedido.Processo.Rascunho is null
            ? FasesIniciadas.Nenhuma
            : (await fasesIniciadas.ObterAgoraAsync(pedido.Processo.Id, cancellationToken).ConfigureAwait(false)).Fases;

        return new InclusaoDeMotivo(
            pedido.Processo, pedido.Command.Lugar, escolhaAtual, pedido.Origem, pedido.Command.Justificativa, pedido.Command.Precondicao, iniciadas);
    }

    private sealed record InclusaoPedida(IncluirMotivoNoProcessoCommand Command, ProcessoSeletivo Processo, MotivoDeDecisao Origem);
}
