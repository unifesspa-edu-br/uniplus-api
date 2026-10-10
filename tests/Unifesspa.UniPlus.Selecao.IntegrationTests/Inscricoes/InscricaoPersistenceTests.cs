namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Prova, contra Postgres real (Testcontainers), os critérios de aceite arquiteturais da
/// ADR-0121 para a inscrição do candidato (issue #1858): a linha crua não contém o CPF nem o
/// número de documento em texto claro, o índice único por (processo, CPF) funciona, e o
/// round-trip via repositório devolve a inscrição inteira — inclusive os documentos dentro de
/// grupos repetíveis.
/// </summary>
[Trait("Category", "Integration")]
public sealed class InscricaoPersistenceTests : IClassFixture<InscricaoDbFixture>
{
    private static readonly Guid Processo = Guid.CreateVersion7();
    private static readonly Guid VersaoFormulario = Guid.CreateVersion7();

    private readonly InscricaoDbFixture _fixture;

    public InscricaoPersistenceTests(InscricaoDbFixture fixture) => _fixture = fixture;

    private static Inscricao NovaInscricao(Cpf cpf, Guid processo) =>
        Inscricao.Iniciar(
            processo, cpf, "sub-1", "https://issuer", VersaoFormulario,
            new HashSet<string>(StringComparer.Ordinal) { "RG_NUMERO" }).Value!;

    [Fact(DisplayName = "Persistir e ler faz round-trip do CPF, das respostas e dos documentos dentro de um grupo")]
    public async Task Persistir_E_Ler_FazRoundTripCompleto()
    {
        Guid processo = Guid.CreateVersion7();
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, processo);
        original.Salvar(
            Guid.NewGuid(),
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement("Ariel Drumond"),
                ["RG_NUMERO"] = JsonSerializer.SerializeToElement("12.868.927-4"),
            },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal)
            {
                ["COMPOSICAO_FAMILIAR"] =
                [
                    new OcorrenciaDeGrupoInscricao("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["RG_NUMERO"] = JsonSerializer.SerializeToElement("25.062.156-3"),
                        ["PARENTESCO"] = JsonSerializer.SerializeToElement("FILHO"),
                    }),
                ],
            },
            [new AceiteDeTermo("TERMO_VERACIDADE", Guid.NewGuid(), "sha256-abc")],
            PrecondicaoIfMatch.DeTags([original.ETag]));

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        InscricaoRepository writeRepository = new(writeContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        await writeRepository.AdicionarAsync(original);
        await writeContext.SaveChangesAsync();

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        InscricaoRepository readRepository = new(readContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        Inscricao? lida = await readRepository.ObterPorIdAsync(original.Id);

        lida.Should().NotBeNull();
        lida!.Cpf.Valor.Should().Be(PersonasDeTeste.FernandaDrumond.Valor);
        lida.Respostas.Should().ContainKey("RG_NUMERO").WhoseValue.GetString().Should().Be("12.868.927-4");
        lida.Respostas.Should().ContainKey("NOME_SOCIAL");
        lida.Grupos.Should().ContainKey("COMPOSICAO_FAMILIAR")
            .WhoseValue.Should().ContainSingle(o => o.Id == "membro-1")
            .Which.Respostas.Should().ContainKey("RG_NUMERO").WhoseValue.GetString().Should().Be("25.062.156-3");
        lida.Aceites.Should().ContainSingle(a => a.Codigo == "TERMO_VERACIDADE");
        lida.CreatedAt.Should().NotBe(default, "o repositório carimba CreatedAt — o AuditableInterceptor não alcança InscricaoRecord");
    }

    [Fact(DisplayName = "ObterPorProcessoECpf localiza a inscrição pelo CPF do dono, sem decifrar a tabela")]
    public async Task ObterPorProcessoECpf_LocalizaPeloDono()
    {
        Guid processo = Guid.CreateVersion7();
        Inscricao original = NovaInscricao(PersonasDeTeste.KevinCarvalho, processo);

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        InscricaoRepository writeRepository = new(writeContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        await writeRepository.AdicionarAsync(original);
        await writeContext.SaveChangesAsync();

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        InscricaoRepository readRepository = new(readContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        Inscricao? lida = await readRepository.ObterPorProcessoECpfAsync(processo, PersonasDeTeste.KevinCarvalho);

        lida.Should().NotBeNull();
        lida!.Id.Should().Be(original.Id);

        Inscricao? outroProcesso = await readRepository.ObterPorProcessoECpfAsync(Guid.CreateVersion7(), PersonasDeTeste.KevinCarvalho);
        outroProcesso.Should().BeNull("o mesmo CPF noutro processo não é a mesma inscrição");
    }

    [Fact(DisplayName = "A linha crua não contém o CPF nem o número de documento em texto claro")]
    public async Task ColunaCifrada_NuncaContemCpfNemDocumentoEmTextoClaro()
    {
        Guid processo = Guid.CreateVersion7();
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, processo);
        original.Salvar(
            null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["RG_NUMERO"] = JsonSerializer.SerializeToElement("12.868.927-4"),
            },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal),
            [],
            PrecondicaoIfMatch.DeTags([original.ETag]));

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        InscricaoRepository writeRepository = new(writeContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        await writeRepository.AdicionarAsync(original);
        await writeContext.SaveChangesAsync();

        await using NpgsqlConnection connection = new(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT cpf_ciphertext, documentos_cifrados, respostas FROM selecao.inscricoes WHERE id = @id";
        command.Parameters.AddWithValue("id", original.Id);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        byte[] cpfCiphertext = (byte[])reader["cpf_ciphertext"];
        byte[] documentosCifrados = (byte[])reader["documentos_cifrados"];
        string respostas = (string)reader["respostas"];

        Encoding.UTF8.GetString(cpfCiphertext).Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor);
        Encoding.UTF8.GetString(documentosCifrados).Should().NotContain("12.868.927-4");
        respostas.Should().NotContain("12.868.927-4", "o número de documento nunca entra no jsonb em claro");
    }

    [Fact(DisplayName = "Dois inícios simultâneos do mesmo CPF no mesmo processo colidem no índice único, não geram duas linhas")]
    public async Task IndiceUnico_RecusaDuasInscricoesDoMesmoCpfNoMesmoProcesso()
    {
        Guid processo = Guid.CreateVersion7();

        await using SelecaoDbContext primeiroContexto = _fixture.CreateDbContext();
        InscricaoRepository primeiroRepositorio = new(primeiroContexto, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        await primeiroRepositorio.AdicionarAsync(NovaInscricao(PersonasDeTeste.FernandaDrumond, processo));
        await primeiroContexto.SaveChangesAsync();

        await using SelecaoDbContext segundoContexto = _fixture.CreateDbContext();
        InscricaoRepository segundoRepositorio = new(segundoContexto, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        await segundoRepositorio.AdicionarAsync(NovaInscricao(PersonasDeTeste.FernandaDrumond, processo));

        Func<Task> segundaGravacao = async () => await segundoContexto.SaveChangesAsync();
        await segundaGravacao.Should().ThrowAsync<DbUpdateException>(
            "o índice único (processo_seletivo_id, cpf_indice) recusa a segunda linha — quem chama traduz isso em 'devolver a existente', não um 500");
    }
}
