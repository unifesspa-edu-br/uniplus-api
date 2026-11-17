namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Os rascunhos e processos que as suítes do rascunho gravam no banco, e o repositório com a espera que elas precisam.</summary>
internal static class RascunhosSemeadosParaTeste
{
    public static readonly TimeSpan EsperaCurta = TimeSpan.FromMilliseconds(300);

    public static RascunhoDeProcessoSeletivoRepository Repositorio(UniPlusDbContext contexto) =>
        new(contexto, Options.Create(new TravaDoProcessoOptions { EsperaMaxima = EsperaCurta }));

    public static async Task<Guid> SemearRascunhoAsync(
        ProcessoSeletivoDbFixture fixture, Guid unidadeId, string conteudo = """{"identificacao":{"nome":"Processo Seletivo Modelo"}}""", string? nome = "Processo Seletivo Modelo")
    {
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        RascunhoDeProcessoSeletivo rascunho = RascunhoDeProcessoSeletivo.Criar(unidadeId, DocumentoDoRascunho.Criar(conteudo, nome).Value!).Value!;
        await Repositorio(contexto).AdicionarAsync(rascunho);
        await contexto.SaveChangesAsync();
        return rascunho.Id;
    }

    public static async Task<ProcessoSeletivo> SemearProcessoAsync(ProcessoSeletivoDbFixture fixture, Guid unidadeId)
    {
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            $"Certame de teste {Guid.CreateVersion7()}",
            TiposDeProcessoDeTeste.SiSU,
            OrigemCandidatos.InscricaoPropria,
            unidadeId,
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        contexto.ProcessosSeletivos.Add(processo);
        await contexto.SaveChangesAsync();
        return processo;
    }

    /// <summary>O processo excluído logicamente: a linha continua no banco, e o filtro global o tira de alcance.</summary>
    public static async Task<ProcessoSeletivo> SemearProcessoExcluidoAsync(ProcessoSeletivoDbFixture fixture, Guid unidadeId)
    {
        ProcessoSeletivo processo = await SemearProcessoAsync(fixture, unidadeId);
        await using UniPlusDbContext contexto = fixture.CreateDbContext();
        ProcessoSeletivo gravado = await contexto.ProcessosSeletivos.SingleAsync(p => p.Id == processo.Id);
        gravado.MarkAsDeleted("conta-de-teste", TimeProvider.System.GetUtcNow());
        await contexto.SaveChangesAsync();
        return processo;
    }
}
