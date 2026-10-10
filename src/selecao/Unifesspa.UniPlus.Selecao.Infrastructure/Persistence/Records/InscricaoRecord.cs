namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Modelo de persistência de <see cref="Domain.Entities.Inscricao"/> — não é a entidade de
/// domínio. O EF Core mapeia exclusivamente este tipo; o domínio nunca é materializado
/// diretamente pelo <c>ChangeTracker</c> (ADR-0121, Opção C, mesmo desenho de
/// <c>VinculoDiscenteRecord</c> em Discentes).
/// </summary>
/// <remarks>
/// <see cref="CpfCiphertext"/> e <see cref="DocumentosCifrados"/> guardam envelopes autenticados
/// (nonce + tag + dados cifrados) produzidos por <c>IUniPlusEncryptionService</c> — nunca texto
/// claro. <see cref="CpfIndice"/> é o índice cego (HMAC) de <c>IUniPlusBlindIndexService</c>, que
/// sustenta o índice único <c>(ProcessoSeletivoId, CpfIndice)</c>. A tradução para/de
/// <see cref="Domain.Entities.Inscricao"/> é responsabilidade exclusiva do
/// <c>InscricaoRepository</c>, nunca de um <c>ValueConverter</c>.
/// </remarks>
public sealed class InscricaoRecord
{
    public Guid Id { get; set; }

    public Guid ProcessoSeletivoId { get; set; }

    public byte[] CpfCiphertext { get; set; } = null!;

    public byte[] CpfIndice { get; set; } = null!;

    public string Sub { get; set; } = null!;

    public string EmissorToken { get; set; } = null!;

    public SituacaoInscricao Situacao { get; set; }

    public Guid VersaoFormularioId { get; set; }

    public Guid? OfertaCursoId { get; set; }

    public string? NumeroInscricao { get; set; }

    /// <summary>Respostas por fato (código → valor), SEM números de documento — ver <see cref="DocumentosCifrados"/>. Texto jsonb bruto.</summary>
    public string RespostasJson { get; set; } = null!;

    /// <summary>Ocorrências de grupo repetível, mesma exclusão de documento. Texto jsonb bruto.</summary>
    public string GruposJson { get; set; } = null!;

    /// <summary>Aceites de termo. Texto jsonb bruto.</summary>
    public string AceitesJson { get; set; } = null!;

    /// <summary>
    /// Os códigos de fato cuja classificação congelada é <c>IDENTIFICADOR</c>, fixados ao iniciar —
    /// decide quais chaves de respostas vão para <see cref="DocumentosCifrados"/> em vez do jsonb em
    /// claro. Texto jsonb bruto (array de strings).
    /// </summary>
    public string FatosIdentificadorDeclaradosJson { get; set; } = null!;

    /// <summary>
    /// Envelope AEAD com o mapa <c>{código do fato: valor}</c> dos números de documento
    /// (<see cref="FatosIdentificadorDeclaradosJson"/>) — separados do jsonb em claro antes de
    /// gravar, nunca cifrados campo a campo.
    /// </summary>
    public byte[] DocumentosCifrados { get; set; } = null!;

    /// <summary>Token de concorrência (ADR-0119) — espelha <see cref="Domain.Entities.Inscricao.Revisao"/>.</summary>
    public int Revisao { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
