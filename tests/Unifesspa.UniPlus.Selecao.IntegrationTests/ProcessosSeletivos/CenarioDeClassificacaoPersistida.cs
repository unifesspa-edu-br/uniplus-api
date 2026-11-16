namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Montagem compartilhada dos testes de persistência da classificação (Postgres real): o processo
/// seletivo de teste, as referências de regra congeladas e a gravação do agregado.
/// </summary>
internal sealed class CenarioDeClassificacaoPersistida
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public CenarioDeClassificacaoPersistida(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    public UniPlusDbContext CriarDbContext() => _fixture.CreateDbContext();

    public static ReferenciaRegra Regra(string codigo, string hashChar) =>
        ReferenciaRegra.De(codigo, "v1", new string(hashChar[0], 64));

    public static ProcessoSeletivo NovoProcesso(string nome, TipoProcessoSnapshot tipo) =>
        ProcessoSeletivo.Criar(
            nome, tipo, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
            IdentificadoresDeTeste.Novo());

    public static TipoEtapaSnapshot TipoDeEtapaProvaObjetiva() =>
        TipoEtapaSnapshot.Criar(
            Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva",
            admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

    public async Task PersistirAsync(ProcessoSeletivo processo)
    {
        await using UniPlusDbContext writeContext = CriarDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(writeContext);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);
    }
}
