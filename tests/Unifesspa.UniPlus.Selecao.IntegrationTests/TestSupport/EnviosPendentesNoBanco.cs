namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

/// <summary>
/// Os registros de envio pendente no Postgres do teste e a rotina que os remove, com o armazenamento
/// escolhido pelo teste.
/// </summary>
internal sealed class EnviosPendentesNoBanco(ProcessoSeletivoDbFixture banco, IArquivoArmazenadoStorage armazenamento)
{
    public async Task<Guid> NovoProcessoAsync()
    {
        await using UniPlusDbContext context = banco.CreateDbContext();
        return (await ProcessoSeletivoDeTeste.GravarAsync(context, "PS 2027 — envios pendentes")).Id;
    }

    public async Task PersistirAsync(DocumentoAPublicar documento)
    {
        await using UniPlusDbContext context = banco.CreateDbContext();
        context.DocumentosAPublicar.Add(documento);
        await context.SaveChangesAsync();
    }

    public async Task PersistirAsync(ModeloDeDocumento modelo)
    {
        await using UniPlusDbContext context = banco.CreateDbContext();
        context.ModelosDeDocumento.Add(modelo);
        await context.SaveChangesAsync();
    }

    public async Task<bool> ExisteDocumentoAsync(Guid id)
    {
        await using UniPlusDbContext context = banco.CreateDbContext();
        return await context.DocumentosAPublicar.AsNoTracking().AnyAsync(d => d.Id == id);
    }

    public async Task<bool> ExisteModeloAsync(Guid id)
    {
        await using UniPlusDbContext context = banco.CreateDbContext();
        return await context.ModelosDeDocumento.AsNoTracking().AnyAsync(m => m.Id == id);
    }

    /// <summary>Executa a rotina com o <paramref name="armazenamentoDaRotina"/>, ou com o do teste quando não informado.</summary>
    public async Task ExecutarRemocaoAsync(IArquivoArmazenadoStorage? armazenamentoDaRotina = null)
    {
        await using UniPlusDbContext context = banco.CreateDbContext();
        RemocaoDeArquivosPendentesVencidos remocao = new(
            new DocumentoAPublicarRepository(context),
            new ModeloDeDocumentoRepository(context),
            new DocumentoDaInscricaoRepository(context),
            armazenamentoDaRotina ?? armazenamento,
            Substitute.For<IArquivoDoCandidatoStorage>(),
            TimeProvider.System,
            NullLogger<RemocaoDeArquivosPendentesVencidos>.Instance);
        await remocao.ExecutarAsync(CancellationToken.None);
    }
}
