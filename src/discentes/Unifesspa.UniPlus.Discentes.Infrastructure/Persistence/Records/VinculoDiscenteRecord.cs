namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Records;

/// <summary>
/// Modelo de persistência de <see cref="Domain.Entities.VinculoDiscente"/> — não é a
/// entidade de domínio. O EF Core mapeia exclusivamente este tipo; o domínio nunca é
/// materializado diretamente pelo <c>ChangeTracker</c> (ADR-0121, Opção C).
/// </summary>
/// <remarks>
/// <see cref="CpfCiphertext"/> guarda o envelope autenticado (nonce + tag + dados cifrados)
/// produzido por <c>IUniPlusEncryptionService</c> — nunca os 11 dígitos em texto claro. A
/// tradução para/de <see cref="Domain.Entities.VinculoDiscente"/> é responsabilidade exclusiva
/// do repositório (<c>VinculoDiscenteRepository</c>), nunca de um <c>ValueConverter</c>.
/// </remarks>
public sealed class VinculoDiscenteRecord
{
    public Guid Id { get; set; }

    public long IdDiscenteSigaa { get; set; }

    public string Matricula { get; set; } = null!;

    public byte[] CpfCiphertext { get; set; } = null!;

    public string Nome { get; set; } = null!;

    public string Nivel { get; set; } = null!;

    public int CursoId { get; set; }

    public string CursoNome { get; set; } = null!;

    public string? CursoCodigoEmec { get; set; }

    public int CursoUnidadeId { get; set; }

    public string CursoUnidadeNome { get; set; } = null!;

    public int SituacaoId { get; set; }

    public string SituacaoDescricao { get; set; } = null!;

    public string? SituacaoVinculo { get; set; }

    public int AnoIngresso { get; set; }

    public int PeriodoIngresso { get; set; }
}
