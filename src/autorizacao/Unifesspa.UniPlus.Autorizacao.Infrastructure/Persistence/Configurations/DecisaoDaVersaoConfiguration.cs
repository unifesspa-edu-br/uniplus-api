namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core das decisões das etapas de aprovação da versão do perfil, registro só acrescentado.</summary>
internal sealed class DecisaoDaVersaoConfiguration : IEntityTypeConfiguration<DecisaoDaVersao>
{
    private const int IdentidadeMaxLength = 255;
    private const int CodigoMaxLength = 40;

    public void Configure(EntityTypeBuilder<DecisaoDaVersao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("decisoes_da_versao", ConfigurarRestricoes);
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Ciclo).IsRequired();
        builder.Property(d => d.Etapa).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(d => d.Resultado).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(d => d.DecisorEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(d => d.DecisorContaId).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(d => d.DecisorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(d => d.DecisorEmail).HasMaxLength(EmailDaPessoa.MaxLength);
        builder.Property(d => d.DecisorTemConcessao).IsRequired();
        builder.Property(d => d.Motivo).HasMaxLength(MotivoInformado.MaximoDeCaracteres);
        builder.Property(d => d.AcrescimoAvaliado).IsRequired();
        builder.Property(d => d.ExigiaProtecaoDeDados).IsRequired();
        builder.Property(d => d.Instante).IsRequired();

        builder.Ignore(d => d.Decisor);

        // Uma decisão por etapa em cada ciclo: a segunda, simultânea, é barrada aqui se escapar da trava do perfil.
        builder.HasIndex(d => new { d.VersaoId, d.Ciclo, d.Etapa }).IsUnique().HasDatabaseName("ux_decisoes_da_versao_etapa");
    }

    private static void ConfigurarRestricoes(TableBuilder<DecisaoDaVersao> tabela)
    {
        tabela.HasCheckConstraint(
            "ck_decisoes_da_versao_motivo_na_recusa",
            $"(resultado = {SqlDosCodigosDeContrato.Literal(ResultadoDaDecisao.Recusado)}) = (motivo IS NOT NULL)");
        tabela.HasCheckConstraint(
            "ck_decisoes_da_versao_etapa",
            $"etapa IN ({SqlDosCodigosDeContrato.Lista<EtapaDaVersao>()})");
        tabela.HasCheckConstraint(
            "ck_decisoes_da_versao_resultado",
            $"resultado IN ({SqlDosCodigosDeContrato.Literal(ResultadoDaDecisao.Aprovado)}, {SqlDosCodigosDeContrato.Literal(ResultadoDaDecisao.Recusado)})");
    }
}
