namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ManifestacaoDeInteressePersistenciaTests(ProcessoSeletivoDbFixture fixture) : IClassFixture<ProcessoSeletivoDbFixture>
{
    [Theory(DisplayName = "A manifestação é gravada pelo código de contrato e lida de volta igual")]
    [InlineData("UMA_VEZ", ManifestacaoDeInteresse.UmaVez)]
    [InlineData("CONFIRMA_A_CADA_CHAMADA", ManifestacaoDeInteresse.ConfirmaACadaChamada)]
    public async Task Manifestacao_GravaELe(string codigo, ManifestacaoDeInteresse esperada)
    {
        Guid id = await GravarAsync(codigo);

        (await LerAsync(id)).Should().Be(esperada);
        (await LerColunaAsync(id)).Should().Be(codigo);
    }

    [Fact(DisplayName = "O processo sem declaração fica com a coluna nula, a manifestação não prevista")]
    public async Task SemDeclaracao_ColunaNula()
    {
        Guid id = await GravarAsync(null);

        (await LerAsync(id)).Should().BeNull();
        (await LerColunaAsync(id)).Should().BeNull();
    }

    [Fact(DisplayName = "A nova definição troca o valor e o código nulo volta a manifestação não prevista")]
    public async Task NovaDefinicao_TrocaEDepoisLimpa()
    {
        Guid id = await GravarAsync("UMA_VEZ");

        await DefinirAsync(id, "CONFIRMA_A_CADA_CHAMADA");
        (await LerAsync(id)).Should().Be(ManifestacaoDeInteresse.ConfirmaACadaChamada);

        await DefinirAsync(id, null);
        (await LerAsync(id)).Should().BeNull();
    }

    [Fact(DisplayName = "O banco recusa um código fora dos dois que o contrato oferece")]
    public async Task Banco_RecusaCodigoForaDoContrato()
    {
        Guid id = await GravarAsync("UMA_VEZ");
        await using UniPlusDbContext db = fixture.CreateDbContext();

        Func<Task> gravar = async () => await db.Database.ExecuteSqlRawAsync(
            "UPDATE selecao.processos_seletivos SET manifestacao_de_interesse = 'OUTRA' WHERE id = {0}", id);

        await gravar.Should().ThrowAsync<Exception>();
    }

    private async Task<Guid> GravarAsync(string? codigo)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirManifestacaoDeInteresse(codigo).IsSuccess.Should().BeTrue();
        db.ProcessosSeletivos.Add(processo);
        await db.SaveChangesAsync();
        return processo.Id;
    }

    private async Task DefinirAsync(Guid id, string? codigo)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.SingleAsync(p => p.Id == id);
        processo.DefinirManifestacaoDeInteresse(codigo).IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
    }

    private async Task<ManifestacaoDeInteresse?> LerAsync(Guid id)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        return (await db.ProcessosSeletivos.AsNoTracking().SingleAsync(p => p.Id == id)).ManifestacaoDeInteresse;
    }

    private async Task<string?> LerColunaAsync(Guid id)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        return await db.Database.SqlQueryRaw<string?>(
            "SELECT manifestacao_de_interesse AS \"Value\" FROM selecao.processos_seletivos WHERE id = {0}", id).SingleAsync();
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "Processo com manifestação de interesse", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.CreateVersion7(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
}
