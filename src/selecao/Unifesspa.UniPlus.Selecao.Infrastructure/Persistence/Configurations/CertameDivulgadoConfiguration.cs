namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

internal sealed class CertameDivulgadoConfiguration : IEntityTypeConfiguration<CertameDivulgado>
{
    /// <summary>
    /// Nome da propriedade sombra que carrega a chave de ordenação alfabética do certame. O
    /// repositório da vitrine a projeta por <c>EF.Property&lt;string&gt;</c>.
    /// </summary>
    internal const string NomeOrdenacaoPropriedade = "NomeOrdenacao";

    /// <summary>
    /// Espelha o limite do título na origem. Uma cópia mais curta que ela recusa um processo de
    /// nome legítimo — e recusa no PIOR lugar: aqui a escrita é assíncrona, disparada pelo registro
    /// do ato, e a falha não volta a ninguém. A mensagem morre na fila, a linha nunca nasce, e como
    /// a existência da linha é a publicidade, o certame fica invisível com o ato já registrado.
    /// </summary>
    private const int NomeMaxLength = ProcessoSeletivoConfiguration.NomeMaxLength;

    /// <summary>
    /// Espelha o limite do número do ato no envelope congelado, que é de onde este valor vem.
    /// </summary>
    private const int NumeroMaxLength = LimitesDoEnvelope.NumeroDoAto;

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
        // Facetas em coluna: buscar, recortar e ordenar não se fazem sobre documento.
        builder.Property(c => c.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(c => c.Numero).HasMaxLength(NumeroMaxLength);

        // Arranjo nativo, e não jsonb: pertinência resolvida por operador de arranjo com GIN.
        builder.Property(c => c.ModalidadesOfertadas)
            .HasColumnType("text[]")
            .IsRequired();

        builder.Property(c => c.InscricoesDe).IsRequired();
        builder.Property(c => c.InscricoesAte).IsRequired();
        builder.Property(c => c.DivulgadoEm).IsRequired();

        // jsonb, e não texto: buscar dentro do documento é necessidade previsível da vitrine.
        builder.Property(c => c.Certame).HasColumnType("jsonb").IsRequired();

        // Concorrência otimista pela coluna de sistema `xmin` (ADR-0119): avançar a divulgação é
        // check-then-act, e a guarda de monotonia é de memória, não do banco. Duas entregas
        // paralelas do mesmo processo passariam ambas, e a mais lenta venceria por último.
        builder.Property<uint>("Version").IsRowVersion();

        // Serve a faixa sobre o prazo e o desempate por identificador. Não serve a ordenação
        // inteira: o segmento aberto/encerrado é expressão sobre o instante da consulta, que é
        // parâmetro — não há índice a criar para ele.
        builder.HasIndex(c => new { c.InscricoesAte, c.Id })
            .HasDatabaseName("ix_certames_divulgados_prazo");

        // Um ato divulga no máximo um certame: reentrega não produz duas divulgações.
        builder.HasIndex(c => c.AtoCriadorId)
            .IsUnique()
            .HasDatabaseName("ux_certames_divulgados_ato_criador");

        ConfigurarOrdenacaoAlfabetica(builder);

        // GIN é o método que serve o operador de arranjo do recorte por modalidade.
        builder.HasIndex(c => c.ModalidadesOfertadas)
            .HasMethod("gin")
            .HasDatabaseName("ix_certames_divulgados_modalidades");
    }

    /// <summary>
    /// Chave de ordenação alfabética do título, como coluna gerada.
    /// </summary>
    /// <remarks>
    /// Ordenar pela coluna crua daria a ordem do ponto de código; normalizar na consulta impediria
    /// o índice. Gerada e não-nula, como a ADR-0095 exige para o seek não devolver conjunto vazio.
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
