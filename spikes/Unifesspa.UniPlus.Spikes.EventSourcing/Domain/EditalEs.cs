using Unifesspa.UniPlus.Spikes.EventSourcing.Domain.Eventos;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Domain;

/// <summary>
/// Projeção single-stream do edital event-sourced (read model). Reconstruída por
/// replay do stream via convenções <c>Create</c>/<c>Apply</c> do Marten.
/// <para>
/// Deliberadamente <b>não decifra</b> a PII do ator — o read model é PII-free por
/// design (LGPD). O fato de quem agiu vive cifrado no stream; revelar exige a chave.
/// </para>
/// <para>
/// Não tem dependência de <c>Marten.*</c>: o domínio permanece limpo e o Marten
/// descobre os métodos por convenção (fitness test de fronteira, gate G6).
/// </para>
/// </summary>
public sealed record EditalEs
{
    /// <summary>Identidade do agregado = id do stream.</summary>
    public Guid Id { get; init; }

    /// <summary>Versão do stream; populada pelo Marten (concorrência otimista).</summary>
    public long Version { get; init; }

    public string NumeroEdital { get; init; } = string.Empty;

    public string Titulo { get; init; } = string.Empty;

    public StatusEditalEs Status { get; init; }

    public int QuantidadeRetificacoes { get; init; }

    public string? MotivoUltimaRetificacao { get; init; }

    public static EditalEs Create(EditalAberto e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return new EditalEs
        {
            Id = e.EditalId,
            NumeroEdital = e.NumeroEdital,
            Titulo = e.Titulo,
            Status = StatusEditalEs.Rascunho,
        };
    }

    public EditalEs Apply(EditalPublicado e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return this with { Status = StatusEditalEs.Publicado };
    }

    public EditalEs Apply(EditalRetificado e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return this with
        {
            QuantidadeRetificacoes = QuantidadeRetificacoes + 1,
            MotivoUltimaRetificacao = e.Motivo,
        };
    }
}
