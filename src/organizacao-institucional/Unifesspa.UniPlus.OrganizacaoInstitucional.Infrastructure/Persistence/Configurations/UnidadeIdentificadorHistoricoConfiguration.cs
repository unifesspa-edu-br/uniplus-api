namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class UnidadeIdentificadorHistoricoConfiguration
    : IEntityTypeConfiguration<UnidadeIdentificadorHistorico>
{
    private const int TipoMaxLength = 40;

    public void Configure(EntityTypeBuilder<UnidadeIdentificadorHistorico> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable(
            "identificadores_da_unidade",
            t => t.HasCheckConstraint(
                "ck_identificadores_da_unidade_tipo",
                $"tipo IN ({SqlDosCodigosDeContrato.Lista<TipoIdentificador>()})"));
        builder.HasKey(h => h.Id);

        // EntityBase gera UUIDv7 no cliente. Sem explicitar isso, o EF trata
        // novas entradas adicionadas a uma Unidade já rastreada como existentes
        // e emite UPDATE em vez de INSERT ao fechar/abrir histórico.
        builder.Property(h => h.Id).ValueGeneratedNever();

        builder.Property(h => h.UnidadeId).IsRequired();
        builder.Property(h => h.TipoIdentificador)
            .HasColumnName("tipo")
            .HasMaxLength(TipoMaxLength)
            .HasComment("Tipo do identificador cujo histórico a linha registra. Códigos: IDENTIFICADOR_NO_ENDERECO_WEB, SIGLA, CODIGO, APELIDO.")
            .IsRequired();
        builder.Property(h => h.Valor).HasMaxLength(100).IsRequired();
        builder.Property(h => h.VigenciaInicio).IsRequired();
        builder.Property(h => h.VigenciaFim);
        builder.Property(h => h.MotivoMudanca).HasMaxLength(500);

        // Índice para consultas históricas por unidade + tipo + período
        builder.HasIndex(h => new { h.UnidadeId, h.TipoIdentificador, h.VigenciaInicio })
            .HasDatabaseName("ix_identificadores_da_unidade_unidade_tipo_inicio");

        // Histórico é append-only — sem query filter de soft-delete
    }
}
