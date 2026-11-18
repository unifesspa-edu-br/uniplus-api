namespace Unifesspa.UniPlus.Discentes.IntegrationTests;

using System.Text;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using static Unifesspa.UniPlus.Discentes.IntegrationTests.VinculoDeTeste;

/// <summary>
/// Prova, contra Postgres real (Testcontainers), os critérios de aceite arquiteturais
/// da ADR-0121 para o CPF do módulo Discentes: coluna <c>bytea</c>, nunca texto claro,
/// envelopes diferentes por linha (nonce aleatório) e round-trip via repositório.
/// </summary>
[Trait("Category", "Integration")]
public sealed class VinculoDiscentePersistenceTests : IClassFixture<VinculoDiscenteDbFixture>
{
    private readonly VinculoDiscenteDbFixture _fixture;

    public VinculoDiscentePersistenceTests(VinculoDiscenteDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Persistir_E_Ler_Faz_RoundTrip_Do_Cpf_Via_Repositorio()
    {
        VinculoDiscente original = Novo(1001);
        await AdicionarAsync(original);

        VinculoDiscente? lido = await LerPorIdSigaaAsync(1001);

        lido.Should().NotBeNull();
        lido!.Id.Should().Be(original.Id);
        lido.DadosCopiados.Identificacao.Cpf.Valor.Should().Be(CpfValido);
        lido.DadosCopiados.Identificacao.Nome.Should().Be(original.DadosCopiados.Identificacao.Nome);
    }

    [Fact]
    public async Task Coluna_Cifrada_Nunca_Contem_Cpf_Em_Texto_Claro()
    {
        await AdicionarAsync(Novo(1002));

        byte[] ciphertext = await LerColunaCifradaAsync(1002);

        Convert.ToBase64String(ciphertext).Should().NotContain(CpfValido);
        Encoding.UTF8.GetString(ciphertext).Should().NotContain(CpfValido);
    }

    [Fact]
    public async Task Mesmo_Cpf_Em_Duas_Linhas_Produz_Envelopes_Diferentes()
    {
        await AdicionarAsync(Novo(1003), Novo(1004));

        byte[] ciphertextA = await LerColunaCifradaAsync(1003);
        byte[] ciphertextB = await LerColunaCifradaAsync(1004);

        ciphertextA.Should().NotBeEquivalentTo(ciphertextB);
    }

    [Fact]
    public async Task Update_Recifra_O_Cpf_Alterado()
    {
        VinculoDiscente original = Novo(1005);
        await AdicionarAsync(original);

        await AtualizarAsync(VinculoDiscente.Reidratar(original.Id, Novo(1005, OutroCpfValido).DadosCopiados));

        VinculoDiscente? lido = await LerPorIdAsync(original.Id);

        lido.Should().NotBeNull();
        lido!.DadosCopiados.Identificacao.Cpf.Valor.Should().Be(OutroCpfValido);
    }

    /// <summary>
    /// Dado persistido que não passa mais pela regra de domínio falha alto, com a mensagem da
    /// parte que acusou. Quando há mais de uma parte corrompida, vale a ordem: curso, situação,
    /// período e, por último, o restante do vínculo (identificação e nível).
    /// </summary>
    [Theory]
    [InlineData(2001, "nome = ''", "Dados copiados corrompidos")]
    [InlineData(2002, "matricula = '12A'", "Dados copiados corrompidos")]
    [InlineData(2003, "nivel = ''", "Dados copiados corrompidos")]
    [InlineData(2004, "curso_nome = ''", "Curso corrompido")]
    [InlineData(2005, "situacao_descricao = ''", "Situação corrompida")]
    [InlineData(2006, "ano_ingresso = 0", "Período corrompido")]
    [InlineData(2007, "nome = '', curso_nome = ''", "Curso corrompido")]
    [InlineData(2008, "curso_nome = '', situacao_descricao = ''", "Curso corrompido")]
    [InlineData(2009, "situacao_descricao = '', ano_ingresso = 0", "Situação corrompida")]
    [InlineData(2010, "ano_ingresso = 0, nome = ''", "Período corrompido")]
    public async Task Registro_corrompido_falha_alto_com_a_mensagem_da_primeira_parte(
        long idSigaa,
        string corrupcao,
        string mensagem)
    {
        await AdicionarAsync(Novo(idSigaa));
        await CorromperAsync(idSigaa, corrupcao);

        Func<Task> leitura = () => LerPorIdSigaaAsync(idSigaa);

        (await leitura.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().StartWith(mensagem);
    }

    private async Task CorromperAsync(long idSigaa, string atribuicoes)
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using NpgsqlCommand comando = new(
            $"UPDATE discentes.vinculos_de_discentes SET {atribuicoes} WHERE id_discente_sigaa = @id",
            conexao);
        comando.Parameters.AddWithValue("id", idSigaa);
        await comando.ExecuteNonQueryAsync();
    }

    private async Task AdicionarAsync(params VinculoDiscente[] vinculos)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        VinculoDiscenteRepository repositorio = new(contexto, _fixture.Encryption);

        foreach (VinculoDiscente vinculo in vinculos)
        {
            await repositorio.AdicionarAsync(vinculo);
        }

        await contexto.SaveChangesAsync();
    }

    private async Task AtualizarAsync(VinculoDiscente vinculo)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();

        await new VinculoDiscenteRepository(contexto, _fixture.Encryption).AtualizarAsync(vinculo);
        await contexto.SaveChangesAsync();
    }

    private async Task<VinculoDiscente?> LerPorIdSigaaAsync(long idSigaa)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();

        return await new VinculoDiscenteRepository(contexto, _fixture.Encryption).ObterPorIdSigaaAsync(VinculoDeTeste.IdNoSigaa(idSigaa));
    }

    private async Task<VinculoDiscente?> LerPorIdAsync(Guid id)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();

        return await new VinculoDiscenteRepository(contexto, _fixture.Encryption).ObterPorIdAsync(id);
    }

    private async Task<byte[]> LerColunaCifradaAsync(long idSigaa)
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using NpgsqlCommand comando = new(
            "SELECT cpf_ciphertext FROM discentes.vinculos_de_discentes WHERE id_discente_sigaa = @id",
            conexao);
        comando.Parameters.AddWithValue("id", idSigaa);

        return (byte[])(await comando.ExecuteScalarAsync())!;
    }
}
