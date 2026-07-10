namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class AtoNormativoConfiguration : IEntityTypeConfiguration<AtoNormativo>
{
    private const int OrgaoMaxLength = 200;
    private const int SerieMaxLength = 100;
    private const int NumeroMaxLength = 60;
    private const int TipoCodigoMaxLength = 60;
    private const int AssinanteMaxLength = 200;
    private const int HashLength = 64;

    public void Configure(EntityTypeBuilder<AtoNormativo> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "ato_normativo",
            t =>
            {
                // Defesa em profundidade do formato do hash do documento contra
                // inserts crus (o guard de domínio já recusa). SHA-256 hex minúsculo.
                t.HasCheckConstraint(
                    "ck_ato_normativo_documento_hash",
                    "documento_hash ~ '^[0-9a-f]{64}$'");

                // O par {id, hash} da versão invocada é completo ou ausente: um
                // identificador sem hash não prova nada (ADR-0075).
                t.HasCheckConstraint(
                    "ck_ato_normativo_versao_completa",
                    "(versao_invocada_id IS NULL AND versao_invocada_hash IS NULL) "
                    + "OR (versao_invocada_id IS NOT NULL AND versao_invocada_hash IS NOT NULL)");

                // Formato do hash da versão quando presente.
                t.HasCheckConstraint(
                    "ck_ato_normativo_versao_hash",
                    "versao_invocada_hash IS NULL OR versao_invocada_hash ~ '^[0-9a-f]{64}$'");

                // Um Guid.Empty não referencia versão alguma — defesa contra insert cru.
                t.HasCheckConstraint(
                    "ck_ato_normativo_versao_id_nao_zero",
                    "versao_invocada_id IS NULL OR versao_invocada_id <> '00000000-0000-0000-0000-000000000000'");

                // Ano é declarado pelo órgão; importação de acervo histórico admite
                // anos antigos, então não há teto — apenas positividade.
                t.HasCheckConstraint("ck_ato_normativo_ano_positivo", "ano > 0");
            });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Orgao).HasMaxLength(OrgaoMaxLength).IsRequired();
        builder.Property(a => a.Serie).HasMaxLength(SerieMaxLength).IsRequired();
        builder.Property(a => a.Ano).IsRequired();
        builder.Property(a => a.Numero).HasMaxLength(NumeroMaxLength);
        builder.Property(a => a.TipoCodigo).HasMaxLength(TipoCodigoMaxLength).IsRequired();

        builder.Property(a => a.CongelaConfiguracao).IsRequired();
        builder.Property(a => a.EfeitoIrreversivel).IsRequired();

        builder.Property(a => a.DataPublicacao).IsRequired();

        builder.Property(a => a.DocumentoHash)
            .HasMaxLength(HashLength)
            .IsFixedLength()
            .IsRequired();

        builder.Property(a => a.Assinante).HasMaxLength(AssinanteMaxLength).IsRequired();

        builder.Property(a => a.RegistradoEm).IsRequired();

        // Versão de configuração que governou o ato, por valor {id, hash} (ADR-0075),
        // sem chave estrangeira cruzando módulo (ADR-0061). Owned opcional: ambas as
        // colunas são nulas juntas quando o ato não invoca configuração.
        builder.OwnsOne(a => a.VersaoInvocada, versao =>
        {
            versao.Property(v => v.Id).HasColumnName("versao_invocada_id");
            versao.Property(v => v.Hash)
                .HasColumnName("versao_invocada_hash")
                .HasMaxLength(HashLength)
                .IsFixedLength();
        });
        builder.Navigation(a => a.VersaoInvocada).IsRequired(false);

        // Lookup por numeração para o aviso de duplicata (AC4). NÃO é único: o
        // número é declarado, não gerado — dois atos com a mesma numeração são
        // aceitos, e a colisão vira aviso, jamais recusa.
        builder.HasIndex(a => new { a.Orgao, a.Serie, a.Ano, a.Numero })
            .HasDatabaseName("ix_ato_normativo_numeracao");
    }
}
