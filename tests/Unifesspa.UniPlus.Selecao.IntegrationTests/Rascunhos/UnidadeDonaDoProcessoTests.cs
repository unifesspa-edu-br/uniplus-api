namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using static RascunhosSemeadosParaTeste;

/// <summary>A unidade dona vem da linha do processo ou, antes da primeira publicação, da linha do rascunho, numa consulta só.</summary>
public sealed class UnidadeDonaDoProcessoTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public UnidadeDonaDoProcessoTests(ProcessoSeletivoDbFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    [Fact(DisplayName = "O processo publicado responde com a unidade da linha dele")]
    public async Task Processo_RespondeComAUnidadeDoProcesso()
    {
        Guid unidade = Guid.CreateVersion7();
        ProcessoSeletivo processo = await SemearProcessoAsync(_fixture, unidade);

        (await DoProcessoAsync(processo.Id)).Should().Be(unidade);
    }

    [Fact(DisplayName = "O rascunho, que ainda não tem linha de processo, responde com a unidade da linha dele")]
    public async Task Rascunho_RespondeComAUnidadeDoRascunho()
    {
        Guid unidade = Guid.CreateVersion7();
        Guid id = await SemearRascunhoAsync(_fixture, unidade);

        (await DoProcessoAsync(id)).Should().Be(unidade);
    }

    [Fact(DisplayName = "Sem processo nem rascunho com o identificador, a resposta é nula")]
    public async Task Inexistente_RespondeNulo()
    {
        (await DoProcessoAsync(Guid.CreateVersion7())).Should().BeNull();
    }

    [Fact(DisplayName = "O processo excluído logicamente não tem unidade dona, como o inexistente")]
    public async Task ProcessoExcluido_RespondeNulo()
    {
        ProcessoSeletivo processo = await SemearProcessoAsync(_fixture, Guid.CreateVersion7());
        await using (UniPlusDbContext contexto = _fixture.CreateDbContext())
        {
            ProcessoSeletivo gravado = contexto.ProcessosSeletivos.Single(p => p.Id == processo.Id);
            gravado.MarkAsDeleted("conta-de-teste", TimeProvider.System.GetUtcNow());
            await contexto.SaveChangesAsync();
        }

        (await DoProcessoAsync(processo.Id)).Should().BeNull();
    }

    [Fact(DisplayName = "A consulta não rastreia nenhuma entidade")]
    public async Task Consulta_NaoRastreia()
    {
        Guid id = await SemearRascunhoAsync(_fixture, Guid.CreateVersion7());
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();

        await new UnidadeDonaDoProcesso(contexto).DoProcessoAsync(id);

        contexto.ChangeTracker.Entries().Should().BeEmpty();
    }

    private async Task<Guid?> DoProcessoAsync(Guid id)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        return await new UnidadeDonaDoProcesso(contexto).DoProcessoAsync(id);
    }
}
