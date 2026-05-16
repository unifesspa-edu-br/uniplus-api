namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Linha append-only do histórico de mutações de
/// <see cref="ObrigatoriedadeLegal"/> (CA-03 da Story #460). Cada
/// criação/atualização/desativação de uma regra insere uma linha desta
/// tabela na MESMA transação do save da regra — via
/// <c>ObrigatoriedadeLegalHistoricoInterceptor</c>.
/// </summary>
/// <remarks>
/// <para>
/// Implementa <see cref="IForensicEntity"/> per ADR-0063: forensic
/// append-only, deliberadamente NÃO herda <see cref="Kernel.Domain.Entities.EntityBase"/>
/// e NÃO carrega soft-delete. Linhas só recebem <c>INSERT</c> — qualquer
/// <c>UPDATE</c>/<c>DELETE</c> em produção é tratado como incidente
/// operacional.
/// </para>
/// <para>
/// <see cref="ConteudoJson"/> guarda o JSON canônico (mesma serialização
/// que alimenta <c>HashCanonicalComputer</c>) com todos os campos semânticos
/// + governance no momento do snapshot. Independente do que aconteça com a
/// linha vigente em <c>obrigatoriedades_legais</c>, o histórico reconstrói
/// fielmente "qual regra rodou em data X".
/// </para>
/// </remarks>
public sealed class ObrigatoriedadeLegalHistorico : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid RegraId { get; private init; }

    public string ConteudoJson { get; private init; } = null!;

    public string Hash { get; private init; } = null!;

    public DateTimeOffset SnapshotAt { get; private init; }

    public string SnapshotBy { get; private init; } = null!;

    // Construtor de materialização do EF Core.
    private ObrigatoriedadeLegalHistorico()
    {
    }

    /// <summary>
    /// Cria uma linha do histórico. Invocado pelo interceptor com o JSON
    /// canônico, hash recomputado e o <c>sub</c> do JWT do usuário responsável
    /// pela mutação (fallback <c>"system"</c> em jobs).
    /// </summary>
    public static ObrigatoriedadeLegalHistorico Snapshot(
        Guid regraId,
        string conteudoJson,
        string hash,
        DateTimeOffset snapshotAt,
        string snapshotBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conteudoJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotBy);

        if (regraId == Guid.Empty)
        {
            throw new ArgumentException(
                "regraId não pode ser Guid.Empty — a regra deve ter Id v7 atribuído antes do snapshot.",
                nameof(regraId));
        }

        return new ObrigatoriedadeLegalHistorico
        {
            RegraId = regraId,
            ConteudoJson = conteudoJson,
            Hash = hash,
            SnapshotAt = snapshotAt,
            SnapshotBy = snapshotBy,
        };
    }
}
