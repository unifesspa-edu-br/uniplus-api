namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

internal sealed class RespostaDaInscricaoRecordConfiguration : IEntityTypeConfiguration<RespostaDaInscricaoRecord>
{
    public void Configure(EntityTypeBuilder<RespostaDaInscricaoRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        ConfigurarPropriedades(builder);
        ConfigurarTabela(builder);
        ConfigurarChaveEProtecao(builder);
        ConfigurarValorCifrado(builder);
        ConfigurarRelacionamentos(builder);
        ConfigurarIndices(builder);
    }

    private static void ConfigurarPropriedades(EntityTypeBuilder<RespostaDaInscricaoRecord> builder)
    {
        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Sensivel);

        builder.Property(r => r.InscricaoId).HasComment("Identificador da inscrição à qual pertence.");
        builder.Property(r => r.OcorrenciaId).HasComment("Identificador da ocorrência de grupo, quando a resposta pertence a um grupo repetível.");
        builder.Property(r => r.Posicao).HasComment("Posição do valor em uma resposta com vários valores; nula para valor único.");
        builder.Property(r => r.FatoCodigo).HasComment("Código do dado do candidato. Exemplos: RENDA_PER_CAPITA, CONCORRER_RENDA.");
        builder.Property(r => r.ValorBooleano).HasColumnName("valor_sim_ou_nao").HasComment("Resposta de sim ou não, quando aplicável.");
        builder.Property(r => r.ValorNumero).HasComment("Resposta numérica, quando aplicável.");
        builder.Property(r => r.ValorCodigo).HasComment("Código da opção ou município respondido, quando aplicável.");
        builder.Property(r => r.ValorTexto).HasComment("Resposta em texto livre, quando aplicável.");
        builder.Property(r => r.ValorData).HasComment("Resposta de data, quando aplicável.");
    }

    private static void ConfigurarTabela(EntityTypeBuilder<RespostaDaInscricaoRecord> builder)
    {
        builder.ToTable("respostas_da_inscricao", t =>
        {
            t.HasComment("Um valor respondido na inscrição, na coluna do tipo do dado, com a origem: informado pelo candidato ou calculado a partir de outra resposta.");
            t.HasCheckConstraint("ck_respostas_da_inscricao_valor_do_tipo", SqlDaRestricaoDoValor);
            t.HasCheckConstraint("ck_respostas_da_inscricao_origem", $"origem IN ({SqlDosCodigosDeContrato.Lista<OrigemDaResposta>()})");
        });
    }

    private static string SqlDaRestricaoDoValor =>
        $"num_nonnulls(valor_sim_ou_nao, valor_numero, valor_codigo, valor_texto, valor_data, valor_cifrado) = 1 AND tipo IN ({SqlDosCodigosDeContrato.Literal(TipoDoValorDeResposta.SimOuNao)}, {SqlDosCodigosDeContrato.Literal(TipoDoValorDeResposta.Numero)}, {SqlDosCodigosDeContrato.Literal(TipoDoValorDeResposta.Opcao)}, {SqlDosCodigosDeContrato.Literal(TipoDoValorDeResposta.Texto)}, {SqlDosCodigosDeContrato.Literal(TipoDoValorDeResposta.Data)}, {SqlDosCodigosDeContrato.Literal(TipoDoValorDeResposta.Municipio)}) AND ("
        + "valor_cifrado IS NOT NULL"
        + $" OR (tipo = {SqlDosCodigosDeContrato.Literal(TipoDoValorDeResposta.SimOuNao)} AND valor_sim_ou_nao IS NOT NULL)"
        + $" OR (tipo = {SqlDosCodigosDeContrato.Literal(TipoDoValorDeResposta.Numero)} AND valor_numero IS NOT NULL)"
        + $" OR (tipo = {SqlDosCodigosDeContrato.Literal(TipoDoValorDeResposta.Opcao)} AND valor_codigo IS NOT NULL)"
        + $" OR (tipo = {SqlDosCodigosDeContrato.Literal(TipoDoValorDeResposta.Texto)} AND valor_texto IS NOT NULL)"
        + $" OR (tipo = {SqlDosCodigosDeContrato.Literal(TipoDoValorDeResposta.Data)} AND valor_data IS NOT NULL)"
        + $" OR (tipo = {SqlDosCodigosDeContrato.Literal(TipoDoValorDeResposta.Municipio)} AND valor_codigo IS NOT NULL))"
        + $" AND (valor_rotulo IS NULL OR (tipo = {SqlDosCodigosDeContrato.Literal(TipoDoValorDeResposta.Municipio)} AND valor_codigo IS NOT NULL))";

    private static void ConfigurarChaveEProtecao(EntityTypeBuilder<RespostaDaInscricaoRecord> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ClassificaComo(ClassificacaoDeProtecao.Interno).ValueGeneratedNever();
        builder.Property(r => r.InscricaoId).ClassificaComo(ClassificacaoDeProtecao.Interno);
        builder.Property(r => r.OcorrenciaId).ClassificaComo(ClassificacaoDeProtecao.Interno);
        builder.Property(r => r.Posicao).ClassificaComo(ClassificacaoDeProtecao.Interno);

        builder.Property(r => r.FatoCodigo).HasColumnName("codigo_do_dado").ClassificaComo(ClassificacaoDeProtecao.Interno).HasMaxLength(FormaDoItem.FatoCodigoMaxLength).IsRequired();
        builder.Property(r => r.Origem).ClassificaComo(ClassificacaoDeProtecao.Interno).HasMaxLength(9).IsRequired().HasComment("Origem da resposta.");
        builder.Property(r => r.Tipo).ClassificaComo(ClassificacaoDeProtecao.Interno).HasMaxLength(10).IsRequired().HasComment("Tipo do valor da resposta. O tipo ENDERECO não é gravado aqui, porque o endereço tem registro próprio.");
        builder.Property(r => r.ValorCodigo).HasMaxLength(OpcaoDeclaradaFato.CodigoMaxLength);
        builder.Property(r => r.ValorRotulo)
            .HasComment("O nome que o Geo conferiu para o município de valor_codigo; nulo no rascunho e preenchido na inscrição enviada.");
    }

    private static void ConfigurarValorCifrado(EntityTypeBuilder<RespostaDaInscricaoRecord> builder)
    {
        // As colunas de valor guardam valores de fatos de classes diferentes, cuja classificação real é a do
        // fato coletado da linha, resolvida pelo projetor: valem a mais alta, o padrão do tipo. Só a chave e
        // o metadado são Interno, para que uma coluna nova herde o padrão seguro.
        builder.Property(r => r.ValorCifrado)
            .HasColumnType("bytea")
            .HasComment("Resposta cifrada (AES-GCM, ADR-0121) — nunca texto claro: o número de documento, com a chave dos identificadores, ou o dado sensível de cifra individual, com a chave dos sensíveis.");
    }

    private static void ConfigurarRelacionamentos(EntityTypeBuilder<RespostaDaInscricaoRecord> builder)
    {
        builder.HasOne<InscricaoRecord>()
            .WithMany()
            .HasForeignKey(r => r.InscricaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<OcorrenciaDeGrupoDaInscricaoRecord>()
            .WithMany()
            .HasForeignKey(r => r.OcorrenciaId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigurarIndices(EntityTypeBuilder<RespostaDaInscricaoRecord> builder)
    {
        builder.HasIndex(r => new { r.InscricaoId, r.OcorrenciaId, r.FatoCodigo, r.Posicao })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("ux_respostas_da_inscricao_valor");

        ConfigurarIndicesPorValor(builder);
    }

    private static void ConfigurarIndicesPorValor(EntityTypeBuilder<RespostaDaInscricaoRecord> builder)
    {
        builder.HasIndex(r => new { r.FatoCodigo, r.ValorCodigo })
            .HasFilter("valor_codigo IS NOT NULL")
            .HasDatabaseName("ix_respostas_da_inscricao_dado_codigo");
        builder.HasIndex(r => new { r.FatoCodigo, r.ValorBooleano })
            .HasFilter("valor_sim_ou_nao IS NOT NULL")
            .HasDatabaseName("ix_respostas_da_inscricao_dado_sim_ou_nao");
        builder.HasIndex(r => new { r.FatoCodigo, r.ValorNumero })
            .HasFilter("valor_numero IS NOT NULL")
            .HasDatabaseName("ix_respostas_da_inscricao_dado_numero");
        builder.HasIndex(r => new { r.FatoCodigo, r.ValorData })
            .HasFilter("valor_data IS NOT NULL")
            .HasDatabaseName("ix_respostas_da_inscricao_dado_data");
    }
}
