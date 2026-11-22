namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Configurations;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

internal sealed class LocalOfertaConfiguration : IEntityTypeConfiguration<LocalOferta>
{
    private const string NomeDaTabela = "locais_de_oferta";
    private const int CodigoDeContratoMaxLength = 30;

    public void Configure(EntityTypeBuilder<LocalOferta> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.ToTable(NomeDaTabela, ConfigurarChecks);
        builder.HasKey(l => l.Id);
        ConfigurarIdentificacao(builder);
        ConfigurarCidade(builder);
        ConfigurarEndereco(builder);
        ConfigurarAuditoria(builder);
        ConfigurarCampusResponsavel(builder);
        ConfigurarIndices(builder);
    }

    // CHECK de coerência cidade↔CEP (CA-04, ADR-0096): NULL-safe. O tipo é conferido contra os códigos de contrato.
    private static void ConfigurarChecks(TableBuilder<LocalOferta> tabela)
    {
        tabela.HasCheckConstraint(
            "ck_locais_de_oferta_tipo",
            $"tipo IN ({SqlDosCodigosDeContrato.Lista<TipoLocalOferta>()})");
        tabela.HasCheckConstraint(
            EnderecoGeoOwnedConfiguration.CoerenciaCidadeCheckName(NomeDaTabela),
            EnderecoGeoOwnedConfiguration.CoerenciaCidadeCheckSql);
        tabela.HasCheckConstraint(
            EnderecoGeoOwnedConfiguration.CompletudeCheckName(NomeDaTabela),
            EnderecoGeoOwnedConfiguration.CompletudeCheckSql);
    }

    private static void ConfigurarIdentificacao(EntityTypeBuilder<LocalOferta> builder)
    {
        builder.Property(l => l.Tipo).HasMaxLength(CodigoDeContratoMaxLength).IsRequired();
        builder.Property(l => l.CodigoEmec).HasMaxLength(20);
    }

    // Referência de cidade do Geo (ADR-0090): código + nome copiado, sem FK
    // cross-banco para uniplus_geo.
    private static void ConfigurarCidade(EntityTypeBuilder<LocalOferta> builder)
    {
        builder.Property(l => l.CidadeCodigoIbge)
            .HasMaxLength(ReferenciaCidadeGeo.CodigoIbgeLength)
            .IsFixedLength()
            .IsRequired();
        builder.Property(l => l.CidadeNome).HasMaxLength(ReferenciaCidadeGeo.NomeMaxLength).IsRequired();
        builder.Property(l => l.CidadeUf)
            .HasMaxLength(ReferenciaCidadeGeo.UfLength)
            .IsFixedLength()
            .IsRequired();
        builder.Property(l => l.CidadeOrigem).HasMaxLength(ReferenciaCidadeGeo.OrigemMaxLength);
        builder.Property(l => l.CidadeDisplayAtualizadoEm);
    }

    // Endereço estruturado ao Geo via CEP (ADR-0096): owned type opcional.
    private static void ConfigurarEndereco(EntityTypeBuilder<LocalOferta> builder)
    {
        builder.OwnsOne(l => l.Endereco, endereco => EnderecoGeoOwnedConfiguration.Configure(endereco, ClassificacaoDeProtecao.Interno));
        builder.Navigation(l => l.Endereco).IsRequired(false);
    }

    private static void ConfigurarAuditoria(EntityTypeBuilder<LocalOferta> builder)
    {
        builder.Property(l => l.CreatedBy).HasMaxLength(255);
        builder.Property(l => l.UpdatedBy).HasMaxLength(255);
    }

    // Campus responsável: FK intra-banco opcional (ADR-0065). Restrict no banco
    // — a remoção lógica do Campus é barrada pelo handler
    // (CampusErrorCodes.RemocaoBloqueadaPorLocalOferta); o RESTRICT cobre o
    // DELETE físico residual.
    private static void ConfigurarCampusResponsavel(EntityTypeBuilder<LocalOferta> builder)
    {
        builder.HasOne<Campus>()
            .WithMany()
            .HasForeignKey(l => l.CampusResponsavelId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurarIndices(EntityTypeBuilder<LocalOferta> builder)
    {
        builder.HasIndex(l => l.CampusResponsavelId)
            .HasDatabaseName("ix_locais_de_oferta_campus_responsavel_id");

        // Índice de relatório/filtro pela cidade (ADR-0090).
        builder.HasIndex(l => l.CidadeCodigoIbge)
            .HasDatabaseName("ix_locais_de_oferta_cidade_codigo_ibge");
    }
}
