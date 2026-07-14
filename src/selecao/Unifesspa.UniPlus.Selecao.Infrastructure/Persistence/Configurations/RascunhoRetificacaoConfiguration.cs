namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Domain.Entities;

public sealed class RascunhoRetificacaoConfiguration : IEntityTypeConfiguration<RascunhoRetificacao>
{
    /// <summary>
    /// O índice que <b>garante</b> a unicidade da sessão editorial — não a checagem em
    /// memória, que perde a corrida entre duas aberturas concorrentes que leram o agregado
    /// antes de qualquer uma gravar.
    /// </summary>
    public const string IndiceUnicoPorProcesso = "ux_rascunhos_retificacao_processo";

    public void Configure(EntityTypeBuilder<RascunhoRetificacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("rascunhos_retificacao");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.ProcessoSeletivoId).IsRequired();

        // O teto da coluna espelha RascunhoRetificacao.MotivoMaxLength, que já é o MENOR
        // dos dois limites que o motivo atravessa (o de Publicações, no fechamento). O
        // domínio recusa antes de chegar aqui — a coluna é o backstop, não a regra.
        builder.Property(r => r.Motivo)
            .HasMaxLength(RascunhoRetificacao.MotivoMaxLength)
            .IsRequired();

        builder.Property(r => r.VersaoBaseId).IsRequired();
        builder.Property(r => r.NumeroVersaoBase).IsRequired();
        builder.Property(r => r.AbertoEm).IsRequired();
        builder.Property(r => r.AbertoPorSub).HasMaxLength(255).IsRequired();
        builder.Property(r => r.Revisao).IsRequired();

        // UNIQUE simples, não parcial: o rascunho é APAGADO no fechamento e no descarte —
        // não há histórico de sessões encerradas convivendo na tabela, e portanto nada a
        // filtrar. Um índice parcial aqui sugeriria um estado "sessão morta" que não
        // existe.
        builder.HasIndex(r => r.ProcessoSeletivoId)
            .IsUnique()
            .HasDatabaseName(IndiceUnicoPorProcesso);
    }
}
