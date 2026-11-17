namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ArquivosEnviados;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// Remoção dos envios de arquivo pendentes vencidos contra Postgres e MinIO reais: a consulta e a
/// remoção condicionadas ao pendente vencido são SQL, e a remoção do objeto ausente é semântica do
/// armazenamento — nenhum dos dois é alcançado por dublê.
/// </summary>
public sealed class RemocaoDeArquivosPendentesVencidosIntegrationTests
    : IClassFixture<ProcessoSeletivoDbFixture>, IClassFixture<MinioContainerFixture>
{
    private const string TestBucket = "uniplus-pendentes-test";
    private static readonly TimeSpan Vencido = -(RemocaoDeArquivosPendentesVencidos.Tolerancia + TimeSpan.FromMinutes(1));
    private static readonly TimeSpan NoPrazo = TimeSpan.FromMinutes(15);
    private static readonly byte[] Conteudo = [.. "%PDF-1.7 envio de teste"u8];

    private readonly ProcessoSeletivoDbFixture _dbFixture;
    private readonly ArquivoArmazenadoStorageService _storage;
    private readonly EnviosPendentesNoBanco _envios;

    public RemocaoDeArquivosPendentesVencidosIntegrationTests(ProcessoSeletivoDbFixture dbFixture, MinioContainerFixture minio)
    {
        ArgumentNullException.ThrowIfNull(dbFixture);
        _dbFixture = dbFixture;
        _storage = new ArmazenamentoDeTesteNoMinio(minio, TestBucket).ArquivosArmazenados();
        _envios = new EnviosPendentesNoBanco(dbFixture, _storage);
    }

    [Fact(DisplayName = "Pendente vencido do documento do Edital e do modelo sai do banco e do armazenamento")]
    public async Task PendenteVencido_SaiDoBancoEDoArmazenamento()
    {
        Guid processoId = await _envios.NovoProcessoAsync();
        DocumentoAPublicar documento = EnviosPendentesDeTeste.Documento(processoId, Vencido);
        ModeloDeDocumento modelo = EnviosPendentesDeTeste.Modelo(processoId, Vencido);
        await PersistirComObjetoAsync(documento, documento.ObjectKey, modelo, modelo.ObjectKey);

        await _envios.ExecutarRemocaoAsync();

        (await _envios.ExisteDocumentoAsync(documento.Id)).Should().BeFalse();
        (await _envios.ExisteModeloAsync(modelo.Id)).Should().BeFalse();
        (await _storage.ObterInfoAsync(documento.ObjectKey)).Should().BeNull();
        (await _storage.ObterInfoAsync(modelo.ObjectKey)).Should().BeNull();
    }

    [Fact(DisplayName = "Pendente no prazo permanece com o objeto")]
    public async Task PendenteNoPrazo_Permanece()
    {
        Guid processoId = await _envios.NovoProcessoAsync();
        DocumentoAPublicar documento = EnviosPendentesDeTeste.Documento(processoId, NoPrazo);
        ModeloDeDocumento modelo = EnviosPendentesDeTeste.Modelo(processoId, NoPrazo);
        await PersistirComObjetoAsync(documento, documento.ObjectKey, modelo, modelo.ObjectKey);

        await _envios.ExecutarRemocaoAsync();

        (await _envios.ExisteDocumentoAsync(documento.Id)).Should().BeTrue();
        (await _envios.ExisteModeloAsync(modelo.Id)).Should().BeTrue();
        (await _storage.ObterInfoAsync(documento.ObjectKey)).Should().NotBeNull();
        (await _storage.ObterInfoAsync(modelo.ObjectKey)).Should().NotBeNull();
    }

    [Fact(DisplayName = "Confirmado permanece com o objeto, mesmo com o prazo de envio vencido")]
    public async Task Confirmado_Permanece()
    {
        Guid processoId = await _envios.NovoProcessoAsync();
        DocumentoAPublicar documento = EnviosPendentesDeTeste.Documento(processoId, Vencido);
        documento.Confirmar(Conteudo.Length, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        ModeloDeDocumento modelo = EnviosPendentesDeTeste.Modelo(processoId, Vencido);
        modelo.Confirmar(Conteudo.Length, new string('b', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        await PersistirComObjetoAsync(documento, documento.ObjectKeyConfirmado!, modelo, modelo.ObjectKeyConfirmado!);

        await _envios.ExecutarRemocaoAsync();

        (await _envios.ExisteDocumentoAsync(documento.Id)).Should().BeTrue();
        (await _envios.ExisteModeloAsync(modelo.Id)).Should().BeTrue();
        (await _storage.ObterInfoAsync(documento.ObjectKeyConfirmado!)).Should().NotBeNull();
        (await _storage.ObterInfoAsync(modelo.ObjectKeyConfirmado!)).Should().NotBeNull();
    }

    [Fact(DisplayName = "Objeto já ausente do armazenamento não impede a remoção do registro vencido")]
    public async Task ObjetoAusente_NaoImpedeARemocao()
    {
        Guid processoId = await _envios.NovoProcessoAsync();
        // O bucket existe — o que falta é só o objeto do pendente.
        await _storage.SalvarConteudoSeladoAsync($"selecao/outro/{Guid.CreateVersion7():D}.pdf", Conteudo, TiposDeMidiaDeDocumento.Pdf);
        DocumentoAPublicar documento = EnviosPendentesDeTeste.Documento(processoId, Vencido);
        await _envios.PersistirAsync(documento);

        await _envios.ExecutarRemocaoAsync();

        (await _envios.ExisteDocumentoAsync(documento.Id)).Should().BeFalse();
    }

    [Fact(DisplayName = "A remoção do documento vencido não alcança o que a confirmação concorrente reivindicou primeiro")]
    public async Task RemocaoCondicionada_DocumentoReivindicadoPelaConfirmacao_Permanece()
    {
        Guid processoId = await _envios.NovoProcessoAsync();
        DocumentoAPublicar documento = EnviosPendentesDeTeste.Documento(processoId, Vencido);
        await _envios.PersistirAsync(documento);

        bool removeu = await RemoverDuranteAConfirmacaoAsync(
            context => new DocumentoAPublicarRepository(context).TentarReivindicarConfirmacaoAsync(documento.Id),
            context => new DocumentoAPublicarRepository(context).RemoverSePendenteVencidoAsync(documento.Id, TimeProvider.System.GetUtcNow(), static _ => Task.CompletedTask));

        removeu.Should().BeFalse();
        await using UniPlusDbContext leitura = _dbFixture.CreateDbContext();
        (await leitura.DocumentosAPublicar.AsNoTracking().SingleAsync(d => d.Id == documento.Id))
            .Status.Should().Be(StatusArquivoEnviado.Confirmado);
    }

    [Fact(DisplayName = "A remoção do modelo vencido não alcança o que a confirmação concorrente reivindicou primeiro")]
    public async Task RemocaoCondicionada_ModeloReivindicadoPelaConfirmacao_Permanece()
    {
        Guid processoId = await _envios.NovoProcessoAsync();
        ModeloDeDocumento modelo = EnviosPendentesDeTeste.Modelo(processoId, Vencido);
        await _envios.PersistirAsync(modelo);

        bool removeu = await RemoverDuranteAConfirmacaoAsync(
            context => new ModeloDeDocumentoRepository(context).TentarReivindicarConfirmacaoAsync(modelo.Id),
            context => new ModeloDeDocumentoRepository(context).RemoverSePendenteVencidoAsync(modelo.Id, TimeProvider.System.GetUtcNow(), static _ => Task.CompletedTask));

        removeu.Should().BeFalse();
        await using UniPlusDbContext leitura = _dbFixture.CreateDbContext();
        (await leitura.ModelosDeDocumento.AsNoTracking().SingleAsync(m => m.Id == modelo.Id))
            .Status.Should().Be(StatusArquivoEnviado.Confirmado);
    }

    /// <summary>
    /// A confirmação reivindica o registro numa transação ainda aberta; a remoção, em outra
    /// conexão, espera o bloqueio de linha e só reavalia a condição depois do commit.
    /// </summary>
    private async Task<bool> RemoverDuranteAConfirmacaoAsync(
        Func<UniPlusDbContext, Task<bool>> reivindicar, Func<UniPlusDbContext, Task<bool>> remover)
    {
        await using UniPlusDbContext confirmacao = _dbFixture.CreateDbContext();
        await using IDbContextTransaction transacao = await confirmacao.Database.BeginTransactionAsync();
        (await reivindicar(confirmacao)).Should().BeTrue();

        await using UniPlusDbContext varredura = _dbFixture.CreateDbContext();
        Task<bool> remocao = remover(varredura);
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        remocao.IsCompleted.Should().BeFalse("a remoção espera o bloqueio de linha da confirmação em curso");

        await transacao.CommitAsync();
        return await remocao;
    }

    private async Task PersistirComObjetoAsync(
        DocumentoAPublicar documento, string chaveDoDocumento, ModeloDeDocumento modelo, string chaveDoModelo)
    {
        await _envios.PersistirAsync(documento);
        await _envios.PersistirAsync(modelo);
        await _storage.SalvarConteudoSeladoAsync(chaveDoDocumento, Conteudo, TiposDeMidiaDeDocumento.Pdf);
        await _storage.SalvarConteudoSeladoAsync(chaveDoModelo, Conteudo, modelo.ContentType);
    }
}
