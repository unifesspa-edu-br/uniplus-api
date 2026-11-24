namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ArquivosEnviados;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
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
/// A cópia gravada na confirmação e o registro pendente vencido saem juntos, contra Postgres e MinIO
/// reais: a cópia que ficou sem registro confirmado é removida com o pendente, e a do confirmado que
/// reivindicou o registro primeiro permanece. O bloqueio de linha entre a confirmação e a rotina é
/// do banco, e nenhum dublê o reproduz.
/// </summary>
public sealed class RemocaoDaCopiaGravadaDoPendenteVencidoIntegrationTests
    : IClassFixture<ProcessoSeletivoDbFixture>, IClassFixture<MinioContainerFixture>
{
    private const string TestBucket = "uniplus-copia-gravada-test";
    private static readonly TimeSpan Vencido = -(RemocaoDeArquivosPendentesVencidos.Tolerancia + TimeSpan.FromMinutes(1));
    private const string DeleteDoDocumento = "DELETE FROM selecao.documentos_a_publicar";
    private const string UpdateDoDocumento = "UPDATE selecao.documentos_a_publicar";
    private static readonly byte[] Conteudo = [.. "%PDF-1.7 envio de teste"u8];

    private readonly ProcessoSeletivoDbFixture _dbFixture;
    private readonly ArquivoArmazenadoStorageService _storage;
    private readonly EnviosPendentesNoBanco _envios;

    public RemocaoDaCopiaGravadaDoPendenteVencidoIntegrationTests(ProcessoSeletivoDbFixture dbFixture, MinioContainerFixture minio)
    {
        ArgumentNullException.ThrowIfNull(dbFixture);
        _dbFixture = dbFixture;
        _storage = new ArmazenamentoDeTesteNoMinio(minio, TestBucket).ArquivosArmazenados();
        _envios = new EnviosPendentesNoBanco(dbFixture, _storage);
    }

    [Fact(DisplayName = "A cópia gravada por confirmação desfeita sai com o pendente vencido do documento e do modelo")]
    public async Task CopiaDeConfirmacaoDesfeita_SaiComOPendenteVencido()
    {
        Guid processoId = await _envios.NovoProcessoAsync();
        DocumentoAPublicar documento = EnviosPendentesDeTeste.Documento(processoId, Vencido);
        ModeloDeDocumento modelo = EnviosPendentesDeTeste.Modelo(processoId, Vencido);
        await _envios.PersistirAsync(documento);
        await _envios.PersistirAsync(modelo);
        await GravarObjetosAsync(documento.ObjectKey, documento.ChaveDaCopiaGravada, TiposDeMidiaDeDocumento.Pdf);
        await GravarObjetosAsync(modelo.ObjectKey, modelo.ChaveDaCopiaGravada, modelo.ContentType);

        await _envios.ExecutarRemocaoAsync();

        (await _envios.ExisteDocumentoAsync(documento.Id)).Should().BeFalse();
        (await _envios.ExisteModeloAsync(modelo.Id)).Should().BeFalse();
        await ObjetosDevemTerSaidoAsync(documento.ObjectKey, documento.ChaveDaCopiaGravada, modelo.ObjectKey, modelo.ChaveDaCopiaGravada);
    }

    [Fact(DisplayName = "A confirmação concorrente que reivindicou o registro primeiro mantém a cópia gravada")]
    public async Task ConfirmacaoQueReivindicouPrimeiro_MantemACopia()
    {
        DocumentoAPublicar documento = await PendenteVencidoComCopiaAsync();

        await RodarRotinaBloqueadaPelaConfirmacaoAsync(documento.Id, transacao => transacao.CommitAsync());

        (await StatusDoDocumentoAsync(documento.Id)).Should().Be(StatusArquivoEnviado.Confirmado);
        (await _storage.ObterInfoAsync(documento.ChaveDaCopiaGravada)).Should().NotBeNull();
    }

    [Fact(DisplayName = "A mesma corrida com a confirmação desfeita remove o registro e a cópia gravada")]
    public async Task ConfirmacaoQueReivindicouPrimeiroEFoiDesfeita_RemoveRegistroECopia()
    {
        DocumentoAPublicar documento = await PendenteVencidoComCopiaAsync();

        await RodarRotinaBloqueadaPelaConfirmacaoAsync(documento.Id, transacao => transacao.RollbackAsync());

        (await _envios.ExisteDocumentoAsync(documento.Id)).Should().BeFalse();
        await ObjetosDevemTerSaidoAsync(documento.ObjectKey, documento.ChaveDaCopiaGravada);
    }

    [Fact(DisplayName = "O pendente que a rotina já removeu não é mais confirmado, e a confirmação espera o commit da rotina")]
    public async Task PendenteEmRemocao_NaoConfirmaMais()
    {
        DocumentoAPublicar documento = await PendenteVencidoComCopiaAsync();

        (bool removeu, bool confirmou) = await RemoverEConfirmarComALinhaSeguradaAsync(documento.Id);

        removeu.Should().BeTrue();
        confirmou.Should().BeFalse();
        (await _envios.ExisteDocumentoAsync(documento.Id)).Should().BeFalse();
    }

    [Fact(DisplayName = "Falha ao remover a cópia gravada mantém o registro, e a execução seguinte remove os dois")]
    public async Task FalhaAoRemoverACopia_MantemORegistroERemoveNaExecucaoSeguinte()
    {
        DocumentoAPublicar documento = await PendenteVencidoComCopiaAsync();

        await _envios.ExecutarRemocaoAsync(ArmazenamentoQueRecusaARemocaoDe(documento.ChaveDaCopiaGravada));

        (await _envios.ExisteDocumentoAsync(documento.Id)).Should().BeTrue();
        (await _storage.ObterInfoAsync(documento.ObjectKey)).Should().BeNull("a chave de envio foi removida no armazenamento real");
        (await _storage.ObterInfoAsync(documento.ChaveDaCopiaGravada)).Should().NotBeNull();

        await _envios.ExecutarRemocaoAsync();

        (await _envios.ExisteDocumentoAsync(documento.Id)).Should().BeFalse();
        await ObjetosDevemTerSaidoAsync(documento.ObjectKey, documento.ChaveDaCopiaGravada);
    }

    /// <summary>
    /// A confirmação reivindica o registro numa transação aberta; a rotina é lançada, o banco confirma
    /// que o DELETE dela espera o bloqueio de linha, e só então a confirmação é encerrada. Em qualquer
    /// caminho a transação é encerrada e a rotina termina antes de os contextos serem descartados.
    /// </summary>
    private async Task RodarRotinaBloqueadaPelaConfirmacaoAsync(Guid id, Func<IDbContextTransaction, Task> encerrarConfirmacao)
    {
        await using UniPlusDbContext confirmacao = _dbFixture.CreateDbContext();
        IDbContextTransaction transacao = await ReivindicarEmTransacaoAsync(confirmacao, id);
        Task rotina = _envios.ExecutarRemocaoAsync();
        try
        {
            await EncerrarComARotinaBloqueadaAsync(transacao, rotina, encerrarConfirmacao);
        }
        finally
        {
            await transacao.DisposeAsync();
            await Task.WhenAny(rotina);
        }
    }

    private static async Task<IDbContextTransaction> ReivindicarEmTransacaoAsync(UniPlusDbContext confirmacao, Guid id)
    {
        IDbContextTransaction transacao = await confirmacao.Database.BeginTransactionAsync();
        (await new DocumentoAPublicarRepository(confirmacao).TentarReivindicarConfirmacaoAsync(id)).Should().BeTrue();
        return transacao;
    }

    private async Task EncerrarComARotinaBloqueadaAsync(
        IDbContextTransaction transacao, Task rotina, Func<IDbContextTransaction, Task> encerrarConfirmacao)
    {
        await SessaoEsperandoBloqueio.AguardarAsync(_dbFixture, DeleteDoDocumento);
        await encerrarConfirmacao(transacao);
        await rotina;
    }

    private IArquivoArmazenadoStorage ArmazenamentoQueRecusaARemocaoDe(string chaveRecusada)
    {
        IArquivoArmazenadoStorage armazenamento = Substitute.For<IArquivoArmazenadoStorage>();
        armazenamento.RemoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(chamada => _storage.RemoverAsync(chamada.Arg<string>(), chamada.Arg<CancellationToken>()));
        armazenamento.RemoverAsync(chaveRecusada, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("armazenamento indisponível"));
        return armazenamento;
    }

    private async Task<(bool Removeu, bool Confirmou)> RemoverEConfirmarComALinhaSeguradaAsync(Guid id)
    {
        await using RemocaoDoPendenteSegurandoALinha remocao = new(_dbFixture, id);
        await remocao.AguardarLinhaSeguradaAsync();
        bool confirmou = await ReivindicarEnquantoALinhaEstaSeguradaAsync(id, remocao);
        return (await remocao.Resultado, confirmou);
    }

    private async Task<bool> ReivindicarEnquantoALinhaEstaSeguradaAsync(Guid id, RemocaoDoPendenteSegurandoALinha remocao)
    {
        await using UniPlusDbContext confirmacao = _dbFixture.CreateDbContext();
        Task<bool> reivindicacao = new DocumentoAPublicarRepository(confirmacao).TentarReivindicarConfirmacaoAsync(id);
        try
        {
            await SessaoEsperandoBloqueio.AguardarAsync(_dbFixture, UpdateDoDocumento);
        }
        finally
        {
            remocao.Liberar();
            await Task.WhenAny(reivindicacao);
        }

        return await reivindicacao;
    }

    private async Task<DocumentoAPublicar> PendenteVencidoComCopiaAsync()
    {
        DocumentoAPublicar documento = EnviosPendentesDeTeste.Documento(await _envios.NovoProcessoAsync(), Vencido);
        await _envios.PersistirAsync(documento);
        await GravarObjetosAsync(documento.ObjectKey, documento.ChaveDaCopiaGravada, TiposDeMidiaDeDocumento.Pdf);
        return documento;
    }

    private async Task GravarObjetosAsync(string chaveDeEnvio, string chaveDaCopia, string tipoDeMidia)
    {
        await _storage.SalvarConteudoSeladoAsync(chaveDeEnvio, Conteudo, tipoDeMidia);
        await _storage.SalvarConteudoSeladoAsync(chaveDaCopia, Conteudo, tipoDeMidia);
    }

    private async Task ObjetosDevemTerSaidoAsync(params string[] chaves)
    {
        foreach (string chave in chaves)
        {
            (await _storage.ObterInfoAsync(chave)).Should().BeNull($"o objeto {chave} sai com o pendente vencido");
        }
    }

    private async Task<StatusArquivoEnviado> StatusDoDocumentoAsync(Guid id)
    {
        await using UniPlusDbContext leitura = _dbFixture.CreateDbContext();
        return (await leitura.DocumentosAPublicar.AsNoTracking().SingleAsync(d => d.Id == id)).Situacao;
    }
}
