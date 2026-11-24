namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosAPublicar;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// A confirmação do documento a publicar contra a exclusão do rascunho dono, no Postgres e no MinIO reais. A confirmação
/// bloqueia o rascunho de modo compartilhado e a exclusão o bloqueia de modo exclusivo, então as duas se ordenam e o resultado
/// é um só; duas confirmações convivem; o bloqueio do rascunho vem antes de tocar o documento.
/// </summary>
public sealed class ConfirmacaoDoDocumentoDoRascunhoConcorrenciaIntegrationTests
    : IClassFixture<ProcessoSeletivoDbFixture>, IClassFixture<MinioContainerFixture>
{
    private const string TestBucket = "uniplus-documentos-rascunho-test";
    private static readonly byte[] ConteudoPdf = [.. "%PDF-1.7 conteúdo do documento do rascunho"u8];

    private readonly ProcessoSeletivoDbFixture _db;
    private readonly ArquivoArmazenadoStorageService _storage;

    public ConfirmacaoDoDocumentoDoRascunhoConcorrenciaIntegrationTests(ProcessoSeletivoDbFixture db, MinioContainerFixture minio)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
        _storage = new ArmazenamentoDeTesteNoMinio(minio, TestBucket).ArquivosArmazenados();
    }

    [Fact(DisplayName = "Duas confirmações do mesmo rascunho não se serializam: uma termina enquanto a outra ainda retém o bloqueio compartilhado")]
    public async Task DuasConfirmacoes_NaoSeSerializam()
    {
        await using EnvioDoDocumentoAPublicar envio = await EnvioDoDocumentoAPublicar.NovoDeRascunhoAsync(_db, _storage);
        Guid primeiro = (await envio.IniciarEEnviarAsync(ConteudoPdf)).DocumentoAPublicarId;
        Guid segundo = (await envio.IniciarEEnviarAsync(ConteudoPdf)).DocumentoAPublicarId;
        ArmazenamentoQuePausaAntesDeGravarACopia retido = new(_storage);
        Task<Result<DocumentoAPublicarDto>> confirmacaoRetida = envio.ConfirmarNaTransacaoAsync(primeiro, retido);
        await retido.ChegouAGravacaoDaCopia;

        await using EnvioDoDocumentoAPublicar outra = envio.EmNovoContexto(_db);
        Result<DocumentoAPublicarDto> concluida = await outra.ConfirmarNaTransacaoAsync(segundo);

        concluida.IsSuccess.Should().BeTrue();
        confirmacaoRetida.IsCompleted.Should().BeFalse("a primeira confirmação segue retida na gravação da cópia do arquivo");
        await LiberarEConfirmarAsync(retido, confirmacaoRetida);
    }

    [Fact(DisplayName = "A exclusão espera a confirmação em andamento e leva junto o documento confirmado, com as duas chaves do objeto")]
    public async Task ExclusaoEsperaAConfirmacao_DocumentoConfirmadoERemovidoJunto()
    {
        await using EnvioDoDocumentoAPublicar envio = await EnvioDoDocumentoAPublicar.NovoDeRascunhoAsync(_db, _storage);
        Guid documentoId = (await envio.IniciarEEnviarAsync(ConteudoPdf)).DocumentoAPublicarId;
        ArmazenamentoQuePausaAntesDeGravarACopia retido = new(_storage);
        Task<Result<DocumentoAPublicarDto>> confirmacao = envio.ConfirmarNaTransacaoAsync(documentoId, retido);
        await retido.ChegouAGravacaoDaCopia;
        await using UniPlusDbContext excluidor = _db.CreateDbContext();
        Task<(Result Resposta, IEnumerable<object> Mensagens)> exclusao = new ExclusaoDoRascunhoDeTeste(excluidor).ExcluirNaTransacaoAsync(envio.ProcessoId);
        await SessaoEsperandoBloqueio.AguardarAsync(_db, "FOR UPDATE");
        await LiberarEConfirmarAsync(retido, confirmacao);

        (Result resposta, IEnumerable<object> mensagens) = await exclusao;
        resposta.IsSuccess.Should().BeTrue();
        ChavesRemovidas(mensagens).Should().HaveCount(2);
        (await DocumentosDoDonoAsync(envio.ProcessoId)).Should().BeEmpty();
    }

    private static IReadOnlyList<string> ChavesRemovidas(IEnumerable<object> mensagens) =>
        mensagens.Should().ContainSingle().Which.Should().BeOfType<RemoverArquivosDoRascunhoExcluido>().Which.Chaves;

    [Fact(DisplayName = "A confirmação que espera a exclusão do rascunho é recusada e não sobra documento de dono inexistente")]
    public async Task ExclusaoEmAndamento_ConfirmacaoRecusada()
    {
        await using EnvioDoDocumentoAPublicar envio = await EnvioDoDocumentoAPublicar.NovoDeRascunhoAsync(_db, _storage);
        Guid documentoId = (await envio.IniciarEEnviarAsync(ConteudoPdf)).DocumentoAPublicarId;
        await using TransacaoQueRetemBloqueios exclusao = await TransacaoQueRetemBloqueios.IniciarAsync(_db);
        RascunhoDeProcessoSeletivo rascunho = (await exclusao.Repositorio.ObterParaMutacaoAsync(envio.ProcessoId))!;
        await using EnvioDoDocumentoAPublicar confirmador = envio.EmNovoContexto(_db);
        Task<Result<DocumentoAPublicarDto>> confirmacao = confirmador.ConfirmarNaTransacaoAsync(documentoId);
        await SessaoEsperandoBloqueio.AguardarAsync(_db, "FOR SHARE");

        await ExcluirRascunhoEDocumentosAsync(exclusao, rascunho);

        (await confirmacao).Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        (await DocumentosDoDonoAsync(envio.ProcessoId)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A confirmação bloqueia o rascunho antes de tocar o documento: rascunho, depois documento")]
    public async Task OrdemDosBloqueios_RascunhoAntesDoDocumento()
    {
        await using EnvioDoDocumentoAPublicar envio = await EnvioDoDocumentoAPublicar.NovoDeRascunhoAsync(_db, _storage);
        Guid documentoId = (await envio.IniciarEEnviarAsync(ConteudoPdf)).DocumentoAPublicarId;
        await using TransacaoQueRetemBloqueios retida = await TransacaoQueRetemBloqueios.IniciarAsync(_db);
        await retida.Repositorio.ObterParaMutacaoAsync(envio.ProcessoId);
        await using EnvioDoDocumentoAPublicar confirmador = envio.EmNovoContexto(_db);
        Task<Result<DocumentoAPublicarDto>> confirmacao = confirmador.ConfirmarNaTransacaoAsync(documentoId);
        await SessaoEsperandoBloqueio.AguardarAsync(_db, "FOR SHARE");

        await DocumentoDeveEstarPendenteESemBloqueioAsync(documentoId);

        await retida.ConfirmarAsync();
        (await confirmacao).IsSuccess.Should().BeTrue();
    }

    private static async Task LiberarEConfirmarAsync(ArmazenamentoQuePausaAntesDeGravarACopia retido, Task<Result<DocumentoAPublicarDto>> confirmacao)
    {
        retido.Liberar();
        (await confirmacao).IsSuccess.Should().BeTrue();
    }

    private static async Task ExcluirRascunhoEDocumentosAsync(TransacaoQueRetemBloqueios exclusao, RascunhoDeProcessoSeletivo rascunho)
    {
        await new DocumentoAPublicarRepository(exclusao.Contexto).RemoverDoProcessoAsync(rascunho.Id);
        exclusao.Repositorio.Remover(rascunho);
        await exclusao.Contexto.SaveChangesAsync();
        await exclusao.ConfirmarAsync();
    }

    private async Task<IReadOnlyList<DocumentoAPublicar>> DocumentosDoDonoAsync(Guid donoId)
    {
        await using UniPlusDbContext leitura = _db.CreateDbContext();
        return await leitura.DocumentosAPublicar.AsNoTracking().Where(d => d.ProcessoSeletivoId == donoId).ToListAsync();
    }

    private async Task DocumentoDeveEstarPendenteESemBloqueioAsync(Guid documentoId)
    {
        await using NpgsqlConnection conexao = new(_db.ConnectionString);
        await conexao.OpenAsync();
        await using NpgsqlTransaction transacao = await conexao.BeginTransactionAsync();
        await using NpgsqlCommand comando = new(
            "SELECT situacao FROM selecao.documentos_a_publicar WHERE id = @id FOR UPDATE NOWAIT", conexao, transacao);
        comando.Parameters.AddWithValue("id", documentoId);

        ((string)(await comando.ExecuteScalarAsync())!).Should().Be(StatusArquivoEnviado.Pendente.Codigo());
    }
}
