namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class TermoConsentimentoConfiguration : IEntityTypeConfiguration<TermoConsentimento>
{
    private const int NomeMaxLength = 200;
    private const int TextoMaxLength = 20_000;
    private const int BaseLegalMaxLength = 500;
    private const int RevisadoPorMaxLength = 255;

    public void Configure(EntityTypeBuilder<TermoConsentimento> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "termo_consentimento",
            t => t.HasCheckConstraint(
                "ck_termo_consentimento_forma_aceite_rascunho",
                $"forma_aceite_rascunho IN ({TokensSql(FormasAceite.TokensCanonicos)})"));
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(t => t.TextoRascunho).HasMaxLength(TextoMaxLength);
        builder.Property(t => t.BaseLegalRascunho).HasMaxLength(BaseLegalMaxLength);

        builder.Property(t => t.FormaAceiteRascunho)
            .HasConversion<FormaAceiteValueConverter>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.Revisado).IsRequired();
        builder.Property(t => t.RevisadoPor).HasMaxLength(RevisadoPorMaxLength);
        builder.Property(t => t.RevisadoEm);

        // Token de concorrência otimista mapeado para a coluna de sistema `xmin` do
        // Postgres (shadow property `uint` + IsRowVersion — convenção do provider
        // Npgsql, sem coluna/migration própria, mesmo padrão de
        // CalendarioDiasUteisConfiguration): promover uma versão não escreve nenhum
        // campo do próprio rascunho, então sem um write explícito amarrado ao xmin
        // (ver PromoverVersaoTermoConsentimentoCommandHandler) uma promoção concorrente
        // com uma edição de rascunho que reverte a revisão não colidiria — a versão
        // imutável sairia gravada a partir de um rascunho já invalidado.
        builder.Property<uint>("Version").IsRowVersion();

        // Auditoria (IAuditableEntity)
        builder.Property(t => t.CreatedBy).HasMaxLength(255);
        builder.Property(t => t.UpdatedBy).HasMaxLength(255);

        // Versões promovidas: append-only, o agregado nunca edita/remove uma
        // versão existente, só adiciona (issue #1018). ClientNoAction (não
        // Restrict/Cascade): com a coleção já rastreada, Restrict/NoAction
        // lançam "required relationship severed" ao marcar o TermoConsentimento
        // como Deleted, ANTES de o interceptor convertê-lo em soft-delete;
        // ClientNoAction deixa o interceptor converter em UPDATE e as versões
        // permanecem intactas (mesmo padrão de CalendarioDiasUteis.DiasNaoUteis).
        // O handler de remoção já recusa apagar termo com versão promovida — na
        // prática esta relação nunca chega a ser exercitada por um DELETE físico.
        builder.HasMany(t => t.Versoes)
            .WithOne()
            .HasForeignKey(v => v.TermoConsentimentoId)
            .OnDelete(DeleteBehavior.ClientNoAction);
        builder.Navigation(t => t.Versoes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(t => t.Nome)
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_termo_consentimento_nome");
    }

    private static string TokensSql(IReadOnlyList<string> tokens) =>
        string.Join(", ", tokens.Select(token => $"'{token}'"));
}
