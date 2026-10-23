namespace Unifesspa.UniPlus.Spikes.CifraNoMinio;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// O estado do arquivo na API: EM_VERIFICACAO na confirmação; VERIFICADO, RECUSADO ou
/// REENVIO_NECESSARIO (o objeto não está mais no bucket de envio) quando o veredicto chega. Os instantes servem às medições; <c>Veredictos</c> conta quantos veredictos
/// chegaram para o mesmo arquivo, o que mostra a entrega repetida sendo absorvida.
/// </summary>
public sealed class ArquivoEnviado
{
    public required string Chave { get; init; }

    public required string Estado { get; set; }

    public required DateTimeOffset ConfirmadoEm { get; init; }

    public required DateTimeOffset PublicadoEm { get; set; }

    public DateTimeOffset? InicioDaVerificacaoEm { get; set; }

    public DateTimeOffset? FimDaVerificacaoEm { get; set; }

    public DateTimeOffset? RegistradoEm { get; set; }

    public double? VerificacaoMs { get; set; }

    public string? Veredicto { get; set; }

    public string? Sha256 { get; set; }

    public long? Bytes { get; set; }

    public string? Verificador { get; set; }

    public int Veredictos { get; set; }

    public int Publicacoes { get; set; }
}

public sealed class Banco(DbContextOptions<Banco> options) : DbContext(options)
{
    public DbSet<ArquivoEnviado> Arquivos => Set<ArquivoEnviado>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<ArquivoEnviado>(arquivo =>
        {
            arquivo.ToTable("arquivo_enviado", "poc");
            arquivo.HasKey(a => a.Chave);
            arquivo.HasIndex(a => new { a.Estado, a.PublicadoEm });
        });
    }
}
