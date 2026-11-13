namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Providencias;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A gravação comum aos atos da providência: grava o que o agregado aceitou e, só com a gravação aceita, emite os avisos
/// do ato, que entram no outbox na mesma transação. Na recusa, nada é gravado nem avisado.
/// </summary>
/// <remarks>
/// Os endpoints dos atos exigem <c>Idempotency-Key</c>, então o conflito de concorrência é capturado aqui, com o
/// rastreamento descartado, e não propagado (ADR-0119). A abertura concorrente para a mesma concessão esbarra no índice
/// único da providência aberta e recebe a mesma recusa do agregado.
/// </remarks>
public sealed class GravacaoDaProvidencia
{
    private const string AbertaUnicaPorConcessao = "ux_providencias_concessao_aberta";

    private readonly IAutorizacaoUnitOfWork _unitOfWork;
    private readonly AvisosDaProvidencia _avisos;

    public GravacaoDaProvidencia(IAutorizacaoUnitOfWork unitOfWork, AvisosDaProvidencia avisos)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(avisos);

        _unitOfWork = unitOfWork;
        _avisos = avisos;
    }

    /// <summary>Grava o ato e devolve a providência como quem agiu a lê no instante do ato, com os avisos a enviar.</summary>
    /// <param name="decisao">O resultado do ato no agregado.</param>
    /// <param name="providencia">A providência e a concessão alvo, já com o ato aplicado.</param>
    /// <param name="ato">Quem agiu e o instante do ato.</param>
    internal async Task<(Result<ProvidenciaDto> Resposta, IEnumerable<object> Mensagens)> GravarAsync(
        Result decisao, ProvidenciaComConcessao providencia, AtoDoAgente ato, CancellationToken cancellationToken)
    {
        Result gravada = decisao.IsFailure ? Descartar(decisao) : await SalvarAsync(cancellationToken).ConfigureAwait(false);
        if (gravada.IsFailure)
        {
            return (Result<ProvidenciaDto>.ValidationFailure(gravada.Errors), []);
        }

        IReadOnlyList<EnviarEmail> avisos = await _avisos.AvisarAsync(providencia, cancellationToken).ConfigureAwait(false);

        return (Result<ProvidenciaDto>.Success(ProvidenciaMapping.ToDto(providencia, ato.Instante, ato.Agente)), avisos);
    }

    // A recusa pode vir depois de a concessão ter sido mutada em memória: a gravação que o outbox dispara depois do
    // handler não pode persisti-la.
    private Result Descartar(Result recusa)
    {
        _unitOfWork.DescartarAlteracoesNaoSalvas();
        return recusa;
    }

    private async Task<Result> SalvarAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (RecusaDaGravacao(ex) is { } recusa)
        {
            return Descartar(Result.Failure(recusa));
        }
    }

    private static DomainError? RecusaDaGravacao(Exception ex)
    {
        if (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            return ProvidenciaHandlerErros.ConflitoDeConcorrencia();
        }

        return ViolacoesDeGravacao.ViolaIndiceUnico(ex, AbertaUnicaPorConcessao) ? ProvidenciaErros.JaHaAberta() : null;
    }
}
