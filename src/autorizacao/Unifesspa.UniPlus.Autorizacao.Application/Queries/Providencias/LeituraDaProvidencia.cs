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
/// derivada no instante da leitura e cada providência exibida registrada antes de a resposta sair (ADR-0153). Sem a
/// permissão no instante da leitura, o detalhe responde 403 e a fila sai vazia.
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

        return (await AcessoAProvidencia.LiberarAsync(verificador, [item], cancellationToken).ConfigureAwait(false)).Count > 0
            ? Result<ProvidenciaDto>.Success(ProvidenciaMapping.ToDto(item, _clock.GetUtcNow(), leitor))
            : Result<ProvidenciaDto>.Failure(ProvidenciaHandlerErros.SemPermissao());
    }

    /// <param name="leitor">Quem lê, para dizer se está impedido de responder; nulo quando o token não o identifica.</param>
    internal async Task<ListarProvidenciasResult> ListarAsync(
        ListarProvidenciasQuery query, IVerificadorDeAcesso verificador, Identidade? leitor, CancellationToken cancellationToken)
    {
        DateTimeOffset agora = _clock.GetUtcNow();
        FiltroDeProvidencias filtro = new(agora, query.Situacao, query.ConcessaoId);
        (IReadOnlyList<ProvidenciaComConcessao> itens, (string, Guid)? anterior, (string, Guid)? proximo) = await _providencias
            .ListarPaginadoAsync(filtro, query.AfterSortKey, query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        // A leitura negada não entrega item nem âncora: a página sai vazia, sem continuação.
        IReadOnlyList<ProvidenciaComConcessao> visiveis = await AcessoAProvidencia.LiberarAsync(verificador, itens, cancellationToken).ConfigureAwait(false);

        return visiveis.Count == 0
            ? new ListarProvidenciasResult([], null, null)
            : new ListarProvidenciasResult([.. visiveis.Select(i => ProvidenciaMapping.ToResumo(i, agora, leitor))], anterior, proximo);
    }
}
