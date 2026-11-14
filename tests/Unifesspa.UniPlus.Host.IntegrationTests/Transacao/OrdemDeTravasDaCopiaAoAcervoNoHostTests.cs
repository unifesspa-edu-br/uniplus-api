namespace Unifesspa.UniPlus.Host.IntegrationTests.Transacao;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Host.IntegrationTests.Perfis;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Wolverine;

/// <summary>
/// A cópia dos documentos de um ato ao acervo copia sem trava e só depois trava o processo e lê a divulgação (ADR-0170):
/// uma publicação concorrente que avança a divulgação enquanto a cópia espera a trava não faz a cópia falhar por versão
/// defasada, e o endereço entra na divulgação que a publicação deixou.
/// </summary>
[Collection(MonolitoHostCollection.Name)]
public sealed class OrdemDeTravasDaCopiaAoAcervoNoHostTests : IClassFixture<HostComAcervoPublicoSemArmazenamento>
{
    private readonly IServiceProvider _servicos;

    public OrdemDeTravasDaCopiaAoAcervoNoHostTests(MonolitoPostgresFixture fixture, HostComAcervoPublicoSemArmazenamento host)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(host);
        _servicos = host.Servicos(fixture);
    }

    [Fact(DisplayName = "A cópia copia antes de esperar o processo e lê a divulgação depois da trava, sem conflito de versão")]
    public async Task CopiaEsperaOProcessoDepoisDeCopiar_ELeADivulgacaoDepoisDaTrava()
    {
        AtoAvulsoPendenteDeCopiaSemeado semeado = await AtoAvulsoPendenteDeCopiaSemeado.SemearAsync(_servicos);
        Guid outroAto = Guid.CreateVersion7();

        await CopiarEnquantoAPublicacaoSeguraOProcessoAsync(semeado, outroAto);

        await ConferirDivulgacaoAsync(semeado, outroAto);
    }

    /// <summary>
    /// Uma publicação trava o processo e avança a divulgação sem confirmar; a cópia chega, copia e espera a trava. A
    /// publicação confirma só depois de o teste ver a cópia feita, e a cópia conclui depois.
    /// </summary>
    private async Task CopiarEnquantoAPublicacaoSeguraOProcessoAsync(AtoAvulsoPendenteDeCopiaSemeado semeado, Guid outroAto)
    {
        await using TransacaoRetidaDeTeste publicacao = await TransacaoRetidaDeTeste.AbrirAsync(_servicos);
        await publicacao.Servicos.GetRequiredService<IProcessoSeletivoRepository>().TravarAsync(semeado.ProcessoId);
        await AvancarADivulgacaoAsync(publicacao, semeado.ProcessoId, outroAto);
        Task copia = CopiarEmSegundoPlano(semeado);

        await ConferirCopiadoEEsperandoAsync(publicacao, copia, semeado.AtoId);
        await publicacao.ConfirmarAsync();

        await copia;
    }

    private async Task ConferirCopiadoEEsperandoAsync(TransacaoRetidaDeTeste publicacao, Task copia, Guid atoId)
    {
        Task bloqueio = publicacao.AguardarBloqueioAsync();
        await Task.WhenAny(bloqueio, copia);
        if (copia.IsCompleted)
        {
            await copia;
        }

        await bloqueio;
        AcervoDeTeste().CopiasDoAto(atoId).Should().Be(1, "a cópia ao acervo não depende da trava do processo");
    }

    private AcervoPublicoSemArmazenamento AcervoDeTeste() => _servicos.GetRequiredService<AcervoPublicoSemArmazenamento>();

    private static async Task AvancarADivulgacaoAsync(TransacaoRetidaDeTeste publicacao, Guid processoId, Guid outroAto)
    {
        CertameDivulgado divulgado = (await publicacao.Servicos.GetRequiredService<ICertameDivulgadoRepository>()
            .ObterParaMutacaoAsync(processoId))!;
        FacetasDoCertameDivulgado facetas = new(
            divulgado.IdentificadorLegivel, divulgado.Nome, divulgado.Numero, divulgado.ModalidadesOfertadas,
            divulgado.InscricoesDe, divulgado.InscricoesAte);

        divulgado.Avancar(2, outroAto, new string('b', 64), divulgado.VersaoProjecao, facetas, divulgado.DocumentoPublico, DateTimeOffset.UtcNow);
        divulgado.AbrirEntradaDoAto(outroAto, numeroVersao: 2);

        await publicacao.Servicos.GetRequiredService<UniPlusDbContext>().SaveChangesAsync();
    }

    private Task CopiarEmSegundoPlano(AtoAvulsoPendenteDeCopiaSemeado semeado) =>
        Task.Run(async () =>
        {
            await using AsyncServiceScope escopo = _servicos.CreateAsyncScope();

            await escopo.ServiceProvider.GetRequiredService<IMessageBus>()
                .InvokeAsync(new CopiarDocumentosDoAtoAoAcervo(semeado.ProcessoId, semeado.AtoId));
        });

    private async Task ConferirDivulgacaoAsync(AtoAvulsoPendenteDeCopiaSemeado semeado, Guid outroAto)
    {
        await using AsyncServiceScope escopo = _servicos.CreateAsyncScope();
        CertameDivulgado divulgado = await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>()
            .Set<CertameDivulgado>().AsNoTracking().SingleAsync(c => c.Id == semeado.ProcessoId);

        divulgado.NumeroVersao.Should().Be(2, "a divulgação que a publicação avançou é a que recebe o endereço");
        divulgado.CopiouOsDocumentosDoAto(semeado.AtoId).Should().BeTrue();
        divulgado.CopiouOsDocumentosDoAto(outroAto).Should().BeFalse("a cópia de um ato não toca na entrada de outro");
    }
}
