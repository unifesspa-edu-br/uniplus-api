namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// Identificador legível contra o Postgres real (issue #1479): o índice único é a última defesa
/// da unicidade, e a consulta do repositório tem de enxergar o que o índice enxerga.
/// </summary>
public sealed class IdentificadorLegivelPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public IdentificadorLegivelPersistenciaTests(ProcessoSeletivoDbFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Persiste e recarrega o identificador legível")]
    public async Task PersisteERecarrega()
    {
        IdentificadorLegivel identificador = IdentificadoresDeTeste.Novo();
        ProcessoSeletivo processo = NovoProcesso(identificador);
        await PersistirAsync(processo);

        await using SelecaoDbContext ctx = _fixture.CreateDbContext();
        ProcessoSeletivo recarregado = await ctx.ProcessosSeletivos.AsNoTracking().SingleAsync(p => p.Id == processo.Id);

        recarregado.IdentificadorLegivel.Should().Be(identificador);
    }

    [Fact(DisplayName = "Dois processos sem identificador convivem: o índice único ignora os ausentes")]
    public async Task SemIdentificador_Convivem()
    {
        await PersistirAsync(NovoProcesso(null));
        Func<Task> segundo = () => PersistirAsync(NovoProcesso(null));

        await segundo.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "O banco recusa o mesmo identificador em dois processos")]
    public async Task MesmoIdentificador_BancoRecusa()
    {
        IdentificadorLegivel identificador = IdentificadoresDeTeste.Novo();
        await PersistirAsync(NovoProcesso(identificador));

        Func<Task> duplicar = () => PersistirAsync(NovoProcesso(identificador));

        PostgresException erro = (await duplicar.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>().Which;
        erro.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        erro.ConstraintName.Should().Be("ix_processos_seletivos_identificador_legivel");
    }

    [Fact(DisplayName = "A consulta de unicidade enxerga processo excluído logicamente, como o índice")]
    public async Task EmUso_IncluiExcluidoLogicamente()
    {
        IdentificadorLegivel identificador = IdentificadoresDeTeste.Novo();
        ProcessoSeletivo excluido = NovoProcesso(identificador);
        excluido.MarkAsDeleted("teste", DateTimeOffset.UtcNow);
        await PersistirAsync(excluido);

        await using SelecaoDbContext ctx = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(ctx, TimeProvider.System);

        (await repository.IdentificadorLegivelEmUsoAsync(identificador, excluirId: null, CancellationToken.None))
            .Should().BeTrue("o endereço público de um certame excluído não pode ser herdado por outro");
        (await repository.IdentificadorLegivelEmUsoAsync(identificador, excluido.Id, CancellationToken.None))
            .Should().BeFalse("o próprio processo não conflita consigo mesmo");
    }

    private static ProcessoSeletivo NovoProcesso(IdentificadorLegivel? identificador) => ProcessoSeletivo.Criar(
        "PS Identificador", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
        identificador);

    private async Task PersistirAsync(ProcessoSeletivo processo)
    {
        await using SelecaoDbContext ctx = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(ctx, TimeProvider.System);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await ctx.SaveChangesAsync(CancellationToken.None);
    }
}
