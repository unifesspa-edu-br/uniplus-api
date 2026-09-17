namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

internal sealed class CertameDivulgadoConfiguration : IEntityTypeConfiguration<CertameDivulgado>
{
    /// <summary>
    /// Nome da propriedade sombra que carrega a chave de ordenação alfabética do certame. O
    /// repositório da vitrine a projeta por <c>EF.Property&lt;string&gt;</c>.
    /// </summary>
    internal const string NomeOrdenacaoPropriedade = "NomeOrdenacao";

    private const int NomeMaxLength = 200;
    private const int NumeroMaxLength = 60;

    /// <summary>
    /// Expressão da coluna gerada, vinda da normalização compartilhada: a mesma regra que a
    /// aplicação aplica ao termo pesquisado, para que a comparação entre os dois seja possível.
    /// </summary>
    private static readonly string NomeOrdenacaoSql = NormalizacaoTextual.ExpressaoSql("nome");

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
        // Facetas: os mesmos valores que o documento carrega, em coluna, porque buscar, recortar e
        // ordenar não se fazem sobre documento. Não são segunda fonte — saem da mesma projeção, no
        // mesmo instante, e avançam com ela.
        builder.Property(c => c.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(c => c.Numero).HasMaxLength(NumeroMaxLength);

        // Arranjo nativo do Postgres, e não jsonb: o recorte por modalidade é um teste de
        // pertinência, que o operador de sobreposição resolve com índice GIN.
        builder.Property(c => c.ModalidadesOfertadas)
            .HasColumnType("text[]")
            .IsRequired();

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

        // Índice da vitrine: prazo e identificador, na ordem em que a página desempata. Serve a
        // faixa sobre o prazo — que é o que reduz o conjunto em todo recorte por situação — e o
        // desempate por identificador. O lado da abertura da janela, que distingue o que ainda não
        // abriu do que já recebe inscrição, fica como filtro residual sobre as linhas que a faixa
        // já reduziu. NÃO serve a ordenação inteira: a primeira coluna que a vitrine ordena é o
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

        ConfigurarOrdenacaoAlfabetica(builder);

        // Sobreposição de arranjos (`&&`) é o operador que o recorte por modalidade usa, e GIN é o
        // método que o serve. Sem ele, o recorte varre a tabela — numa rota anônima com pico
        // previsível na abertura de inscrições.
        builder.HasIndex(c => c.ModalidadesOfertadas)
            .HasMethod("gin")
            .HasDatabaseName("ix_certames_divulgados_modalidades");
    }

    /// <summary>
    /// Chave de ordenação alfabética do título, como coluna gerada.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ordenar pela coluna crua daria a ordem do ponto de código: acentuada depois de tudo, e
    /// maiúscula antes de minúscula. Normalizar na consulta impediria o uso de índice.
    /// </para>
    /// <para>
    /// Coluna gerada não tem caminho de escrita próprio: não há como dessincronizá-la do título. A
    /// chave é não-nula porque o título é obrigatório — é o que a regra de chave de ordenação
    /// não-nula (ADR-0095) exige para o seek não devolver conjunto vazio.
    /// </para>
    /// </remarks>
    private static void ConfigurarOrdenacaoAlfabetica(EntityTypeBuilder<CertameDivulgado> builder)
    {
        builder.Property<string>(NomeOrdenacaoPropriedade)
            .HasMaxLength(NomeMaxLength)
            .UseCollation("C")
            .HasComputedColumnSql(NomeOrdenacaoSql, stored: true)
            .IsRequired();

        // Casa o ORDER BY da vitrine ordenada por título, com o identificador de desempate.
        builder.HasIndex(NomeOrdenacaoPropriedade, nameof(CertameDivulgado.Id))
            .HasDatabaseName("ix_certames_divulgados_nome_ordenacao");
    }
}
