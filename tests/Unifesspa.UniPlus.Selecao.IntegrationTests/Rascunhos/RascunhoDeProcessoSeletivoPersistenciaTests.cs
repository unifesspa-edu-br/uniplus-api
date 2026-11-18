namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using static RascunhosSemeadosParaTeste;

/// <summary>
/// Integração real (Testcontainers Postgres) do rascunho do processo seletivo: o documento volta como JSON, o banco
/// guarda a revisão e a forma do documento por constraint, e a exclusão apaga a linha.
/// </summary>
public sealed class RascunhoDeProcessoSeletivoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public RascunhoDeProcessoSeletivoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    [Fact(DisplayName = "O rascunho volta com o documento como JSON, a revisão, o nome, a unidade e os instantes de auditoria")]
    public async Task GravarELer_PreservaOQueOAgregadoGuarda()
    {
        Guid unidade = Guid.CreateVersion7();
        Guid id = await SemearRascunhoAsync(_fixture, unidade, """{"numero":"07","ano":2027,"orgao":"REITORIA — CEPS"}""", "Certame de teste");

        await using UniPlusDbContext leitura = _fixture.CreateDbContext();
        RascunhoDeProcessoSeletivo? lido = await Repositorio(leitura).ObterAsync(id);

        lido.Should().NotBeNull();
        JsonNode.DeepEquals(JsonNode.Parse(lido!.Documento), JsonNode.Parse("""{"numero":"07","ano":2027,"orgao":"REITORIA — CEPS"}""")).Should().BeTrue();
        lido.Revisao.Should().Be(1);
        lido.Nome.Should().Be("Certame de teste");
        lido.UnidadeAdministradoraOrigemId.Should().Be(unidade);
        lido.CreatedAt.Should().NotBe(default);
    }

    [Fact(DisplayName = "A leitura comum não rastreia o rascunho e a de mutação rastreia")]
    public async Task ObterNaoRastreia_ObterParaMutacaoRastreia()
    {
        Guid id = await SemearRascunhoAsync(_fixture, Guid.CreateVersion7());

        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        await Repositorio(contexto).ObterAsync(id);
        contexto.ChangeTracker.Entries().Should().BeEmpty();

        await Repositorio(contexto).ObterParaMutacaoAsync(id);

        contexto.ChangeTracker.Entries<RascunhoDeProcessoSeletivo>().Should().ContainSingle();
    }

    [Fact(DisplayName = "A substituição grava a nova revisão, o documento e a unidade, e atualiza o instante de alteração")]
    public async Task Substituir_GravaARevisaoNova()
    {
        Guid id = await SemearRascunhoAsync(_fixture, Guid.CreateVersion7());
        Guid outraUnidade = Guid.CreateVersion7();

        await using (UniPlusDbContext contexto = _fixture.CreateDbContext())
        {
            RascunhoDeProcessoSeletivo rascunho = (await Repositorio(contexto).ObterParaMutacaoAsync(id))!;
            rascunho.Substituir(Documento("""{"b":2}""", "Segundo nome"), outraUnidade, PrecondicaoIfMatch.Curinga);
            await contexto.SaveChangesAsync();
        }

        await using UniPlusDbContext leitura = _fixture.CreateDbContext();
        RascunhoDeProcessoSeletivo lido = (await Repositorio(leitura).ObterAsync(id))!;
        (lido.Revisao, lido.Nome, lido.UnidadeAdministradoraOrigemId).Should().Be((2, "Segundo nome", outraUnidade));
        lido.UpdatedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "Remover apaga a linha de fato, sem marca de exclusão lógica")]
    public async Task Remover_ApagaALinha()
    {
        Guid id = await SemearRascunhoAsync(_fixture, Guid.CreateVersion7());

        await using (UniPlusDbContext contexto = _fixture.CreateDbContext())
        {
            RascunhoDeProcessoSeletivo rascunho = (await Repositorio(contexto).ObterParaMutacaoAsync(id))!;
            Repositorio(contexto).Remover(rascunho);
            await contexto.SaveChangesAsync();
        }

        (await ContarLinhasAsync(id)).Should().Be(0);
    }

    [Fact(DisplayName = "O banco recusa documento que não é objeto JSON")]
    public async Task Banco_RecusaDocumentoQueNaoEObjeto()
    {
        Func<Task> inserir = () => InserirAsync(Guid.CreateVersion7(), revisao: 1, documento: "[1,2]");

        (await inserir.Should().ThrowAsync<PostgresException>()).Which.ConstraintName
            .Should().Be("ck_rascunhos_processos_seletivos_documento_objeto");
    }

    [Fact(DisplayName = "O banco recusa revisão menor que 1")]
    public async Task Banco_RecusaRevisaoMenorQueUm()
    {
        Func<Task> inserir = () => InserirAsync(Guid.CreateVersion7(), revisao: 0, documento: "{}");

        (await inserir.Should().ThrowAsync<PostgresException>()).Which.ConstraintName
            .Should().Be("ck_rascunhos_processos_seletivos_revisao");
    }

    [Fact(DisplayName = "O banco aceita objeto JSON e a revisão 1 gravados direto")]
    public async Task Banco_AceitaObjetoERevisaoUm()
    {
        Guid id = Guid.CreateVersion7();

        await InserirAsync(id, revisao: 1, documento: "{}");

        (await ContarLinhasAsync(id)).Should().Be(1);
    }

    private static DocumentoDoRascunho Documento(string conteudo, string nome) =>
        DocumentoDoRascunho.Criar(conteudo, nome).Value!;

    private async Task<int> ContarLinhasAsync(Guid id)
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext();
        return await leitura.RascunhosDeProcessosSeletivos.IgnoreQueryFilters().CountAsync(r => r.Id == id);
    }

    private async Task InserirAsync(Guid id, int revisao, string documento)
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = new(
            """
            INSERT INTO selecao.rascunhos_processos_seletivos (id, revisao, unidade_administradora_origem_id, documento, created_at)
            VALUES (@id, @revisao, @unidade, @documento::jsonb, now())
            """,
            conexao);
        comando.Parameters.AddWithValue("id", id);
        comando.Parameters.AddWithValue("revisao", revisao);
        comando.Parameters.AddWithValue("unidade", Guid.CreateVersion7());
        comando.Parameters.AddWithValue("documento", documento);
        await comando.ExecuteNonQueryAsync();
    }
}
