namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Fitness function do tamanho de identificador (<c>docs/guia-banco-de-dados.md</c>): tabela, coluna, chave, chave
/// estrangeira, índice e restrição de verificação de todo <see cref="DbContext"/> cabem nos 63 bytes do PostgreSQL. A
/// leitura é do modelo relacional, o que a migration grava, e não do texto da migration; o EF corta em silêncio o nome que
/// ele mesmo gera, então chave estrangeira e índice de nome longo recebem nome explícito na <c>Configuration</c>.
/// </summary>
public sealed class TamanhoDosIdentificadoresDoModeloTests
{
    private const string TabelaDoCanario = "canarios_com_nome_muito_comprido_para_estourar_o_limite";

    private static readonly string[] TiposDeIdentificador =
    [
        IdentificadoresDoModelo.Tabela,
        IdentificadoresDoModelo.Coluna,
        IdentificadoresDoModelo.Chave,
        IdentificadoresDoModelo.ChaveEstrangeira,
        IdentificadoresDoModelo.Indice,
        IdentificadoresDoModelo.RestricaoDeVerificacao,
    ];

    [Fact(DisplayName = "A descoberta encontra identificadores de todos os tipos")]
    public void DescobertaEncontraIdentificadoresDeTodosOsTipos() =>
        IdentificadoresDaSolucao().Select(i => i.Tipo).Distinct().Should().BeEquivalentTo(
            TiposDeIdentificador, "sem identificador descoberto de um tipo, a conferência dele passaria vazia");

    [Fact(DisplayName = "Todo identificador do modelo relacional cabe nos 63 bytes do PostgreSQL")]
    public void TodoIdentificadorCabeNoLimiteDoBanco()
    {
        IReadOnlyList<string> violacoes = RegraDeTamanhoDeIdentificador.Violacoes(IdentificadoresDaSolucao());

        violacoes.Should().BeEmpty(
            "o PostgreSQL corta o identificador acima de 63 bytes; dê nome explícito, por extenso, na Configuration. Violações:\n"
            + string.Join("\n", violacoes));
    }

    [Fact(DisplayName = "Chave estrangeira e índice que o EF deriva com mais de 63 bytes, sem nome explícito, são recusados")]
    public void NomesDerivadosAcimaDoLimite_SaoRecusados()
    {
        using ContextoCanario contexto = new(indice: null, chaveEstrangeira: null, restricao: "ck_canarios_id_preenchido");

        IReadOnlyList<string> violacoes = RegraDeTamanhoDeIdentificador.Violacoes(IdentificadoresDoModelo.Do(Modelo(contexto)));

        string chaveEstrangeira = $"fk_{TabelaDoCanario}_{TabelaDoCanario}_pai_do_registro_id";
        string indice = $"ix_{TabelaDoCanario}_pai_do_registro_id";
        violacoes.Should().Equal(
            $"canario.{TabelaDoCanario}: chave estrangeira '{chaveEstrangeira}' ocupa {chaveEstrangeira.Length} bytes e o limite é 63",
            $"canario.{TabelaDoCanario}: índice '{indice}' ocupa {indice.Length} bytes e o limite é 63");
    }

    [Fact(DisplayName = "Índice, chave estrangeira e restrição explícitos com 64 bytes são recusados")]
    public void NomesExplicitosComUmByteAMais_SaoRecusados()
    {
        using ContextoCanario contexto = new(NomeLongo("ix"), NomeLongo("fk"), NomeLongo("ck"));

        IReadOnlyList<string> violacoes = RegraDeTamanhoDeIdentificador.Violacoes(IdentificadoresDoModelo.Do(Modelo(contexto)));

        violacoes.Should().Equal(
            $"canario.{TabelaDoCanario}: chave estrangeira '{NomeLongo("fk")}' ocupa 64 bytes e o limite é 63",
            $"canario.{TabelaDoCanario}: restrição de verificação '{NomeLongo("ck")}' ocupa 64 bytes e o limite é 63",
            $"canario.{TabelaDoCanario}: índice '{NomeLongo("ix")}' ocupa 64 bytes e o limite é 63");
    }

    [Fact(DisplayName = "Índice, chave estrangeira e restrição explícitos com 63 bytes são aceitos")]
    public void NomesExplicitosNoLimite_SaoAceitos()
    {
        using ContextoCanario contexto = new(NomeLongo("ix")[1..], NomeLongo("fk")[1..], NomeLongo("ck")[1..]);

        RegraDeTamanhoDeIdentificador.Violacoes(IdentificadoresDoModelo.Do(Modelo(contexto))).Should().BeEmpty();
    }

    private static IEnumerable<IdentificadorDoBanco> IdentificadoresDaSolucao() =>
        ModelosSemTruncamento.Modelos().SelectMany(m => IdentificadoresDoModelo.Do(m.Modelo));

    private static IModel Modelo(DbContext contexto) => contexto.GetService<IDesignTimeModel>().Model;

    private static string NomeLongo(string prefixo) => prefixo + new string('a', RegraDeTamanhoDeIdentificador.LimiteDeBytes + 1 - prefixo.Length);

    private sealed class Canario
    {
        public Guid Id { get; init; }

        public Guid? PaiDoRegistroId { get; init; }
    }

    // Sem cache do provedor de serviços, cada contexto constrói o próprio modelo: com ele, o primeiro modelo do tipo valeria para todos.
    // Nome nulo deixa o EF derivar o nome da chave estrangeira ou do índice.
    private sealed class ContextoCanario(string? indice, string? chaveEstrangeira, string restricao) : DbContext(
        ModelosSemTruncamento.SemTruncamento(
            new DbContextOptionsBuilder<ContextoCanario>().UseNpgsql("Host=canario").UseSnakeCaseNamingConvention()
                .EnableServiceProviderCaching(false).Options))
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            modelBuilder.Entity<Canario>(canario =>
            {
                canario.ToTable(TabelaDoCanario, "canario", tabela => tabela.HasCheckConstraint(restricao, "id IS NOT NULL"));
                NomearIndice(canario.HasIndex(c => c.PaiDoRegistroId));
                NomearChaveEstrangeira(canario.HasOne<Canario>().WithMany().HasForeignKey(c => c.PaiDoRegistroId));
            });
        }

        private void NomearIndice(IndexBuilder<Canario> construtor)
        {
            if (indice is not null)
            {
                construtor.HasDatabaseName(indice);
            }
        }

        private void NomearChaveEstrangeira(ReferenceCollectionBuilder<Canario, Canario> construtor)
        {
            if (chaveEstrangeira is not null)
            {
                construtor.HasConstraintName(chaveEstrangeira);
            }
        }
    }
}
