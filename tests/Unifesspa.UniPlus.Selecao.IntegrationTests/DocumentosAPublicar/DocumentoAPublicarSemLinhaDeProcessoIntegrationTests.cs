namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosAPublicar;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

using Npgsql;


using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// O documento a publicar guarda a identidade do dono, que antes da primeira publicação é a do rascunho: não há chave
/// estrangeira para a linha do processo. O que o banco e o repositório garantem sem ela: o documento de dono sem linha
/// grava, o pendente que ficou sem dono sai ao vencer, e a exclusão leva os documentos do dono e só eles.
/// </summary>
public sealed class DocumentoAPublicarSemLinhaDeProcessoIntegrationTests
    : IClassFixture<ProcessoSeletivoDbFixture>, IClassFixture<MinioContainerFixture>
{
    private const string TestBucket = "uniplus-documentos-sem-processo-test";
    private static readonly TimeSpan Vencido = -(RemocaoDeArquivosPendentesVencidos.Tolerancia + TimeSpan.FromMinutes(1));
    private static readonly byte[] Conteudo = [.. "%PDF-1.7 envio de teste"u8];

    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly ProcessoSeletivoDbFixture _db;
    private readonly ArquivoArmazenadoStorageService _storage;

    public DocumentoAPublicarSemLinhaDeProcessoIntegrationTests(ProcessoSeletivoDbFixture db, MinioContainerFixture minio)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
        _storage = new ArmazenamentoDeTesteNoMinio(minio, TestBucket).ArquivosArmazenados();
    }

    [Fact(DisplayName = "O modelo não tem chave estrangeira do documento a publicar para o processo")]
    public async Task Modelo_SemChaveEstrangeira()
    {
        await using UniPlusDbContext contexto = _db.CreateDbContext();

        contexto.Model.FindEntityType(typeof(DocumentoAPublicar))!.GetForeignKeys().Should().BeEmpty();
    }

    [Fact(DisplayName = "O banco não tem chave estrangeira na tabela do documento a publicar")]
    public async Task Banco_SemChaveEstrangeira()
    {
        await using NpgsqlConnection conexao = new(_db.ConnectionString);
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = new(
            "SELECT count(*) FROM pg_constraint WHERE conrelid = 'selecao.documentos_a_publicar'::regclass AND contype = 'f'", conexao);

        ((long)(await comando.ExecuteScalarAsync())!).Should().Be(0);
    }

    [Fact(DisplayName = "O documento de um dono sem linha de processo grava e o índice por dono o encontra")]
    public async Task DocumentoDeDonoSemLinha_Grava()
    {
        DocumentoAPublicar documento = EnviosPendentesDeTeste.Documento(Guid.CreateVersion7());
        await PersistirAsync(documento);

        await using UniPlusDbContext leitura = _db.CreateDbContext();

        (await new DocumentoAPublicarRepository(leitura).ListarPorProcessoAsync(documento.ProcessoSeletivoId, null)).Should().ContainSingle()
            .Which.Id.Should().Be(documento.Id);
    }

    [Fact(DisplayName = "O pendente que ficou sem dono, criado em corrida com a exclusão do rascunho, é removido ao vencer com o objeto")]
    public async Task PendenteSemDono_RemovidoAoVencer()
    {
        DocumentoAPublicar orfao = EnviosPendentesDeTeste.Documento(Guid.CreateVersion7(), Vencido, relogio: _relogio);
        await PersistirAsync(orfao);
        await GravarObjetosAsync(orfao);

        await RemocaoDePendentesDeTeste.ExecutarAsync(_db, _storage, _relogio);

        (await DocumentosDoDonoAsync(orfao.ProcessoSeletivoId)).Should().BeEmpty();
        (await _storage.ObterInfoAsync(orfao.ObjectKey)).Should().BeNull();
        (await _storage.ObterInfoAsync(orfao.ChaveDaCopiaGravada)).Should().BeNull();
    }

    [Fact(DisplayName = "Remover os documentos do dono grava a remoção, devolve as chaves e preserva os de outro dono")]
    public async Task RemoverDoProcesso_LevaSoOsDoDono()
    {
        DocumentoAPublicar doDono = EnviosPendentesDeTeste.Documento(Guid.CreateVersion7());
        DocumentoAPublicar deOutro = EnviosPendentesDeTeste.Documento(Guid.CreateVersion7());
        await PersistirAsync(doDono, deOutro);

        IReadOnlyList<string> chaves = await RemoverDoDonoAsync(doDono.ProcessoSeletivoId);

        chaves.Should().BeEquivalentTo([doDono.ObjectKey, doDono.ChaveDaCopiaGravada]);
        (await DocumentosDoDonoAsync(doDono.ProcessoSeletivoId)).Should().BeEmpty();
        (await DocumentosDoDonoAsync(deOutro.ProcessoSeletivoId)).Should().ContainSingle();
    }

    [Fact(DisplayName = "O documento devolve a chave de envio e a da cópia gravada na confirmação, pendente ou confirmado")]
    public async Task ChavesDeObjeto_PendenteEConfirmadoTemDuas()
    {
        Guid dono = Guid.CreateVersion7();
        DocumentoAPublicar pendente = EnviosPendentesDeTeste.Documento(dono);
        DocumentoAPublicar confirmado = EnviosPendentesDeTeste.Documento(dono);
        confirmado.Confirmar(Conteudo.Length, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        await PersistirAsync(pendente, confirmado);

        await using UniPlusDbContext leitura = _db.CreateDbContext();

        (await new DocumentoAPublicarRepository(leitura).RemoverDoProcessoAsync(dono)).Should().BeEquivalentTo(
            [pendente.ObjectKey, pendente.ChaveDaCopiaGravada, confirmado.ObjectKey, confirmado.ObjectKeyConfirmado!]);
    }

    private async Task GravarObjetosAsync(DocumentoAPublicar documento)
    {
        foreach (string chave in new[] { documento.ObjectKey, documento.ChaveDaCopiaGravada })
        {
            await _storage.SalvarConteudoSeladoAsync(chave, Conteudo, TiposDeMidiaDeDocumento.Pdf);
            (await _storage.ObterInfoAsync(chave)).Should().NotBeNull();
        }
    }

    private async Task<IReadOnlyList<string>> RemoverDoDonoAsync(Guid dono)
    {
        await using UniPlusDbContext contexto = _db.CreateDbContext();
        IReadOnlyList<string> chaves = await new DocumentoAPublicarRepository(contexto).RemoverDoProcessoAsync(dono);
        await contexto.SaveChangesAsync();
        return chaves;
    }

    private async Task PersistirAsync(params DocumentoAPublicar[] documentos)
    {
        await using UniPlusDbContext contexto = _db.CreateDbContext();
        contexto.DocumentosAPublicar.AddRange(documentos);
        await contexto.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<DocumentoAPublicar>> DocumentosDoDonoAsync(Guid dono)
    {
        await using UniPlusDbContext leitura = _db.CreateDbContext();
        return await leitura.DocumentosAPublicar.AsNoTracking().Where(d => d.ProcessoSeletivoId == dono).ToListAsync();
    }

}
