namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Configuração EF Core de <see cref="ProdutoDaFase"/> — filha 0..* de
/// <see cref="FaseCronograma"/>. Os limites de comprimento espelham
/// <c>LimitesDoEnvelope</c> (constantes duplicadas por convenção do repo, verificadas
/// pelo fitness <c>LimitesDoEnvelopeBatemComOSchemaTests</c>).
/// </summary>
public sealed class ProdutoDaFaseConfiguration : IEntityTypeConfiguration<ProdutoDaFase>
{
    private const int AtoCodigoMaxLength = 60;

    public void Configure(EntityTypeBuilder<ProdutoDaFase> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("publicacoes_previstas_da_fase", tabela =>
        {
            tabela.HasComment("Publicações previstas para uma fase do cronograma.");
            tabela.HasCheckConstraint("ck_publicacoes_previstas_da_fase_papel", $"papel IS NULL OR papel IN ({SqlDosCodigosDeContrato.Lista<PapelDaPublicacaoPrevista>()})");
        });
        builder.Property(p => p.FaseCronogramaId).HasComment("Identificador do registro ao qual esta configuração pertence.");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasComment("Identificador do registro.").ValueGeneratedNever();

        builder.Property(p => p.AtoCodigo).HasComment("Código do ato previsto para publicação.").HasMaxLength(AtoCodigoMaxLength).IsRequired();
        builder.Property(p => p.Papel).HasMaxLength(TamanhoDoCodigoDeContrato.Maximo).HasComment("Papel da publicação no resultado, quando aplicável: PRELIMINAR ou DEFINITIVO.");

        // O par ato + papel declarado uma única vez por fase — a mesma matéria é publicada
        // uma vez como preliminar e outra como definitiva, então o ato sozinho não é chave.
        // É a chave de reconciliação de FaseCronograma.AtualizarDe e a chave de
        // round-trip do envelope — a rejeição no domínio é check-then-act não-atômico, e
        // esta constraint é a defesa atômica.
        //
        // `AreNullsDistinct(false)` porque o papel é nulo em ato que o catálogo não marca
        // como resultado: sem isso o Postgres trataria cada nulo como valor próprio e o
        // índice deixaria passar duas publicações idênticas do mesmo comunicado.
        builder.HasIndex(p => new { p.FaseCronogramaId, p.AtoCodigo, p.Papel })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("ux_publicacoes_previstas_da_fase_ato");
    }
}
