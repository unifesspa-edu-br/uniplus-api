namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Termos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A publicação de uma versão do termo. Toma só a trava exclusiva da vigência, que nenhum outro caminho toma depois de
/// outra trava, e por isso não forma ciclo (ADR-0162): espera a criação de pedido protegido que a leu, e a publicação
/// concorrente que viu a mesma vigente espera esta e recebe conflito.
/// </summary>
public sealed class PublicacaoDoTermo
{
    private readonly ITermoDeResponsabilidadeRepository _termos;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public PublicacaoDoTermo(ITermoDeResponsabilidadeRepository termos, IUnitOfWork unitOfWork, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(termos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        _termos = termos;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    internal async Task<Result<VersaoDoTermoDto>> PublicarAsync(
        PublicarVersaoDoTermoCommand command, Identidade autor, CancellationToken cancellationToken)
    {
        Result<ConteudoDoTermo> conteudo = ConteudoDoTermo.Criar(command.Texto, command.Ato);
        if (conteudo.IsFailure)
        {
            return Result<VersaoDoTermoDto>.ValidationFailure(conteudo.Errors);
        }

        (TermoDeResponsabilidade termo, VersaoDoTermo? vigente) = await _termos.ObterParaPublicarAsync(cancellationToken).ConfigureAwait(false);
        Result<VersaoDoTermo> publicada = termo.Publicar(conteudo.Value!, autor, vigente, command.VersaoVigenteEsperadaId, _clock.GetUtcNow());

        return publicada.IsFailure
            ? Recusar(publicada.Errors)
            : await GravarAsync(publicada.Value!, cancellationToken).ConfigureAwait(false);
    }

    private Result<VersaoDoTermoDto> Recusar(IReadOnlyList<FieldError> erros)
    {
        _unitOfWork.DescartarAlteracoesNaoSalvas();
        return Result<VersaoDoTermoDto>.ValidationFailure(erros);
    }

    private async Task<Result<VersaoDoTermoDto>> GravarAsync(VersaoDoTermo versao, CancellationToken cancellationToken)
    {
        await _termos.AdicionarVersaoAsync(versao, cancellationToken).ConfigureAwait(false);

        return await SalvouAsync(cancellationToken).ConfigureAwait(false)
            ? Result<VersaoDoTermoDto>.Success(TermoMapping.ToDto(versao, versao.Id))
            : Result<VersaoDoTermoDto>.Failure(TermoHandlerErros.ConflitoDeConcorrencia());
    }

    // Com Idempotency-Key o handler captura e descarta o rastreamento (ADR-0119).
    private async Task<bool> SalvouAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            _unitOfWork.DescartarAlteracoesNaoSalvas();
            return false;
        }
    }
}
