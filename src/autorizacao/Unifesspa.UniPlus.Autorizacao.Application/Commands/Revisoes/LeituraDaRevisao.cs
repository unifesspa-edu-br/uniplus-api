namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// A revisão como a resposta a mostra: com os itens e, no item pendente, quem o decide pela condição do beneficiário no
/// instante da leitura, que pode mudar até a decisão (ADR-0162).
/// </summary>
public sealed class LeituraDaRevisao
{
    private readonly IRevisaoDeAcessosRepository _revisoes;
    private readonly IPapelDoRepresentante _papel;

    public LeituraDaRevisao(IRevisaoDeAcessosRepository revisoes, IPapelDoRepresentante papel)
    {
        ArgumentNullException.ThrowIfNull(revisoes);
        ArgumentNullException.ThrowIfNull(papel);

        _revisoes = revisoes;
        _papel = papel;
    }

    /// <summary>A revisão no instante, com os itens da concessão de quem lê marcados; nula quando não existe.</summary>
    public async Task<RevisaoDeAcessosDto?> LerAsync(Guid revisaoId, DateTimeOffset agora, Identidade? leitor, CancellationToken cancellationToken)
    {
        RevisaoDetalhada? detalhada = await _revisoes.ObterDetalhadaAsync(revisaoId, agora, cancellationToken).ConfigureAwait(false);
        if (detalhada is null)
        {
            return null;
        }

        IReadOnlySet<Identidade> representantes = await _papel.ListarVigentesAsync(agora, cancellationToken).ConfigureAwait(false);
        return RevisaoMapping.ToDto(detalhada, representantes, leitor);
    }
}
