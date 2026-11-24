namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Configuração EF Core de <see cref="GrupoColetado"/> (UNI-REQ-0146), filha de
/// <c>ProcessoSeletivo</c> substituída por inteiro com ele, dona dos seus campos. A exibição e a
/// obrigatoriedade são lidas e gravadas sempre inteiras, junto do grupo, por isso ficam em <c>jsonb</c>.
/// </summary>
public sealed class GrupoColetadoConfiguration : IEntityTypeConfiguration<GrupoColetado>
{
    public void Configure(EntityTypeBuilder<GrupoColetado> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.Property(g => g.ProcessoSeletivoId).HasComment("Identificador do processo seletivo ao qual pertence.");
        builder.Property(g => g.Codigo).HasComment("Código que identifica o item no processo.");
        builder.Property(g => g.Ordem).HasComment("Posição de apresentação ou avaliação no conjunto.");
        builder.Property(g => g.EtapaCodigo).HasComment("Código da etapa do formulário, quando houver.");
        builder.Property(g => g.Rotulo).HasComment("Texto apresentado à pessoa candidata.");
        builder.Property(g => g.Minimo).HasComment("Quantidade mínima de ocorrências do grupo.");
        builder.Property(g => g.Maximo).HasComment("Quantidade máxima de ocorrências do grupo, quando limitada.");
        builder.Property(g => g.IncluiCandidato).HasComment("Indica se o grupo inclui a própria pessoa candidata.");
        builder.Property(g => g.Exibicao).HasComment("Condições para exibir o grupo, em JSON, quando houver.");
        builder.Property(g => g.Obrigatoriedade).HasComment("Regra que determina quando a resposta é obrigatória, em JSON.");

        builder.ToTable("grupos_dos_formularios", t =>
        {
            t.HasComment("Grupos repetíveis de dados solicitados nos formulários.");
            t.HasCheckConstraint("ck_grupos_dos_formularios_finalidade", $"finalidade IN ({SqlDosCodigosDeContrato.Lista<FinalidadeFormulario>()})");
        });
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();

        builder.Property(g => g.Finalidade).HasMaxLength(TamanhoDoCodigoDeContrato.Maximo).IsRequired().HasComment($"Finalidade do formulário. {ComentarioDosCodigosDeContrato.Codigos<FinalidadeFormulario>()}");
        builder.Property(g => g.Codigo).HasMaxLength(FormaDoGrupo.CodigoMaxLength).IsRequired();
        builder.Property(g => g.Ordem).IsRequired();
        builder.Property(g => g.EtapaCodigo).HasMaxLength(FormaDaEtapa.CodigoMaxLength);
        builder.Property(g => g.Rotulo).HasMaxLength(FormaDoItem.RotuloMaxLength).IsRequired();
        builder.Property(g => g.Minimo).IsRequired();
        builder.Property(g => g.Maximo);
        builder.Property(g => g.IncluiCandidato).IsRequired();
        builder.Property(g => g.Exibicao)
            .HasConversion(ConversoresDeRegras.Predicado, ConversoresDeRegras.ComparadorDePredicado)
            .HasColumnType("jsonb");
        builder.Property(g => g.Obrigatoriedade)
            .HasConversion(ConversoresDeRegras.Obrigatoriedade, ConversoresDeRegras.ComparadorDeObrigatoriedade)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Ignore(g => g.Ocorrencias);
        builder.Ignore(g => g.Regras);
        builder.Ignore(g => g.FatosCitados);
        builder.Ignore(g => g.Condicoes);

        // O código do grupo é único no processo, como o do fato; a ordem que ele compartilha com os
        // itens do formulário é invariante do agregado, conferida em DefinirFatosColetados.
        builder.HasIndex(g => new { g.ProcessoSeletivoId, g.Codigo })
            .IsUnique()
            .HasDatabaseName("ux_grupos_dos_formularios_processo_codigo");

        builder.HasMany(g => g.Subitens)
            .WithOne()
            .HasForeignKey(f => f.GrupoColetadoId)
            .HasConstraintName("fk_dados_dos_formularios_grupo_formulario")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(g => g.Subitens)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
