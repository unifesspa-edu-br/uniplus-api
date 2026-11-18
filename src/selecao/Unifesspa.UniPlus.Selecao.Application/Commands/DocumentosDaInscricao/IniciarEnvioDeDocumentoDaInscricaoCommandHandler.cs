namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;

using Domain.Entities;

using DTOs;

using Kernel.Results;

using Services;
using Services.Inscricoes;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>Confere o acesso do titular e confirma a apresentação e o arquivo pendente na mesma transação.</summary>
public static class IniciarEnvioDeDocumentoDaInscricaoCommandHandler
{
    public static async Task<Result<IniciarEnvioDeDocumentoDaInscricaoDto>> Handle(
        IniciarEnvioDeDocumentoDaInscricaoCommand command, AcessoAoDocumentoDaInscricao acesso,
        InicioDoEnvioDeDocumento inicio, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(acesso);
        ArgumentNullException.ThrowIfNull(inicio);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        Result<Inscricao> inscricao = await acesso.ResolverInscricaoAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return inscricao.IsFailure
            ? Result<IniciarEnvioDeDocumentoDaInscricaoDto>.Failure(inscricao.Error!)
            : await IniciarEConfirmarAsync(inscricao.Value!, command, inicio, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<IniciarEnvioDeDocumentoDaInscricaoDto>> IniciarEConfirmarAsync(
        Inscricao inscricao, IniciarEnvioDeDocumentoDaInscricaoCommand command,
        InicioDoEnvioDeDocumento inicio, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result<IniciarEnvioDeDocumentoDaInscricaoDto> resultado = await inicio.IniciarAsync(inscricao, command, cancellationToken).ConfigureAwait(false);
        return resultado.IsFailure ? resultado : await ConfirmarAsync(resultado, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<IniciarEnvioDeDocumentoDaInscricaoDto>> ConfirmarAsync(
        Result<IniciarEnvioDeDocumentoDaInscricaoDto> resultado, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return resultado;
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<IniciarEnvioDeDocumentoDaInscricaoDto>.Failure(AcessoAoDocumentoDaInscricao.InscricaoAlterada);
        }
    }
}
