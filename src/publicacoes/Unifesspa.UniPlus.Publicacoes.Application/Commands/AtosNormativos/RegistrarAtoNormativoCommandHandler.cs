namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Avisos;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

/// <summary>
/// Handler do <see cref="RegistrarAtoNormativoCommand"/> (convention-based Wolverine): monta o ato
/// (<see cref="MontagemDoAtoNormativo"/>) com o instante da divulgação recebido (ou lido uma vez
/// do relógio, no endpoint), registra-o append-only e computa o aviso de número
/// duplicado (AC4).
/// </summary>
public static class RegistrarAtoNormativoCommandHandler
{
    public static async Task<Result<RegistrarAtoNormativoResult>> Handle(
        RegistrarAtoNormativoCommand command,
        MontagemDoAtoNormativo montagem,
        IAtoNormativoRepository atosRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(montagem);
        ArgumentNullException.ThrowIfNull(atosRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Result<AtoMontado> montado = await montagem
            .MontarAsync(command, command.PublicadoEm ?? timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return montado.IsFailure
            ? Result<RegistrarAtoNormativoResult>.Failure(montado.Error!)
            : await GravarAsync(command, montado.Value!, atosRepository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Colisão de numeração é aviso, jamais recusa. No registro o próprio ato ainda não existe, então
    /// todos os que compartilham a numeração são conflitantes, menos a linhagem que este ato passa a
    /// integrar: uma republicação com o mesmo número dentro da cadeia de retificação não é duplicata,
    /// é a mesma linhagem (ADR-0103). O aviso é best-effort: como o número não tem unicidade, dois
    /// registros concorrentes da mesma numeração podem ambos observar zero conflitos; a consulta de
    /// detalhe recomputa e enxerga ambos.
    /// </summary>
    private static async Task<Result<RegistrarAtoNormativoResult>> GravarAsync(
        RegistrarAtoNormativoCommand command,
        AtoMontado montado,
        IAtoNormativoRepository atosRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AvisoNumeracao> avisos = await AvisoNumeracaoCalculator
            .CalcularAsync(atosRepository, montado.Ato, montado.IdsDaPropriaCadeia, cancellationToken)
            .ConfigureAwait(false);
        await atosRepository.AdicionarAsync(montado.Ato, cancellationToken).ConfigureAwait(false);
        DomainError? conflito = await SalvarAsync(command, unitOfWork, cancellationToken).ConfigureAwait(false);

        return conflito is null
            ? Result<RegistrarAtoNormativoResult>.Success(
                new RegistrarAtoNormativoResult(montado.Ato.Id, montado.Ato.PublicadoEm, avisos))
            : Result<RegistrarAtoNormativoResult>.Failure(conflito);
    }

    private static async Task<DomainError?> SalvarAsync(
        RegistrarAtoNormativoCommand command, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (ConflitoNaGravacaoDoAto.Traduzir(ex, command.AtoRetificadoId) is { } conflito)
        {
            return conflito;
        }
    }
}
