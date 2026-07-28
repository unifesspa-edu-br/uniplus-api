namespace Unifesspa.UniPlus.Discentes.IntegrationTests;

using System.Text;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// Prova, contra Postgres real (Testcontainers), os critérios de aceite arquiteturais
/// da ADR-0121 para o CPF do módulo Discentes: coluna <c>bytea</c>, nunca texto claro,
/// envelopes diferentes por linha (nonce aleatório) e round-trip via repositório.
/// </summary>
public sealed class VinculoDiscentePersistenceTests : IClassFixture<VinculoDiscenteDbFixture>
{
    private const string CpfValido = "52998224725";
    private const string OutroCpfValido = "11144477735";

    private readonly VinculoDiscenteDbFixture _fixture;

    public VinculoDiscentePersistenceTests(VinculoDiscenteDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static VinculoDiscente NovoVinculo(long idSigaa, string cpfValor) =>
        new(
            Guid.CreateVersion7(),
            idSigaa,
            matricula: idSigaa.ToString(System.Globalization.CultureInfo.InvariantCulture),
            cpf: Cpf.Criar(cpfValor).Match(cpf => cpf, erro => throw new InvalidOperationException(erro.Code)),
            nome: "Discente de Teste",
            nivel: "G",
            cursoId: 1,
            cursoNome: "Ciência da Computação",
            cursoCodigoEmec: null,
            cursoUnidadeId: 1,
            cursoUnidadeNome: "Instituto de Ciências Exatas",
            situacaoId: 1,
            situacaoDescricao: "Matriculado",
            situacaoVinculo: null,
            anoIngresso: 2026,
            periodoIngresso: 1);

    [Fact]
    public async Task Persistir_E_Ler_Faz_RoundTrip_Do_Cpf_Via_Repositorio()
    {
        await using DiscentesDbContext writeContext = _fixture.CreateDbContext();
        VinculoDiscenteRepository writeRepository = new(writeContext, _fixture.Encryption);

        VinculoDiscente original = NovoVinculo(idSigaa: 1001, CpfValido);
        await writeRepository.AdicionarVinculoDiscenteAsync(original);
        await writeContext.SaveChangesAsync();

        await using DiscentesDbContext readContext = _fixture.CreateDbContext();
        VinculoDiscenteRepository readRepository = new(readContext, _fixture.Encryption);

        VinculoDiscente? lido = await readRepository.ObterComIdSigaaAsync(1001);

        lido.Should().NotBeNull();
        lido!.Cpf.Valor.Should().Be(CpfValido);
        lido.Nome.Should().Be(original.Nome);
    }

    [Fact]
    public async Task Coluna_Cifrada_Nunca_Contem_Cpf_Em_Texto_Claro()
    {
        await using DiscentesDbContext writeContext = _fixture.CreateDbContext();
        VinculoDiscenteRepository repository = new(writeContext, _fixture.Encryption);

        await repository.AdicionarVinculoDiscenteAsync(NovoVinculo(idSigaa: 1002, CpfValido));
        await writeContext.SaveChangesAsync();

        await using NpgsqlConnection connection = new(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using NpgsqlCommand command = new(
            "SELECT cpf_ciphertext FROM discentes.vinculo_discente WHERE id_discente_sigaa = 1002",
            connection);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        byte[] ciphertext = (byte[])reader[0];
        string ciphertextComoTexto = Convert.ToBase64String(ciphertext);

        ciphertextComoTexto.Should().NotContain(CpfValido);
        Encoding.UTF8.GetString(ciphertext).Should().NotContain(CpfValido);
    }

    [Fact]
    public async Task Mesmo_Cpf_Em_Duas_Linhas_Produz_Envelopes_Diferentes()
    {
        await using DiscentesDbContext writeContext = _fixture.CreateDbContext();
        VinculoDiscenteRepository repository = new(writeContext, _fixture.Encryption);

        await repository.AdicionarVinculoDiscenteAsync(NovoVinculo(idSigaa: 1003, CpfValido));
        await repository.AdicionarVinculoDiscenteAsync(NovoVinculo(idSigaa: 1004, CpfValido));
        await writeContext.SaveChangesAsync();

        await using DiscentesDbContext readContext = _fixture.CreateDbContext();

        byte[] ciphertextA = await readContext.VinculosDiscentes
            .Where(v => v.IdDiscenteSigaa == 1003)
            .Select(v => v.CpfCiphertext)
            .SingleAsync();

        byte[] ciphertextB = await readContext.VinculosDiscentes
            .Where(v => v.IdDiscenteSigaa == 1004)
            .Select(v => v.CpfCiphertext)
            .SingleAsync();

        ciphertextA.Should().NotBeEquivalentTo(ciphertextB);
    }

    [Fact]
    public async Task Update_Recifra_O_Cpf_Alterado()
    {
        await using DiscentesDbContext writeContext = _fixture.CreateDbContext();
        VinculoDiscenteRepository writeRepository = new(writeContext, _fixture.Encryption);

        VinculoDiscente original = NovoVinculo(idSigaa: 1005, CpfValido);
        await writeRepository.AdicionarVinculoDiscenteAsync(original);
        await writeContext.SaveChangesAsync();

        await using DiscentesDbContext updateContext = _fixture.CreateDbContext();
        VinculoDiscenteRepository updateRepository = new(updateContext, _fixture.Encryption);

        VinculoDiscente atualizado = NovoVinculo(idSigaa: 1005, OutroCpfValido);
        VinculoDiscente comIdOriginal = new(
            original.Id,
            atualizado.IdDiscenteSigaa,
            atualizado.Matricula,
            atualizado.Cpf,
            atualizado.Nome,
            atualizado.Nivel,
            atualizado.CursoId,
            atualizado.CursoNome,
            atualizado.CursoCodigoEmec,
            atualizado.CursoUnidadeId,
            atualizado.CursoUnidadeNome,
            atualizado.SituacaoId,
            atualizado.SituacaoDescricao,
            atualizado.SituacaoVinculo,
            atualizado.AnoIngresso,
            atualizado.PeriodoIngresso);

        await updateRepository.AtualizarVinculoDiscenteAsync(comIdOriginal);
        await updateContext.SaveChangesAsync();

        await using DiscentesDbContext readContext = _fixture.CreateDbContext();
        VinculoDiscenteRepository readRepository = new(readContext, _fixture.Encryption);

        VinculoDiscente? lido = await readRepository.ObterVinculoDiscenteAsync(original.Id);

        lido.Should().NotBeNull();
        lido!.Cpf.Valor.Should().Be(OutroCpfValido);
    }
}
