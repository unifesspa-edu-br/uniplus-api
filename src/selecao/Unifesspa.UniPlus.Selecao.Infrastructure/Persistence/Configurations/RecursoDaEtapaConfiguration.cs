namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento de <see cref="RecursoDaEtapa"/>. <c>publicacao_de_referencia_id</c> é coluna crua, sem
/// FK: os produtos da etapa são substituídos por inteiro a cada gravação, e uma FK
/// restritiva recusaria a substituição — mesmo raciocínio de <c>recursos_configurados_da_fase</c>.
/// </summary>
public sealed class RecursoDaEtapaConfiguration : IEntityTypeConfiguration<RecursoDaEtapa>
{
    // As mesmas grandezas que `recursos_configurados_da_fase` e as demais referências ao rol de regras
    // declaram, e as mesmas que o decodificador do envelope aceita: a janela recursal da etapa
    // guarda referência de regra igual à da fase, e larguras próprias aqui só produziriam
    // envelope que o decodificador aprova e a coluna recusa.

    public void Configure(EntityTypeBuilder<RecursoDaEtapa> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("recursos_configurados_da_etapa", tabela =>
        {
            tabela.HasComment("Configurações dos recursos de uma etapa de avaliação.");
            tabela.HasCheckConstraint(
                "ck_recursos_configurados_da_etapa_evento_inicial_do_prazo",
                $"evento_inicial_do_prazo IN ({SqlDosCodigosDeContrato.Lista<EventoInicialDoPrazo>()})");
        });
        builder.Property(r => r.EtapaProcessoId).HasComment("Identificador do registro ao qual esta configuração pertence.");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasComment("Identificador do registro.").ValueGeneratedNever();
        builder.Property(r => r.Ancora).HasComment("Evento que inicia o prazo: PUBLICACAO_DO_ATO ou CIENCIA_INDIVIDUAL.").HasColumnName("evento_inicial_do_prazo").HasMaxLength(TamanhoDoCodigoDeContrato.Maximo).IsRequired();
        builder.Property(r => r.ProdutoAncoraId).HasComment("Identificador da publicação usada como referência para o início do prazo.").HasColumnName("publicacao_de_referencia_id");
        builder.MapearProcedimentosDeRecurso(r => r.Procedimentos);

        builder.OwnsOne(r => r.Regra, regra =>
        {
            regra.ConfigurarReferenciaRegra("regra");
            regra.Property(x => x.Codigo).HasComment("Código da regra aplicada ao prazo de recurso.");
            regra.Property(x => x.Versao).HasComment("Versão da regra aplicada ao prazo de recurso.");
            regra.Property(x => x.Hash).HasComment("Resumo do conteúdo da regra aplicada ao prazo de recurso.");
        });
        builder.Navigation(r => r.Regra).IsRequired();

        builder.OwnsOne(r => r.Parametros, parametros => parametros.ConfigurarParametrosDoRecurso());
        builder.Navigation(r => r.Parametros).IsRequired();

        builder.HasIndex(r => r.EtapaProcessoId);
    }
}
