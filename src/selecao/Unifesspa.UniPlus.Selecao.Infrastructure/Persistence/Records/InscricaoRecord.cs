namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Modelo de persistência de <see cref="Domain.Entities.Inscricao"/> — não é a entidade de
/// domínio. O EF Core mapeia exclusivamente este tipo; o domínio nunca é materializado
/// diretamente pelo <c>ChangeTracker</c> (ADR-0121, Opção C, mesmo desenho de
/// <c>VinculoDiscenteRecord</c> em Discentes).
/// </summary>
/// <remarks>
/// <see cref="CpfCiphertext"/> guarda o envelope autenticado (nonce + tag + dados cifrados)
/// produzido por <c>IUniPlusEncryptionService</c> — nunca texto claro. <see cref="CpfIndice"/> é o
/// índice cego (HMAC) de <c>IUniPlusBlindIndexService</c>, que sustenta o índice único
/// <c>(ProcessoSeletivoId, CpfIndice)</c>. As respostas ficam em <see cref="RespostaDaInscricaoRecord"/>,
/// <see cref="EnderecoDaInscricaoRecord"/>, <see cref="OcorrenciaDeGrupoDaInscricaoRecord"/> e
/// <see cref="AceiteDaInscricaoRecord"/> (ADR-0141). A tradução para/de
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

    public Guid? OfertaCursoSegundaOpcaoId { get; set; }

    public string? NumeroInscricao { get; set; }

    /// <summary>Token de concorrência (ADR-0119) — espelha <see cref="Domain.Entities.Inscricao.Revisao"/>.</summary>
    public int Revisao { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
