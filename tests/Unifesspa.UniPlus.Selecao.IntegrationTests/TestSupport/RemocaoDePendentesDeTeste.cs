namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

internal static class RemocaoDePendentesDeTeste
{
    public static async Task ExecutarAsync(ProcessoSeletivoDbFixture banco, ArquivoArmazenadoStorageService armazenamento, TimeProvider relogio)
    {
        await using UniPlusDbContext contexto = banco.CreateDbContext();
        RemocaoDeArquivosPendentesVencidos remocao = new(
            new DocumentoAPublicarRepository(contexto),
            new ModeloDeDocumentoRepository(contexto),
            new DocumentoDaInscricaoRepository(contexto),
            armazenamento,
            Substitute.For<IArquivoDoCandidatoStorage>(),
            relogio,
            NullLogger<RemocaoDeArquivosPendentesVencidos>.Instance);
        await remocao.ExecutarAsync(CancellationToken.None);
    }
}
