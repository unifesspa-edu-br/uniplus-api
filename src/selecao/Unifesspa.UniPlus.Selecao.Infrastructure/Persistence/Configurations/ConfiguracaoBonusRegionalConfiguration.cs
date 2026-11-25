namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Configuração EF Core de <see cref="ConfiguracaoBonusRegional"/> (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015) — entidade 0..1 do agregado <see cref="ProcessoSeletivo"/>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ConfiguracaoBonusRegionalConfiguration : IEntityTypeConfiguration<ConfiguracaoBonusRegional>
{
    private const int TipoInstrumentoMaxLength = 30;
    private const int CodigoBaseLegalMaxLength = 100;
    private const int IdentificacaoMaxLength = 500;
    private const int DescricaoMaxLength = 2000;

    public void Configure(EntityTypeBuilder<ConfiguracaoBonusRegional> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("configuracoes_do_fada", t => t.HasComment("Configurações do Fator de Atenuação das Desigualdades de Acesso (FADA) nos processos seletivos."));
        builder.HasKey(b => b.Id).HasName("pk_configuracoes_do_fada");
        builder.Property(b => b.Id).ValueGeneratedNever().HasComment("Identificador da configuração do Fator de Atenuação das Desigualdades de Acesso (FADA).");
        builder.Property(b => b.ProcessoSeletivoId).HasComment("Identificador do processo seletivo que aplica o Fator de Atenuação das Desigualdades de Acesso (FADA).");
        builder.HasIndex(b => b.ProcessoSeletivoId).IsUnique().HasDatabaseName("ux_configuracoes_do_fada_processo_seletivo_id");

        builder.OwnsOne(b => b.Regra, regra =>
        {
            regra.ConfigurarReferenciaRegra("regra");
            regra.Property(r => r.Codigo).HasComment("Código da regra do Fator de Atenuação das Desigualdades de Acesso (FADA).");
            regra.Property(r => r.Versao).HasComment("Versão da regra do Fator de Atenuação das Desigualdades de Acesso (FADA).");
            regra.Property(r => r.Hash).HasComment("Resumo que identifica o conteúdo da regra do Fator de Atenuação das Desigualdades de Acesso (FADA).");
        });
        builder.Navigation(b => b.Regra).IsRequired();

        builder.Property(b => b.Fator).HasComment("Multiplicador do Fator de Atenuação das Desigualdades de Acesso (FADA).").HasPrecision(6, 4).IsRequired();
        builder.Property(b => b.Teto).HasComment("Limite da nota após o Fator de Atenuação das Desigualdades de Acesso (FADA), quando definido.").HasPrecision(6, 4);
        builder.Property(b => b.FatoElegibilidadeCodigo).HasColumnName("codigo_do_dado_de_elegibilidade").HasComment("Código do dado do candidato que decide a elegibilidade ao Fator de Atenuação das Desigualdades de Acesso (FADA), quando definido.").HasMaxLength(FormaDoItem.FatoCodigoMaxLength);

        builder.Property(b => b.BaseLegalBonusRegionalId).HasColumnName("base_legal_id").HasComment("Identificador da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) copiado na configuração.").IsRequired();
        builder.Property(b => b.BaseLegalBonusRegionalCodigo).HasColumnName("base_legal_codigo").HasComment("Código da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) copiado na configuração.").HasMaxLength(CodigoBaseLegalMaxLength).IsRequired();
        builder.Property(b => b.TipoInstrumento).HasComment("Tipo do instrumento do Fator de Atenuação das Desigualdades de Acesso (FADA); copiado da base legal.").HasMaxLength(TipoInstrumentoMaxLength).IsRequired();
        builder.Property(b => b.Identificacao).HasComment("Identificação do instrumento do Fator de Atenuação das Desigualdades de Acesso (FADA), copiada da base legal.").HasMaxLength(IdentificacaoMaxLength).IsRequired();
        builder.Property(b => b.Descricao).HasComment("Descrição da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA), copiada na configuração.").HasMaxLength(DescricaoMaxLength).IsRequired();

        builder.HasMany(b => b.Municipios)
            .WithOne()
            .HasForeignKey(m => m.ConfiguracaoBonusRegionalId)
            .HasConstraintName("fk_municipios_do_fada_configuracao_do_fada_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.Municipios)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
