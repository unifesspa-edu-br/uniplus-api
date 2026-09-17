namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

internal sealed class CertameDivulgadoConfiguration : IEntityTypeConfiguration<CertameDivulgado>
{
    public void Configure(EntityTypeBuilder<CertameDivulgado> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("certames_divulgados");

        // O processo É a identidade: um certame tem uma divulgação corrente, nunca duas.
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.NumeroVersao).IsRequired();
        builder.Property(c => c.AtoCriadorId).IsRequired();
        builder.Property(c => c.HashConfiguracao).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(c => c.VersaoProjecao).HasMaxLength(16).IsRequired();
        builder.Property(c => c.InscricoesDe).IsRequired();
        builder.Property(c => c.InscricoesAte).IsRequired();
        builder.Property(c => c.DivulgadoEm).IsRequired();

        // A resposta pública pronta. jsonb, e não texto: mesmo que hoje a leitura devolva o
        // documento inteiro, buscar dentro dele é a próxima necessidade previsível da vitrine.
        builder.Property(c => c.Certame).HasColumnType("jsonb").IsRequired();

        // Token de concorrência otimista mapeado para a coluna de sistema `xmin` do Postgres
        // (shadow property `uint` + IsRowVersion — convenção do provider Npgsql, sem coluna nem
        // migration própria; ADR-0119, mesmo padrão de MotivoDecisaoIsencaoConfiguration).
        // Avançar a divulgação é check-then-act: dois desfechos de registro do mesmo processo,
        // um da retificação e outro da versão seguinte, podem ser consumidos em paralelo, ler a
        // MESMA linha e ambos passar a guarda de monotonia — que é de memória, não do banco. Sem
        // o xmin, a entrega mais lenta sobrescreveria a versão mais nova e o certame ficaria
        // preso num conteúdo antigo, sem mensagem alguma sobrando para repará-lo. Com ele, a
        // perdedora estoura e a fila reentrega, e aí a guarda de monotonia enxerga o estado real.
        builder.Property<uint>("Version").IsRowVersion();

        // Índice da vitrine: prazo e identificador, na ordem em que a página desempata. Serve o
        // RECORTE por situação, que é sempre uma faixa sobre o prazo, e o desempate por
        // identificador. NÃO serve a ordenação inteira: a primeira coluna que a vitrine ordena é o
        // segmento aberto/encerrado, uma expressão sobre o instante da consulta, que nenhum índice
        // sobre a coluna crua alcança — o Postgres ordena o recorte já reduzido. Indexar a
        // expressão é impossível: o instante é parâmetro, não constante.
        builder.HasIndex(c => new { c.InscricoesAte, c.Id })
            .HasDatabaseName("ix_certames_divulgados_prazo");

        // Um ato divulga no máximo um certame — a mesma unicidade que o registro central garante do
        // outro lado, reafirmada aqui para que uma reentrega não produza duas divulgações.
        builder.HasIndex(c => c.AtoCriadorId)
            .IsUnique()
            .HasDatabaseName("ux_certames_divulgados_ato_criador");
    }
}
