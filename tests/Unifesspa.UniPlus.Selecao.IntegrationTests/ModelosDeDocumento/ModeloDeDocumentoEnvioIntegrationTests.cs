namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento;

using System.Security.Cryptography;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Integração real (Testcontainers Postgres + MinIO) do envio do modelo de documento: iniciar →
/// PUT pré-assinado direto ao MinIO com o content-type do formato → confirmar → a URL de acesso
/// abre a cópia selada, com o mesmo hash.
/// </summary>
public sealed class ModeloDeDocumentoEnvioIntegrationTests : IClassFixture<ProcessoSeletivoDbFixture>, IClassFixture<MinioContainerFixture>
{
    private const string TestBucket = "uniplus-documentos-test";

    private readonly ProcessoSeletivoDbFixture _dbFixture;
    private readonly ArquivoArmazenadoStorageService _storage;

    public ModeloDeDocumentoEnvioIntegrationTests(ProcessoSeletivoDbFixture dbFixture, MinioContainerFixture minio)
    {
        ArgumentNullException.ThrowIfNull(dbFixture);
        _dbFixture = dbFixture;
        _storage = new ArmazenamentoDeTesteNoMinio(minio, TestBucket).ArquivosArmazenados();
    }

    [Fact(DisplayName = "Os modelos citados são lidos só do processo: o modelo de outro processo não volta")]
    public async Task ListarDoProcesso_SoDevolveOsModelosDoProcesso()
    {
        await using SelecaoDbContext context = _dbFixture.CreateDbContext();
        ProcessoSeletivo processoA = ProcessoSeletivoDeTeste.Novo("PS A — modelo de documento");
        ProcessoSeletivo processoB = ProcessoSeletivoDeTeste.Novo("PS B — modelo de documento");
        context.ProcessosSeletivos.AddRange(processoA, processoB);
        ModeloDeDocumento doA = EnviosPendentesDeTeste.Modelo(processoA.Id);
        ModeloDeDocumento doB = EnviosPendentesDeTeste.Modelo(processoB.Id);
        context.ModelosDeDocumento.AddRange(doA, doB);
        await context.SaveChangesAsync();

        IReadOnlyList<ModeloDeDocumento> lidos = await new ModeloDeDocumentoRepository(context)
            .ListarDoProcessoAsync(processoA.Id, [doA.Id, doB.Id], CancellationToken.None);

        lidos.Select(static m => m.Id).Should().Equal(doA.Id);
    }

    [Fact(DisplayName = "Fluxo completo: o ODT enviado é confirmado com o hash local, e a URL de acesso abre a cópia selada")]
    public async Task FluxoCompleto_OdtConfirmado_AcessoAbreACopiaSelada()
    {
        byte[] odt = ArquivosDeModeloDeTeste.Odt();
        await using EnvioDoModeloDeDocumento envio = await EnvioDoModeloDeDocumento.NovoAsync(_dbFixture, _storage, "PS 2027 — modelo de documento");
        IniciarEnvioDoModeloDeDocumentoDto iniciar = await envio.IniciarEEnviarAsync(odt, "Declaração de pertencimento quilombola");

        Result<ModeloDeDocumentoDto> confirmar = await envio.ConfirmarAsync(iniciar.ModeloDeDocumentoId);

        confirmar.IsSuccess.Should().BeTrue(confirmar.Error?.Message);
        confirmar.Value!.HashSha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(odt)));
        confirmar.Value.NomeArquivo.Should().Be("Declaração de pertencimento quilombola.odt");
        (await envio.BaixarPeloAcessoAsync(iniciar.ModeloDeDocumentoId)).Should().Equal(odt);
    }
}
