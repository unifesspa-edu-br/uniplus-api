namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Congelamento append-only da configuração de negócio de um
/// <see cref="Edital"/> no momento da publicação (RN08, ADR-0100). Um
/// snapshot por Edital — capturado EXPLICITAMENTE dentro de
/// <see cref="ProcessoSeletivo.Publicar"/> (evento de negócio único), nunca
/// por interceptor de <c>SaveChanges</c> (diferente de
/// <see cref="ObrigatoriedadeLegalHistorico"/>, que registra toda mutação).
/// </summary>
/// <remarks>
/// Implementa <see cref="IForensicEntity"/> per ADR-0063: forensic
/// append-only, deliberadamente NÃO herda <see cref="Kernel.Domain.Entities.EntityBase"/>
/// e NÃO carrega soft-delete. <see cref="HashConfiguracao"/> e
/// <see cref="ConfiguracaoCongelada"/> são DERIVADOS de
/// <see cref="ConfiguracaoCongeladaCanonica"/> dentro da factory — nunca
/// aceitos como parâmetros independentes, para que a evidência persistida
/// nunca possa divergir dos bytes que a fundamentam (ADR-0100 item 6/7).
/// </remarks>
public sealed class SnapshotPublicacao : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid EditalId { get; private init; }

    public string SchemaVersion { get; private init; } = null!;

    public string AlgoritmoHash { get; private init; } = null!;

    /// <summary>Bytes canônicos (ADR-0100) — a base do hash; fonte única de verdade.</summary>
#pragma warning disable CA1819 // Properties should not return arrays — entidade EF Core mapeia bytea diretamente; sem value-equality de record.
    public byte[] ConfiguracaoCongeladaCanonica { get; private init; } = null!;
#pragma warning restore CA1819

    /// <summary>Derivado por parsing UTF-8 dos bytes canônicos — só consulta (jsonb).</summary>
    public string ConfiguracaoCongelada { get; private init; } = null!;

    /// <summary>SHA-256 (hex minúsculo) de <see cref="ConfiguracaoCongeladaCanonica"/> — nunca recebido como parâmetro.</summary>
    public string HashConfiguracao { get; private init; } = null!;

    public string HashEdital { get; private init; } = null!;

    public string AtorUsuarioSub { get; private init; } = null!;

    public DateTimeOffset DataPublicacao { get; private init; }

    // Construtor de materialização do EF Core.
    private SnapshotPublicacao()
    {
    }

    /// <summary>
    /// Congela o snapshot a partir dos bytes canônicos já produzidos pelo
    /// <c>ISnapshotPublicacaoCanonicalizer</c> (Application) — deriva
    /// <see cref="HashConfiguracao"/> e <see cref="ConfiguracaoCongelada"/>
    /// internamente, garantindo que ambos sejam sempre consistentes com os
    /// bytes persistidos (revisão de plano P2, ADR-0100 §Confirmação).
    /// </summary>
    /// <remarks>
    /// Lança <see cref="ArgumentException"/> em vez de retornar
    /// <c>Result.Failure</c> — mesmo padrão de
    /// <see cref="ObrigatoriedadeLegalHistorico.Snapshot"/> (ADR-0063):
    /// entidades forensic são criadas a partir de invariantes JÁ garantidas
    /// pelo caller (<see cref="ProcessoSeletivo.Publicar"/> só chama esta
    /// factory com um <see cref="Edital"/> recém-emitido e bytes canônicos
    /// recém-produzidos) — as checagens aqui são defesa em profundidade
    /// contra erro de programação, não regra de negócio exposta ao usuário
    /// final (ADR-0046 continua valendo para regras de negócio genuínas).
    /// </remarks>
    public static SnapshotPublicacao Congelar(
        Guid editalId,
        byte[] configuracaoCongeladaCanonica,
        string schemaVersion,
        string algoritmoHash,
        string hashEdital,
        string atorUsuarioSub,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(configuracaoCongeladaCanonica);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(algoritmoHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(atorUsuarioSub);

        if (editalId == Guid.Empty)
        {
            throw new ArgumentException("O snapshot deve estar vinculado a um Edital.", nameof(editalId));
        }

        if (configuracaoCongeladaCanonica.Length == 0)
        {
            throw new ArgumentException("A configuração congelada não pode ser vazia.", nameof(configuracaoCongeladaCanonica));
        }

        if (!HashCanonicalComputer.IsValidHashShape(hashEdital))
        {
            throw new ArgumentException(
                "O hash do documento do Edital deve ser um SHA-256 em hexadecimal minúsculo (64 caracteres).",
                nameof(hashEdital));
        }

        return new SnapshotPublicacao
        {
            EditalId = editalId,
            SchemaVersion = schemaVersion,
            AlgoritmoHash = algoritmoHash,
            ConfiguracaoCongeladaCanonica = configuracaoCongeladaCanonica,
            ConfiguracaoCongelada = Encoding.UTF8.GetString(configuracaoCongeladaCanonica),
            HashConfiguracao = HashCanonicalComputer.ComputeSha256Hex(configuracaoCongeladaCanonica),
            HashEdital = hashEdital,
            AtorUsuarioSub = atorUsuarioSub,
            DataPublicacao = clock.GetUtcNow(),
        };
    }
}
