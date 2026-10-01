namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Regras.Formularios;


/// <summary>
/// Configuração EF Core de <see cref="TermoExigidoFormulario"/> (UNI-REQ-0086), filha de
/// <c>ProcessoSeletivo</c> substituída por inteiro com ele. A exibição e a obrigatoriedade são
/// lidas e gravadas sempre inteiras, junto do termo, por isso ficam em <c>jsonb</c>.
/// </summary>
public sealed class TermoExigidoFormularioConfiguration : IEntityTypeConfiguration<TermoExigidoFormulario>
{
    private const int NomeMaxLength = 200;
    private const int TextoMaxLength = 20_000;
    private const int BaseLegalMaxLength = 500;
    private const int FormaAceiteMaxLength = 40;
    private const int HashMaxLength = 64;

    public void Configure(EntityTypeBuilder<TermoExigidoFormulario> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("termos_exigidos_formulario");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Codigo).HasMaxLength(FormaDoTermo.CodigoMaxLength).IsRequired();
        builder.Property(t => t.Ordem).IsRequired();
        builder.Property(t => t.Finalidade).HasConversion<int>().IsRequired();
        builder.Property(t => t.TermoId).IsRequired();
        builder.Property(t => t.VersaoId).IsRequired();
        builder.Property(t => t.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(t => t.Texto).HasMaxLength(TextoMaxLength).IsRequired();
        builder.Property(t => t.BaseLegal).HasMaxLength(BaseLegalMaxLength).IsRequired();
        builder.Property(t => t.FormaAceite).HasMaxLength(FormaAceiteMaxLength).IsRequired();
        builder.Property(t => t.HashVersao).HasMaxLength(HashMaxLength).IsRequired();

        builder.Property(t => t.Exibicao)
            .HasConversion(ConversoresDeRegras.Predicado, ConversoresDeRegras.ComparadorDePredicado)
            .HasColumnType("jsonb");
        builder.Property(t => t.Obrigatoriedade)
            .HasConversion(ConversoresDeRegras.Obrigatoriedade, ConversoresDeRegras.ComparadorDeObrigatoriedade)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Ignore(t => t.SemFormaDeAceite);
        builder.Ignore(t => t.Condicoes);
        builder.Ignore(t => t.FatosCitados);

        // Invariantes do agregado, garantidas também contra escrita concorrente.
        builder.HasIndex(t => new { t.ProcessoSeletivoId, t.Finalidade, t.Codigo })
            .IsUnique()
            .HasDatabaseName("ux_termos_exigidos_formulario_processo_finalidade_codigo");
        builder.HasIndex(t => new { t.ProcessoSeletivoId, t.Finalidade, t.Ordem })
            .IsUnique()
            .HasDatabaseName("ux_termos_exigidos_formulario_processo_finalidade_ordem");
    }
}
