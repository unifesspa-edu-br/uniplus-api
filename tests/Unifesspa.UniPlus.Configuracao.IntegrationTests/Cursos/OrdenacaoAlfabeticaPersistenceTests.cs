namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Cursos;

using System.Diagnostics.CodeAnalysis;
using System.Text;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// A chave de ordenação alfabética como o banco a materializa: coluna gerada a
/// partir do nome, o índice que sustenta a listagem, e a normalização que faz
/// acento e caixa não decidirem a posição.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class OrdenacaoAlfabeticaPersistenceTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public OrdenacaoAlfabeticaPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A chave de ordenação é coluna gerada pelo banco, e não escrita pela aplicação")]
    public async Task ChaveDeOrdenacao_EhColunaGerada()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        // is_generated = 'ALWAYS' é o que garante que não existe caminho de escrita
        // capaz de dessincronizar a chave do nome.
        string? geracao = await ctx.Database
            .SqlQuery<string?>($@"
                SELECT is_generated AS ""Value""
                FROM information_schema.columns
                WHERE table_schema = 'configuracao'
                  AND table_name = 'curso'
                  AND column_name = 'nome_ordenacao'")
            .SingleOrDefaultAsync();

        geracao.Should().Be("ALWAYS");
    }

    [Fact(DisplayName = "A chave de ordenação é obrigatória — sem ela o seek do cursor não teria por onde continuar")]
    public async Task ChaveDeOrdenacao_EhObrigatoria()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        string? aceitaNulo = await ctx.Database
            .SqlQuery<string?>($@"
                SELECT is_nullable AS ""Value""
                FROM information_schema.columns
                WHERE table_schema = 'configuracao'
                  AND table_name = 'curso'
                  AND column_name = 'nome_ordenacao'")
            .SingleOrDefaultAsync();

        aceitaNulo.Should().Be("NO");
    }

    [Fact(DisplayName = "O índice da listagem cobre as três colunas da ordenação, restrito aos cursos vivos")]
    public async Task Indice_CobreAsColunasDaOrdenacao()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        string? definicao = await ctx.Database
            .SqlQuery<string?>($@"
                SELECT indexdef AS ""Value""
                FROM pg_indexes
                WHERE schemaname = 'configuracao'
                  AND indexname = 'ix_curso_ordenacao_alfabetica'")
            .SingleOrDefaultAsync();

        definicao.Should().NotBeNull("sem o índice, a listagem ordena por varredura completa");
        definicao.Should().Contain("nome_ordenacao").And.Contain("codigo").And.Contain("id");
        definicao.Should().Contain("is_deleted = false", "toda leitura já é restrita aos cursos vivos");
    }

    [Fact(DisplayName = "A chave sai sem acento e em minúsculas, inclusive com acento decomposto")]
    public async Task Chave_RemoveAcentoECaixa()
    {
        string codigo = $"CUR_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

        // Acento decomposto de propósito: "e" seguido do acento agudo combinante
        // (U+0301), em vez do "é" pré-composto. É a forma que chega de fonte que não
        // normaliza o texto, e a substituição de acentos só a alcança porque a
        // expressão da coluna normaliza antes de substituir.
        const string NomeDecomposto = "Cie\u0301ncias Conta\u0301beis";

        await using (ConfiguracaoDbContext escrita = _fixture.CreateDbContext("admin"))
        {
            escrita.Cursos.Add(
                Curso.Criar(codigo, NomeDecomposto, "Bacharelado", "Graduação", null).Value!);
            await escrita.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext leitura = _fixture.CreateDbContext(userId: null);
        string? chave = await leitura.Database
            .SqlQuery<string?>($@"
                SELECT nome_ordenacao AS ""Value""
                FROM configuracao.curso
                WHERE codigo = {codigo}")
            .SingleOrDefaultAsync();

        chave.Should().Be("ciencias contabeis");

        // A forma gravada continua sendo a que chegou — a normalização vive na
        // chave de ordenação, não no dado do cadastro.
        string? nomeGravado = await leitura.Database
            .SqlQuery<string?>($@"
                SELECT nome AS ""Value""
                FROM configuracao.curso
                WHERE codigo = {codigo}")
            .SingleOrDefaultAsync();

        nomeGravado.Should().Be(NomeDecomposto);
        nomeGravado.Should().NotBe(NomeDecomposto.Normalize(NormalizationForm.FormC));
    }
}
