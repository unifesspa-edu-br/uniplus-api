namespace Unifesspa.UniPlus.Selecao.Application.Commands.NotasDoEnem;

using Domain.Entities;
using Domain.Interfaces;

using DTOs.NotasDoEnem;

using Kernel.Results;

using Services.NotasDoEnem;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>Prepara o retorno do INEP antes do bloqueio e confirma o lote na mesma transação.</summary>
public static class ImportarNotasDoEnemCommandHandler
{
    public static async Task<Result<ImportacaoDeNotasDto>> Handle(ImportarNotasDoEnemCommand command,
        PreparacaoDaImportacaoDeNotas preparacao, ImportacaoDasNotasDoEnem importacao,
        IProcessoSeletivoRepository processos, IUnitOfWork unitOfWork, TimeProvider relogio, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(preparacao);
        ArgumentNullException.ThrowIfNull(importacao);
        ArgumentNullException.ThrowIfNull(processos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(relogio);
        Result<ImportacaoDeNotasPreparada> preparada = await preparacao.PrepararAsync(command, cancellationToken).ConfigureAwait(false);
        return preparada.IsFailure ? Result<ImportacaoDeNotasDto>.Failure(preparada.Error!)
            : await ConcluirEConfirmarAsync(command, preparada.Value!, importacao, processos, unitOfWork, relogio, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<ImportacaoDeNotasDto>> ConcluirEConfirmarAsync(ImportarNotasDoEnemCommand command,
        ImportacaoDeNotasPreparada preparada, ImportacaoDasNotasDoEnem importacao, IProcessoSeletivoRepository processos,
        IUnitOfWork unitOfWork, TimeProvider relogio, CancellationToken cancellationToken)
    {
        // Publicações e importações do processo leem a versão divulgada e o instante após o bloqueio.
        await processos.TravarAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        Result<(LoteDeNotasDoEnem Lote, bool Criado)> resultado = await importacao.ConcluirAsync(command, preparada, relogio.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (resultado.IsFailure)
        {
            return Result<ImportacaoDeNotasDto>.Failure(resultado.Error!);
        }
        if (resultado.Value.Criado)
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        return await importacao.ResponderAsync(resultado.Value.Lote, resultado.Value.Criado, cancellationToken).ConfigureAwait(false);
    }
}
