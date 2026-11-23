namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

internal static class ProvaPresencialOwnedConfiguration
{
    internal static void ConfigurarProvaPresencial(this OwnedNavigationBuilder<ProcessoSeletivo, DeclaracaoDaProvaPresencial> prova)
    {
        ArgumentNullException.ThrowIfNull(prova);
        prova.ClassificaPorPadrao(ClassificacaoDeProtecao.Publico);
        prova.Property(d => d.TemProvaPresencial).HasColumnName("tem_prova_presencial")
            .HasComment("Declara se o processo tem prova presencial. Nulo = ainda não declarado.");
        prova.OwnsMany(d => d.Municipios, ConfigurarMunicipios);
        prova.Navigation(d => d.Municipios).HasField("_municipios").UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigurarMunicipios(OwnedNavigationBuilder<DeclaracaoDaProvaPresencial, MunicipioDeProva> municipio)
    {
        municipio.ClassificaPorPadrao(ClassificacaoDeProtecao.Publico);
        municipio.ToTable("municipios_de_prova", ConfigurarTabela);
        municipio.WithOwner().HasForeignKey("ProcessoSeletivoId").HasConstraintName("fk_municipios_de_prova_processo_seletivo");
        municipio.Property<int>("Id").ValueGeneratedOnAdd().HasComment("Identificador técnico da linha, substituída com a declaração.");
        municipio.HasKey("Id");
        municipio.Property<Guid>("ProcessoSeletivoId").HasComment("Processo seletivo que declara o município de prova.");
        ConfigurarColunasDoMunicipio(municipio);
        municipio.HasIndex("ProcessoSeletivoId", nameof(MunicipioDeProva.Posicao)).IsUnique().HasDatabaseName("ix_municipios_de_prova_processo_posicao");
        municipio.HasIndex("ProcessoSeletivoId", nameof(MunicipioDeProva.CodigoIbge)).IsUnique().HasDatabaseName("ix_municipios_de_prova_processo_codigo_ibge");
    }

    private static void ConfigurarColunasDoMunicipio(OwnedNavigationBuilder<DeclaracaoDaProvaPresencial, MunicipioDeProva> municipio)
    {
        municipio.Property(m => m.CodigoIbge).HasColumnName("codigo_ibge").HasMaxLength(7).IsFixedLength().IsRequired()
            .HasComment("Código do IBGE do município de prova da prova.");
        municipio.Property(m => m.Uf).HasColumnName("uf").HasMaxLength(2).IsFixedLength().IsRequired()
            .HasComment("UF do município de prova da prova.");
        municipio.Property(m => m.Nome).HasColumnName("nome").HasMaxLength(ReferenciaCidadeGeo.NomeMaxLength)
            .HasComment("Nome copiado do Geo na última publicação ou retificação; nulo antes da primeira.");
        municipio.Property(m => m.Posicao).HasColumnName("posicao").HasComment("Posição do município na declaração, a partir de 1.");
    }

    private static void ConfigurarTabela(OwnedNavigationTableBuilder<DeclaracaoDaProvaPresencial, MunicipioDeProva> tabela)
    {
        tabela.HasComment("Municípios de prova da prova presencial, na ordem declarada pelo processo seletivo.");
        tabela.HasCheckConstraint("ck_municipios_de_prova_codigo_ibge", "codigo_ibge ~ '^[0-9]{7}$'");
        tabela.HasCheckConstraint("ck_municipios_de_prova_posicao", "posicao >= 1");
    }
}
