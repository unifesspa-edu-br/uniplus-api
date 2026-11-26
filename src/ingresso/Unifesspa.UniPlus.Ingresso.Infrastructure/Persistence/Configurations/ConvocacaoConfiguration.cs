namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence.Configurations;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Configurations;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class ConvocacaoConfiguration : IEntityTypeConfiguration<Convocacao>
{
    private const int CodigoDeContratoMaxLength = 30;
    private const string IndiceProtocolo = "ux_convocacoes_protocolo";

    public void Configure(EntityTypeBuilder<Convocacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        ConfigurarTabela(builder);
        ComentarColunas(builder);
        builder.HasKey(c => c.Id);

        ConfigurarProtocolo(builder);
        builder.Property(c => c.Status)
            .HasMaxLength(CodigoDeContratoMaxLength)
            .HasComment("Situação da convocação.")
            .IsRequired();
        builder.Property(c => c.Posicao).IsRequired();
        ConfigurarOferta(builder);
    }

    private static void ConfigurarProtocolo(EntityTypeBuilder<Convocacao> builder)
    {
        builder.OwnsOne(c => c.Protocolo, protocolo =>
        {
            protocolo.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);
            protocolo.Property(p => p.Valor).HasColumnName("protocolo").HasMaxLength(50).HasComment("Identificador atribuído à convocação para acompanhamento.").IsRequired();
            protocolo.HasIndex(p => p.Valor).IsUnique().HasDatabaseName(IndiceProtocolo);
        });
    }

    private static void ConfigurarOferta(EntityTypeBuilder<Convocacao> builder)
    {
        builder.OwnsOne(c => c.OfertaCurso, oferta => CopiaDaOfertaDeCursoOwnedConfiguration.Configure(
            oferta, "da oferta congelada na versão do processo seletivo que a chamada segue, na criação da convocação", ClassificacaoDeProtecao.Pessoal));
        builder.Navigation(c => c.OfertaCurso).IsRequired();
    }

    private static void ConfigurarTabela(EntityTypeBuilder<Convocacao> builder)
    {
        builder.ToTable("convocacoes", static tabela =>
        {
            tabela.HasComment("Convocações de candidatos para chamadas.");
            tabela.HasCheckConstraint("ck_convocacoes_status", $"status IN ({SqlDosCodigosDeContrato.Lista<StatusConvocacao>()})");
            CopiaDaOfertaDeCursoOwnedConfiguration.ConfigurarRestricao(tabela, "convocacoes");
        });
    }

    private static void ComentarColunas(EntityTypeBuilder<Convocacao> builder)
    {
        builder.Property(c => c.Id).HasComment("Identificador do registro.");
        builder.Property(c => c.ChamadaId).HasComment("Identificador da chamada.");
        builder.Property(c => c.InscricaoId).HasComment("Identificador da inscrição.");
        builder.Property(c => c.CandidatoId).HasComment("Identificador do candidato.");
        builder.Property(c => c.DataManifestacao).HasComment("Instante da manifestação.");
        builder.Property(c => c.Posicao).HasComment("Posição na chamada.");
    }
}
