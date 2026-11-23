namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento;

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
using Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosAPublicar;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ConfirmacaoDoModeloDoRascunhoConcorrenciaIntegrationTests
    : IClassFixture<ProcessoSeletivoDbFixture>, IClassFixture<MinioContainerFixture>
{
    private const string TestBucket = "uniplus-modelos-rascunho-test";
    private static readonly byte[] Conteudo = ArquivosDeModeloDeTeste.Odt();

    private readonly ProcessoSeletivoDbFixture _db;
    private readonly ArquivoArmazenadoStorageService _storage;

    public ConfirmacaoDoModeloDoRascunhoConcorrenciaIntegrationTests(ProcessoSeletivoDbFixture db, MinioContainerFixture minio)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
        _storage = new ArmazenamentoDeTesteNoMinio(minio, TestBucket).ArquivosArmazenados();
    }

    [Fact(DisplayName = "Duas confirmações do mesmo rascunho não se serializam: uma termina enquanto a outra ainda retém o bloqueio compartilhado")]
    public async Task DuasConfirmacoes_NaoSeSerializam()
    {
        await using EnvioDoModeloDeDocumento envio = await EnvioDoModeloDeDocumento.NovoDeRascunhoAsync(_db, _storage);
        Guid primeiro = (await envio.IniciarEEnviarAsync(Conteudo, "Declaração")).ModeloDeDocumentoId;
        Guid segundo = (await envio.IniciarEEnviarAsync(Conteudo, "Declaração")).ModeloDeDocumentoId;
        ArmazenamentoQuePausaAntesDeGravarACopia retido = new(_storage);
        Task<Result<ModeloDeDocumentoDto>> confirmacaoRetida = envio.ConfirmarNaTransacaoAsync(primeiro, retido);
        await retido.ChegouAGravacaoDaCopia;

        await using EnvioDoModeloDeDocumento outra = envio.EmNovoContexto(_db);
        Result<ModeloDeDocumentoDto> concluida = await outra.ConfirmarNaTransacaoAsync(segundo);

        concluida.IsSuccess.Should().BeTrue();
        confirmacaoRetida.IsCompleted.Should().BeFalse("a primeira confirmação segue retida na gravação da cópia do arquivo");
        await LiberarEConfirmarAsync(retido, confirmacaoRetida);
    }

    [Fact(DisplayName = "A exclusão espera a confirmação em andamento e leva junto o modelo confirmado, com as duas chaves do objeto")]
    public async Task ExclusaoEsperaAConfirmacao_ModeloConfirmadoERemovidoJunto()
    {
        await using EnvioDoModeloDeDocumento envio = await EnvioDoModeloDeDocumento.NovoDeRascunhoAsync(_db, _storage);
        Guid modeloId = (await envio.IniciarEEnviarAsync(Conteudo, "Declaração")).ModeloDeDocumentoId;
        ArmazenamentoQuePausaAntesDeGravarACopia retido = new(_storage);
        Task<Result<ModeloDeDocumentoDto>> confirmacao = envio.ConfirmarNaTransacaoAsync(modeloId, retido);
        await retido.ChegouAGravacaoDaCopia;
        await using UniPlusDbContext excluidor = _db.CreateDbContext();
        Task<(Result Resposta, IEnumerable<object> Mensagens)> exclusao = new ExclusaoDoRascunhoDeTeste(excluidor).ExcluirNaTransacaoAsync(envio.ProcessoId);
        await SessaoEsperandoBloqueio.AguardarAsync(_db, "FOR UPDATE");
        await LiberarEConfirmarAsync(retido, confirmacao);

        (Result resposta, IEnumerable<object> mensagens) = await exclusao;
        resposta.IsSuccess.Should().BeTrue();
        ChavesRemovidas(mensagens).Should().HaveCount(2);
        (await ModelosDoDonoAsync(envio.ProcessoId)).Should().BeEmpty();
    }

    private static IReadOnlyList<string> ChavesRemovidas(IEnumerable<object> mensagens) =>
        mensagens.Should().ContainSingle().Which.Should().BeOfType<RemoverArquivosDoRascunhoExcluido>().Which.Chaves;

    [Fact(DisplayName = "A confirmação que espera a exclusão do rascunho é recusada e não sobra modelo de dono inexistente")]
    public async Task ExclusaoEmAndamento_ConfirmacaoRecusada()
    {
        await using EnvioDoModeloDeDocumento envio = await EnvioDoModeloDeDocumento.NovoDeRascunhoAsync(_db, _storage);
        Guid modeloId = (await envio.IniciarEEnviarAsync(Conteudo, "Declaração")).ModeloDeDocumentoId;
        await using TransacaoQueRetemBloqueios exclusao = await TransacaoQueRetemBloqueios.IniciarAsync(_db);
        RascunhoDeProcessoSeletivo rascunho = (await exclusao.Repositorio.ObterParaMutacaoAsync(envio.ProcessoId))!;
        await using EnvioDoModeloDeDocumento confirmador = envio.EmNovoContexto(_db);
        Task<Result<ModeloDeDocumentoDto>> confirmacao = confirmador.ConfirmarNaTransacaoAsync(modeloId);
        await SessaoEsperandoBloqueio.AguardarAsync(_db, "FOR SHARE");

        await ExcluirRascunhoEModelosAsync(exclusao, rascunho);

        (await confirmacao).Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        (await ModelosDoDonoAsync(envio.ProcessoId)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A confirmação bloqueia o rascunho antes de tocar o modelo: rascunho, depois modelo")]
    public async Task OrdemDosBloqueios_RascunhoAntesDoModelo()
    {
        await using EnvioDoModeloDeDocumento envio = await EnvioDoModeloDeDocumento.NovoDeRascunhoAsync(_db, _storage);
        Guid modeloId = (await envio.IniciarEEnviarAsync(Conteudo, "Declaração")).ModeloDeDocumentoId;
        await using TransacaoQueRetemBloqueios retida = await TransacaoQueRetemBloqueios.IniciarAsync(_db);
        await retida.Repositorio.ObterParaMutacaoAsync(envio.ProcessoId);
        await using EnvioDoModeloDeDocumento confirmador = envio.EmNovoContexto(_db);
        Task<Result<ModeloDeDocumentoDto>> confirmacao = confirmador.ConfirmarNaTransacaoAsync(modeloId);
        await SessaoEsperandoBloqueio.AguardarAsync(_db, "FOR SHARE");

        await ModeloDeveEstarPendenteESemBloqueioAsync(modeloId);

        await retida.ConfirmarAsync();
        (await confirmacao).IsSuccess.Should().BeTrue();
    }

    private static async Task LiberarEConfirmarAsync(ArmazenamentoQuePausaAntesDeGravarACopia retido, Task<Result<ModeloDeDocumentoDto>> confirmacao)
    {
        retido.Liberar();
        (await confirmacao).IsSuccess.Should().BeTrue();
    }

    private static async Task ExcluirRascunhoEModelosAsync(TransacaoQueRetemBloqueios exclusao, RascunhoDeProcessoSeletivo rascunho)
    {
        await new ModeloDeDocumentoRepository(exclusao.Contexto).RemoverDoProcessoAsync(rascunho.Id);
        exclusao.Repositorio.Remover(rascunho);
        await exclusao.Contexto.SaveChangesAsync();
        await exclusao.ConfirmarAsync();
    }

    private async Task<IReadOnlyList<ModeloDeDocumento>> ModelosDoDonoAsync(Guid donoId)
    {
        await using UniPlusDbContext leitura = _db.CreateDbContext();
        return await leitura.ModelosDeDocumento.AsNoTracking().Where(d => d.ProcessoSeletivoId == donoId).ToListAsync();
    }

    private async Task ModeloDeveEstarPendenteESemBloqueioAsync(Guid modeloId)
    {
        await using NpgsqlConnection conexao = new(_db.ConnectionString);
        await conexao.OpenAsync();
        await using NpgsqlTransaction transacao = await conexao.BeginTransactionAsync();
        await using NpgsqlCommand comando = new(
            "SELECT situacao FROM selecao.modelos_de_documento WHERE id = @id FOR UPDATE NOWAIT", conexao, transacao);
        comando.Parameters.AddWithValue("id", modeloId);

        ((string)(await comando.ExecuteScalarAsync())!).Should().Be(StatusArquivoEnviado.Pendente.Codigo());
    }
}
