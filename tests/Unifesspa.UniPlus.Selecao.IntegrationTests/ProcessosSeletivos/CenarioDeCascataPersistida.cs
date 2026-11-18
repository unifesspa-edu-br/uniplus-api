namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// Montagem compartilhada dos testes de persistência da cascata de remanejamento (Postgres real):
/// a cascata de duas origens, a referência de regra e a gravação do processo sem publicá-lo.
/// </summary>
internal sealed class CenarioDeCascataPersistida
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public CenarioDeCascataPersistida(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    public string ConnectionString => _fixture.ConnectionString;

    public UniPlusDbContext CriarDbContext() => _fixture.CreateDbContext();

    public static ReferenciaRegra Regra(string codigo, string hashChar) =>
        ReferenciaRegra.De(codigo, "v1", new string(hashChar[0], 64));

    public static ConfiguracaoCascataRemanejamento CascataDeDuasOrigens(string semente) =>
        ConfiguracaoCascataRemanejamento.Criar(
            Regra(RegraRemanejamentoCodigo.Cascata, semente),
            "AC",
            [
                DestinoRemanejamento.Criar("LB_PPI", 1, "LB_Q").Value!,
                DestinoRemanejamento.Criar("LB_PPI", 2, "AC").Value!,
                DestinoRemanejamento.Criar("LB_Q", 1, "AC").Value!,
            ]).Value!;

    public async Task PersistirAsync(ProcessoSeletivo processo)
    {
        await using UniPlusDbContext writeContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(writeContext);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);
    }
}
