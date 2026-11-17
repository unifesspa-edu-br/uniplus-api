namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

/// <summary>
/// Descarta a sessão editorial (ADR-0110): <b>repõe a configuração congelada</b> e encerra a sessão — na mesma
/// transação, pelo <see cref="DescarteDaRetificacao"/>.
/// </summary>
/// <remarks>
/// A ordem dos dois passos é a coisa inteira. Enquanto a sessão existe, os <c>Definir*</c> escrevem direto na
/// configuração viva — não há staging, e todo <c>Definir*</c> substitui a coleção inteira —, de modo que não existe
/// desfazer incremental. Encerrar a sessão sem repor devolveria o certame ao estado "publicado normal" servindo, em
/// silêncio, uma configuração que nunca foi publicada. Descartar não gera versão, evento nem ato: o mundo nunca soube da
/// edição, e continua sem saber.
/// </remarks>
public static class DescartarRetificacaoCommandHandler
{
    public static async Task<Result> Handle(
        DescartarRetificacaoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        DescarteDaRetificacao descarte,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(descarte);

        Result<ProcessoSeletivo> obtido = await ObterParaDescartarAsync(command, processoSeletivoRepository, cancellationToken).ConfigureAwait(false);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await descarte.DescartarAsync(obtido.Value!, command.Precondicao, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<ProcessoSeletivo>> ObterParaDescartarAsync(
        DescartarRetificacaoCommand command, IProcessoSeletivoRepository repositorio, CancellationToken cancellationToken)
    {
        ProcessoSeletivo? processo = await repositorio
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<ProcessoSeletivo>.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(command.ProcessoSeletivoId));
        }

        return PrecondicoesDoEncerramentoDaSessao.Recusa(command.Precondicao, processo, "O descarte") is { } recusa
            ? Result<ProcessoSeletivo>.Failure(recusa)
            : Result<ProcessoSeletivo>.Success(processo);
    }
}
