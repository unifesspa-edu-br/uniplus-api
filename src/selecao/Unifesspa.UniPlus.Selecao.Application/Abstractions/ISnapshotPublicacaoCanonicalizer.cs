namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Domain.Entities;
using Domain.ValueObjects;

/// <summary>
/// Bytes canônicos + metadados de um snapshot de publicação, prontos para
/// <c>ProcessoSeletivo.Publicar</c> congelar (ADR-0100). Não carrega hash —
/// <c>SnapshotPublicacao.Congelar</c> deriva o hash dos bytes internamente
/// (revisão de plano, evita divergência entre bytes e hash persistidos).
/// </summary>
#pragma warning disable CA1819 // Properties should not return arrays — bytes canônicos, sem value-equality de record aplicável.
public sealed record SnapshotCanonico(byte[] Bytes, string SchemaVersion, string AlgoritmoHash);
#pragma warning restore CA1819

/// <summary>
/// Porta do serializador canônico dos 17 blocos do snapshot de publicação
/// (ADR-0100) — implementação em Infrastructure. Projeta a configuração viva
/// do <see cref="ProcessoSeletivo"/> num payload canônico (11 blocos reais +
/// 6 stubs <c>{"status":"nao_construido"}</c> para dimensões ainda não
/// implementadas), serializa e devolve os bytes que
/// <c>SnapshotPublicacao.Congelar</c> persiste como base do hash.
/// </summary>
public interface ISnapshotPublicacaoCanonicalizer
{
    SnapshotCanonico Canonicalizar(ProcessoSeletivo processo, DadosEdital dados, string hashEdital);
}
