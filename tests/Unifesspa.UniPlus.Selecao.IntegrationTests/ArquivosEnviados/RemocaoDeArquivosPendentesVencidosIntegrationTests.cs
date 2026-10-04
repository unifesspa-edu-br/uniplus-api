namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ArquivosEnviados;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

/// <summary>
/// Remoção dos envios de arquivo pendentes vencidos contra Postgres e MinIO reais: a consulta e a
/// remoção condicionadas ao pendente vencido são SQL, e a remoção do objeto ausente é semântica do
/// armazenamento — nenhum dos dois é alcançado por dublê.
/// </summary>
public sealed class RemocaoDeArquivosPendentesVencidosIntegrationTests
    : IClassFixture<ProcessoSeletivoDbFixture>, IClassFixture<MinioContainerFixture>
{
    private const string TestBucket = "uniplus-pendentes-test";
    private static readonly TimeSpan Vencido = TimeSpan.FromMinutes(-1);
    private static readonly TimeSpan NoPrazo = TimeSpan.FromMinutes(15);
    private static readonly byte[] Conteudo = [.. "%PDF-1.7 envio de teste"u8];

    private readonly ProcessoSeletivoDbFixture _dbFixture;

    [SuppressMessage(
        "Performance",
        "CA1859:Use concrete types when possible for improved performance",
        Justification = "Intencional: o teste exercita o port da Application, o mesmo contrato que a rotina consome.")]
    private readonly IArquivoArmazenadoStorage _storage;

    public RemocaoDeArquivosPendentesVencidosIntegrationTests(ProcessoSeletivoDbFixture dbFixture, MinioContainerFixture minio)
    {
        ArgumentNullException.ThrowIfNull(dbFixture);
        ArgumentNullException.ThrowIfNull(minio);
        _dbFixture = dbFixture;

        Dictionary<string, string?> config = new()
        {
            ["Storage:Endpoint"] = minio.Endpoint,
            ["Storage:AccessKey"] = MinioContainerFixture.AccessKey,
            ["Storage:SecretKey"] = MinioContainerFixture.SecretKey,
            ["Storage:BucketName"] = TestBucket,
        };
        ServiceCollection services = new();
        services.AddUniPlusStorage(
            new ConfigurationBuilder().AddInMemoryCollection(config).Build(),
            new HostingEnvironment { EnvironmentName = Environments.Production });
        ServiceProvider provider = services.BuildServiceProvider();
        _storage = new ArquivoArmazenadoStorageService(
            provider.GetRequiredService<IStorageService>(), provider.GetRequiredService<IOptions<StorageOptions>>());
    }

    [Fact(DisplayName = "Pendente vencido do documento do Edital e do modelo sai do banco e do armazenamento")]
    public async Task PendenteVencido_SaiDoBancoEDoArmazenamento()
    {
        Guid processoId = await NovoProcessoAsync();
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processoId, TimeProvider.System, Vencido);
        ModeloDeDocumento modelo = Modelo(processoId, Vencido);
        await PersistirComObjetoAsync(documento, documento.ObjectKey, modelo, modelo.ObjectKey);

        await ExecutarRemocaoAsync();

        (await ExisteDocumentoAsync(documento.Id)).Should().BeFalse();
        (await ExisteModeloAsync(modelo.Id)).Should().BeFalse();
        (await _storage.ObterInfoAsync(documento.ObjectKey)).Should().BeNull();
        (await _storage.ObterInfoAsync(modelo.ObjectKey)).Should().BeNull();
    }

    [Fact(DisplayName = "Pendente no prazo permanece com o objeto")]
    public async Task PendenteNoPrazo_Permanece()
    {
        Guid processoId = await NovoProcessoAsync();
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processoId, TimeProvider.System, NoPrazo);
        ModeloDeDocumento modelo = Modelo(processoId, NoPrazo);
        await PersistirComObjetoAsync(documento, documento.ObjectKey, modelo, modelo.ObjectKey);

        await ExecutarRemocaoAsync();

        (await ExisteDocumentoAsync(documento.Id)).Should().BeTrue();
        (await ExisteModeloAsync(modelo.Id)).Should().BeTrue();
        (await _storage.ObterInfoAsync(documento.ObjectKey)).Should().NotBeNull();
        (await _storage.ObterInfoAsync(modelo.ObjectKey)).Should().NotBeNull();
    }

    [Fact(DisplayName = "Confirmado permanece com o objeto, mesmo com o prazo de envio vencido")]
    public async Task Confirmado_Permanece()
    {
        Guid processoId = await NovoProcessoAsync();
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processoId, TimeProvider.System, Vencido);
        documento.Confirmar(Conteudo.Length, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        ModeloDeDocumento modelo = Modelo(processoId, Vencido);
        modelo.Confirmar(Conteudo.Length, new string('b', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        await PersistirComObjetoAsync(documento, documento.ObjectKeyConfirmado!, modelo, modelo.ObjectKeyConfirmado!);

        await ExecutarRemocaoAsync();

        (await ExisteDocumentoAsync(documento.Id)).Should().BeTrue();
        (await ExisteModeloAsync(modelo.Id)).Should().BeTrue();
        (await _storage.ObterInfoAsync(documento.ObjectKeyConfirmado!)).Should().NotBeNull();
        (await _storage.ObterInfoAsync(modelo.ObjectKeyConfirmado!)).Should().NotBeNull();
    }

    [Fact(DisplayName = "Objeto já ausente do armazenamento não impede a remoção do registro vencido")]
    public async Task ObjetoAusente_NaoImpedeARemocao()
    {
        Guid processoId = await NovoProcessoAsync();
        // O bucket existe — o que falta é só o objeto do pendente.
        await _storage.SalvarConteudoSeladoAsync($"selecao/outro/{Guid.CreateVersion7():D}.pdf", Conteudo, DocumentoEdital.ContentTypeEsperado);
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processoId, TimeProvider.System, Vencido);
        await using (SelecaoDbContext context = _dbFixture.CreateDbContext())
        {
            context.DocumentosEdital.Add(documento);
            await context.SaveChangesAsync();
        }

        await ExecutarRemocaoAsync();

        (await ExisteDocumentoAsync(documento.Id)).Should().BeFalse();
    }

    [Fact(DisplayName = "A remoção do documento vencido não alcança o que a confirmação concorrente reivindicou primeiro")]
    public async Task RemocaoCondicionada_DocumentoReivindicadoPelaConfirmacao_Permanece()
    {
        Guid processoId = await NovoProcessoAsync();
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processoId, TimeProvider.System, Vencido);
        await using (SelecaoDbContext context = _dbFixture.CreateDbContext())
        {
            context.DocumentosEdital.Add(documento);
            await context.SaveChangesAsync();
        }

        bool removeu = await RemoverDuranteAConfirmacaoAsync(
            context => new DocumentoEditalRepository(context).TentarReivindicarConfirmacaoAsync(documento.Id),
            context => new DocumentoEditalRepository(context).RemoverSePendenteVencidoAsync(documento.Id, TimeProvider.System.GetUtcNow()));

        removeu.Should().BeFalse();
        await using SelecaoDbContext leitura = _dbFixture.CreateDbContext();
        (await leitura.DocumentosEdital.AsNoTracking().SingleAsync(d => d.Id == documento.Id))
            .Status.Should().Be(StatusArquivoEnviado.Confirmado);
    }

    [Fact(DisplayName = "A remoção do modelo vencido não alcança o que a confirmação concorrente reivindicou primeiro")]
    public async Task RemocaoCondicionada_ModeloReivindicadoPelaConfirmacao_Permanece()
    {
        Guid processoId = await NovoProcessoAsync();
        ModeloDeDocumento modelo = Modelo(processoId, Vencido);
        await using (SelecaoDbContext context = _dbFixture.CreateDbContext())
        {
            context.ModelosDeDocumento.Add(modelo);
            await context.SaveChangesAsync();
        }

        bool removeu = await RemoverDuranteAConfirmacaoAsync(
            context => new ModeloDeDocumentoRepository(context).TentarReivindicarConfirmacaoAsync(modelo.Id),
            context => new ModeloDeDocumentoRepository(context).RemoverSePendenteVencidoAsync(modelo.Id, TimeProvider.System.GetUtcNow()));

        removeu.Should().BeFalse();
        await using SelecaoDbContext leitura = _dbFixture.CreateDbContext();
        (await leitura.ModelosDeDocumento.AsNoTracking().SingleAsync(m => m.Id == modelo.Id))
            .Status.Should().Be(StatusArquivoEnviado.Confirmado);
    }

    /// <summary>
    /// A confirmação reivindica o registro numa transação ainda aberta; a remoção, em outra
    /// conexão, espera o bloqueio de linha e só reavalia a condição depois do commit.
    /// </summary>
    private async Task<bool> RemoverDuranteAConfirmacaoAsync(
        Func<SelecaoDbContext, Task<bool>> reivindicar, Func<SelecaoDbContext, Task<bool>> remover)
    {
        await using SelecaoDbContext confirmacao = _dbFixture.CreateDbContext();
        await using IDbContextTransaction transacao = await confirmacao.Database.BeginTransactionAsync();
        (await reivindicar(confirmacao)).Should().BeTrue();

        await using SelecaoDbContext varredura = _dbFixture.CreateDbContext();
        Task<bool> remocao = remover(varredura);
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        remocao.IsCompleted.Should().BeFalse("a remoção espera o bloqueio de linha da confirmação em curso");

        await transacao.CommitAsync();
        return await remocao;
    }

    private async Task ExecutarRemocaoAsync()
    {
        await using SelecaoDbContext context = _dbFixture.CreateDbContext();
        RemocaoDeArquivosPendentesVencidos remocao = new(
            new DocumentoEditalRepository(context),
            new ModeloDeDocumentoRepository(context),
            _storage,
            TimeProvider.System,
            NullLogger<RemocaoDeArquivosPendentesVencidos>.Instance);
        await remocao.ExecutarAsync(CancellationToken.None);
    }

    private async Task PersistirComObjetoAsync(
        DocumentoEdital documento, string chaveDoDocumento, ModeloDeDocumento modelo, string chaveDoModelo)
    {
        await using SelecaoDbContext context = _dbFixture.CreateDbContext();
        context.DocumentosEdital.Add(documento);
        context.ModelosDeDocumento.Add(modelo);
        await context.SaveChangesAsync();
        await _storage.SalvarConteudoSeladoAsync(chaveDoDocumento, Conteudo, DocumentoEdital.ContentTypeEsperado);
        await _storage.SalvarConteudoSeladoAsync(chaveDoModelo, Conteudo, modelo.ContentType);
    }

    private async Task<bool> ExisteDocumentoAsync(Guid id)
    {
        await using SelecaoDbContext context = _dbFixture.CreateDbContext();
        return await context.DocumentosEdital.AsNoTracking().AnyAsync(d => d.Id == id);
    }

    private async Task<bool> ExisteModeloAsync(Guid id)
    {
        await using SelecaoDbContext context = _dbFixture.CreateDbContext();
        return await context.ModelosDeDocumento.AsNoTracking().AnyAsync(m => m.Id == id);
    }

    private static ModeloDeDocumento Modelo(Guid processoId, TimeSpan ttl) =>
        ModeloDeDocumento.IniciarPendente(processoId, "Autodeclaração", "ODT", TimeProvider.System, ttl).Value!;

    private async Task<Guid> NovoProcessoAsync()
    {
        await using SelecaoDbContext context = _dbFixture.CreateDbContext();
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2027 — envios pendentes", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        context.ProcessosSeletivos.Add(processo);
        await context.SaveChangesAsync();
        return processo.Id;
    }
}
