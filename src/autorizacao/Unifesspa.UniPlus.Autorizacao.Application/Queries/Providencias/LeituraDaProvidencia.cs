namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Providencias;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Providencias;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A leitura das providências por quem administra concessões ou solicita providências: o detalhe e a fila, com a situação
/// derivada no instante da leitura e cada providência exibida registrada antes de a resposta sair (ADR-0153).
/// </summary>
public sealed class LeituraDaProvidencia
{
    private readonly IProvidenciaRepository _providencias;
    private readonly TimeProvider _clock;

    public LeituraDaProvidencia(IProvidenciaRepository providencias, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(providencias);
        ArgumentNullException.ThrowIfNull(clock);

        _providencias = providencias;
        _clock = clock;
    }

    /// <param name="leitor">Quem lê, para dizer se está impedido de responder; nulo quando o token não o identifica.</param>
    internal async Task<Result<ProvidenciaDto>> ObterAsync(Guid id, IVerificadorDeAcesso verificador, Identidade? leitor, CancellationToken cancellationToken)
    {
        if (await _providencias.ObterComConcessaoAsync(id, cancellationToken).ConfigureAwait(false) is not { } item)
        {
            return Result<ProvidenciaDto>.Failure(ProvidenciaHandlerErros.NaoEncontrada());
        }

        await AcessoAProvidencia.RegistrarLeituraAsync(verificador, [id], cancellationToken).ConfigureAwait(false);

        return Result<ProvidenciaDto>.Success(ProvidenciaMapping.ToDto(item, _clock.GetUtcNow(), leitor));
    }

    /// <param name="leitor">Quem lê, para dizer se está impedido de responder; nulo quando o token não o identifica.</param>
    internal async Task<ListarProvidenciasResult> ListarAsync(
        ListarProvidenciasQuery query, IVerificadorDeAcesso verificador, Identidade? leitor, CancellationToken cancellationToken)
    {
        DateTimeOffset agora = _clock.GetUtcNow();
        (IReadOnlyList<ProvidenciaComConcessao> itens, Guid? anterior, Guid? proximo) = await _providencias
            .ListarPaginadoAsync(new FiltroDeProvidencias(agora, query.Situacao, query.ConcessaoId), query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        await AcessoAProvidencia.RegistrarLeituraAsync(verificador, [.. itens.Select(static i => i.Id)], cancellationToken).ConfigureAwait(false);

        return new ListarProvidenciasResult([.. itens.Select(i => ProvidenciaMapping.ToResumo(i, agora, leitor))], anterior, proximo);
    }
}
