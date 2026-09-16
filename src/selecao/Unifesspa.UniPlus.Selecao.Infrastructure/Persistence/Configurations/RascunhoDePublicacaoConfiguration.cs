namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class RascunhoDePublicacaoConfiguration : IEntityTypeConfiguration<RascunhoDePublicacao>
{
    public void Configure(EntityTypeBuilder<RascunhoDePublicacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("rascunhos_publicacao");
        builder.HasKey(r => r.Id);
        // Guid v7 gerado no domínio (EntityBase) — mesma convenção das demais entidades.
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.UsuarioSub).HasMaxLength(255).IsRequired();

        // `text`, e não `jsonb`: o documento precisa voltar EXATAMENTE como foi enviado, e o
        // `jsonb` não faz isso — ele parseia, reordena as chaves e renormaliza os números, que
        // é o oposto de guardar sem interpretar. A garantia que se perde, a de recusar JSON
        // malformado na escrita, já é dada antes: o corpo chega tipado como documento JSON e
        // não há caminho pelo qual texto inválido alcance a coluna.
        builder.Property(r => r.Conteudo).IsRequired();

        builder.Property(r => r.Versao).IsRequired();
        builder.Property(r => r.ExpiraEm).IsRequired();

        // SalvoEm é derivada de CreatedAt/UpdatedAt, que o AuditableInterceptor carimba — não
        // há coluna própria para ela.
        builder.Ignore(r => r.SalvoEm);

        // Vínculo por FK, sem navegação inversa: o rascunho não é entidade filha do agregado.
        // O Cascade declarado aqui nunca dispara — ProcessoSeletivo é SoftDeletableEntity e
        // jamais emite DELETE —, e por isso a remoção é explícita em todo caminho que registra
        // o ato. Está declarado assim mesmo assim porque descrever a dependência real é o que
        // permite ao Postgres recusar um rascunho órfão.
        builder.HasOne<ProcessoSeletivo>()
            .WithMany()
            .HasForeignKey(r => r.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Rascunho tem dono: um por operador por processo. É esta restrição que faz o debate
        // sobre precondição de concorrência desaparecer — sem recurso compartilhado, não há o
        // que arbitrar. Também é o índice que serve a única leitura que existe.
        // A varredura dos vencidos roda a cada gravação (ver ApagarVencidosAsync): sem este
        // índice ela seria um seq scan na tabela inteira a cada salvamento.
        builder.HasIndex(r => r.ExpiraEm)
            .HasDatabaseName("ix_rascunhos_publicacao_expira_em");

        builder.HasIndex(r => new { r.ProcessoSeletivoId, r.UsuarioSub })
            .IsUnique()
            .HasDatabaseName("ux_rascunhos_publicacao_processo_operador");
    }
}
