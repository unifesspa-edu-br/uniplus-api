namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class EnderecoDaInscricaoRecordConfiguration : IEntityTypeConfiguration<EnderecoDaInscricaoRecord>
{
    public void Configure(EntityTypeBuilder<EnderecoDaInscricaoRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Sensivel);

        builder.Property(e => e.InscricaoId).HasComment("Identificador da inscrição à qual pertence.");
        builder.Property(e => e.OcorrenciaId).HasComment("Identificador da ocorrência de grupo, quando a resposta pertence a um grupo repetível.");
        builder.Property(e => e.FatoCodigo).HasComment("Código do dado do candidato. Exemplos: RENDA_PER_CAPITA, CONCORRER_RENDA.");
        builder.Property(e => e.Origem).HasComment("Origem da resposta: INFORMADA pela pessoa candidata ou CALCULADA pelo sistema.");
        builder.Property(e => e.Cep).HasComment("Código postal do endereço, quando informado.");
        builder.Property(e => e.Logradouro).HasComment("Nome do logradouro, quando informado.");
        builder.Property(e => e.Numero).HasComment("Número do endereço, quando informado.");
        builder.Property(e => e.Complemento).HasComment("Complemento do endereço, quando informado.");
        builder.Property(e => e.Bairro).HasComment("Bairro do endereço, quando informado.");
        builder.Property(e => e.Distrito).HasComment("Distrito do endereço, quando informado.");
        builder.Property(e => e.MunicipioCodigoIbge).HasComment("Código IBGE do município, quando informado.");
        builder.Property(e => e.MunicipioNome).HasComment("Nome do município, quando informado.");
        builder.Property(e => e.Uf).HasComment("Sigla da unidade federativa, quando informada.");
        builder.Property(e => e.Latitude).HasComment("Latitude do endereço, quando disponível.");
        builder.Property(e => e.Longitude).HasComment("Longitude do endereço, quando disponível.");
        builder.Property(e => e.NivelResolucao).HasComment("Precisão da localização obtida para o endereço, quando disponível.");

        builder.ToTable("enderecos_da_inscricao", t =>
        {
            t.HasComment("A resposta de um campo de endereço da inscrição, um componente por coluna.");
            t.HasCheckConstraint("ck_enderecos_da_inscricao_origem", $"origem IN ({SqlDosCodigosDeContrato.Lista<OrigemDaResposta>()})");
        });

        // Cada componente do endereço é valor de um fato cuja classificação real vem do fato coletado da
        // linha, resolvida pelo projetor: valem a mais alta, o padrão do tipo. Só a chave e o metadado são
        // Interno, para que uma coluna nova herde o padrão seguro.
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ClassificaComo(ClassificacaoDeProtecao.Interno).ValueGeneratedNever();
        builder.Property(e => e.InscricaoId).ClassificaComo(ClassificacaoDeProtecao.Interno);
        builder.Property(e => e.OcorrenciaId).ClassificaComo(ClassificacaoDeProtecao.Interno);

        builder.Property(e => e.FatoCodigo).HasColumnName("codigo_do_dado").ClassificaComo(ClassificacaoDeProtecao.Interno).HasMaxLength(FormaDoItem.FatoCodigoMaxLength).IsRequired();
        builder.Property(e => e.Origem).ClassificaComo(ClassificacaoDeProtecao.Interno).HasMaxLength(9).IsRequired().HasComment("Origem da resposta.");
        builder.Property(e => e.Procedencia)
            .HasComment("Como o endereço foi preenchido no formulário: pela busca do CEP no Geo ou à mão.");

        builder.Property(e => e.CepConfirmado)
            .HasComment("O candidato confirmou que o CEP é o geral da cidade ou do bairro, quando o Geo só o resolveu por faixa.");

        builder.HasOne<InscricaoRecord>()
            .WithMany()
            .HasForeignKey(e => e.InscricaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<OcorrenciaDeGrupoDaInscricaoRecord>()
            .WithMany()
            .HasForeignKey(e => e.OcorrenciaId)
            .HasConstraintName("fk_enderecos_da_inscricao_ocorrencia_de_grupo_da_inscricao")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.InscricaoId, e.OcorrenciaId, e.FatoCodigo })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("ux_enderecos_da_inscricao_dado");
    }
}
