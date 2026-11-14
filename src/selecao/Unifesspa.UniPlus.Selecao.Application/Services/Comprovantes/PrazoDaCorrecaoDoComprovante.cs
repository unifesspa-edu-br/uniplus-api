namespace Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// O prazo que o comprovante que vale mostra para a correção: o fim do período de inscrição da versão divulgada, lido ao vivo
/// porque a prorrogação vale para quem já se inscreveu, e se a correção ainda cabe agora, pela mesma regra de
/// <see cref="Inscricao.AcoesPermitidas"/>. A inscrição cancelada não se corrige, e o prazo é informação acessória: se não se lê, o comprovante sai sem ele.
/// </summary>
public sealed class PrazoDaCorrecaoDoComprovante
{
    private readonly ICertameDivulgadoRepository _certameDivulgadoRepository;
    private readonly IProcessoSeletivoRepository _processoSeletivoRepository;
    private readonly IRegistroCodecsEnvelope _registroCodecs;
    private readonly IResolvedorFusoInstitucional _resolvedorFuso;
    private readonly TimeProvider _timeProvider;

    public PrazoDaCorrecaoDoComprovante(
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IResolvedorFusoInstitucional resolvedorFuso,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(resolvedorFuso);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _certameDivulgadoRepository = certameDivulgadoRepository;
        _processoSeletivoRepository = processoSeletivoRepository;
        _registroCodecs = registroCodecs;
        _resolvedorFuso = resolvedorFuso;
        _timeProvider = timeProvider;
    }

    /// <summary>O prazo da correção da inscrição, ou nulo quando o comprovante não é o que vale, a inscrição está cancelada ou a janela não se lê.</summary>
    public async Task<PrazoDaCorrecaoDto?> LerAsync(Inscricao inscricao, bool vale, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);

        if (!vale || inscricao.Situacao == SituacaoInscricao.Cancelada)
        {
            return null;
        }

        Result<VersaoDivulgada> divulgada = await VersaoDivulgadaResolver
            .ResolverAsync(inscricao.ProcessoSeletivoId, _certameDivulgadoRepository, _processoSeletivoRepository, cancellationToken)
            .ConfigureAwait(false);
        Result<JanelaDaInscricao> janela = divulgada.IsFailure
            ? Result<JanelaDaInscricao>.Failure(divulgada.Error!)
            : PeriodoDaInscricaoResolver.JanelaDa(divulgada.Value!.Versao, _registroCodecs);

        return janela.IsFailure ? null : Prazo(inscricao, janela.Value!);
    }

    private PrazoDaCorrecaoDto? Prazo(Inscricao inscricao, JanelaDaInscricao janela)
    {
        Result<TimeZoneInfo> fuso = _resolvedorFuso.Resolver();
        if (fuso.IsFailure)
        {
            return null;
        }

        bool inscricoesAbertas = janela.Conferir(_timeProvider.GetUtcNow(), fuso.Value!) is null;

        return new PrazoDaCorrecaoDto(janela.Fim, FusoHorarioDto.Institucional, inscricao.AcoesPermitidas(inscricoesAbertas).PodeCorrigir);
    }
}
