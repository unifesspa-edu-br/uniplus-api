namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static CenarioDeInscricaoPublicada;
using static ProcessosDeInscricaoDeTeste;

[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class AtomicidadeDoEnvioDaInscricaoTests(InscricaoDbFixture fixture)
{
    private readonly CenarioDeInscricaoPublicada _cenario = new(fixture);

    [Fact]
    public async Task Enviar_FalhaNoComprovante_NaoGravaInscricaoNemConciliacaoDosDocumentos()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        (ProcessoSeletivo processo, InscricaoDto original) = await PrepararAsync(db, nameof(Enviar_FalhaNoComprovante_NaoGravaInscricaoNemConciliacaoDosDocumentos));
        ArquivoDaApresentacao arquivo = await GravarArquivoSemExigenciaAplicavelAsync(db, original.Id);
        IComprovanteDaInscricaoRepository comprovantes = ComprovantesQueFalham();
        Func<Task> enviar = async () => await _cenario.EnviarAsync(db, processo.Id, PedidoDeEnvio(processo, new Dictionary<string, System.Text.Json.JsonElement>()),
            original.ETag, "00000701", comprovantes, new DocumentoDaInscricaoRepository(db));
        await enviar.Should().ThrowAsync<InvalidOperationException>().WithMessage("Falha simulada na persistência do comprovante.");
        db.Entry(arquivo).State.Should().Be(EntityState.Deleted);
        db.ArquivosAApagar.Local.Should().ContainSingle(c => c.ObjectKey == arquivo.ChaveDefinitivaPrevista);
        await ConferirAusenciaDeAlteracoesAsync(original, arquivo);
    }

    [Fact]
    public async Task Enviar_DuasLeiturasDaMesmaRevisao_GravaUmaSoInscricaoEUmComprovante()
    {
        await using UniPlusDbContext primeiro = _cenario.CriarDbContext();
        (ProcessoSeletivo processo, InscricaoDto original) = await PrepararAsync(primeiro, nameof(Enviar_DuasLeiturasDaMesmaRevisao_GravaUmaSoInscricaoEUmComprovante));
        await using UniPlusDbContext segundo = _cenario.CriarDbContext();
        (await _cenario.NovoRepositorioDeInscricao(segundo).ObterPorIdAsync(original.Id))!.ETag.Should().Be(original.ETag);
        InscricaoRequest pedido = PedidoDeEnvio(processo, new Dictionary<string, System.Text.Json.JsonElement>());
        (await _cenario.EnviarAsync(primeiro, processo.Id, pedido, original.ETag, "00000702")).IsSuccess.Should().BeTrue();
        Result<ReciboDaInscricaoDto> recusada = await _cenario.EnviarAsync(segundo, processo.Id, pedido, original.ETag, "00000703");
        recusada.Error!.Code.Should().Be(InscricaoErrorCodes.ConflitoDeConcorrencia);
        segundo.ChangeTracker.Entries().Should().BeEmpty();
        (Inscricao gravada, IReadOnlyList<ComprovanteInscricao> comprovantes) = await _cenario.RelerAsync(processo.Id);
        gravada.Estado.NumeroInscricao.Should().Be("00000702");
        comprovantes.Should().ContainSingle().Which.NumeroInscricao.Should().Be("00000702");
    }

    private async Task<(ProcessoSeletivo, InscricaoDto)> PrepararAsync(UniPlusDbContext db, string nome)
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(nome);
        (_, InscricaoDto inscricao) = await _cenario.PublicarEIniciarAsync(db, processo, PersonasDeTeste.FernandaDrumond);
        return (processo, inscricao);
    }

    private static IComprovanteDaInscricaoRepository ComprovantesQueFalham()
    {
        IComprovanteDaInscricaoRepository comprovantes = Substitute.For<IComprovanteDaInscricaoRepository>();
        comprovantes.AdicionarComprovanteAsync(Arg.Any<ComprovanteInscricao>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Falha simulada na persistência do comprovante."));
        return comprovantes;
    }

    private static async Task<ArquivoDaApresentacao> GravarArquivoSemExigenciaAplicavelAsync(UniPlusDbContext db, Guid inscricaoId)
    {
        DocumentoExigido exigencia = new DocumentoExigidoBuilder().ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "LAUDO", "Laudo", "SAUDE", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComIndeferimento(new IndeferimentoDoDocumento(false, null, [])).Construir();
        ApresentacaoDeDocumento apresentacao = ApresentacaoDeDocumento.Criar(inscricaoId, exigencia, null);
        ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(apresentacao.Id, inscricaoId, exigencia,
            "application/pdf", 1024, DentroDoPeriodo, TimeSpan.FromMinutes(15)).Value!;
        arquivo.Confirmar("\"etag\"", DentroDoPeriodo).IsSuccess.Should().BeTrue();
        arquivo.RegistrarVerificado(1024, new string('a', 64), FormatoPermitido.Pdf).Should().BeTrue();
        DocumentoDaInscricaoRepository documentos = new(db);
        await documentos.AdicionarApresentacaoAsync(apresentacao);
        await documentos.AdicionarArquivoAsync(arquivo);
        await db.SalvarAlteracoesAsync(CancellationToken.None);
        return arquivo;
    }

    private async Task ConferirAusenciaDeAlteracoesAsync(InscricaoDto original, ArquivoDaApresentacao arquivo)
    {
        await using UniPlusDbContext leitura = _cenario.CriarDbContext();
        Inscricao inscricao = (await _cenario.NovoRepositorioDeInscricao(leitura).ObterPorIdAsync(original.Id))!;
        inscricao.ETag.Should().Be(original.ETag);
        inscricao.EhRascunho.Should().BeTrue();
        (await leitura.ArquivosDaApresentacao.AnyAsync(a => a.Id == arquivo.Id)).Should().BeTrue();
        (await leitura.ApresentacoesDeDocumento.AnyAsync(a => a.Id == arquivo.ApresentacaoId)).Should().BeTrue();
        (await leitura.ArquivosAApagar.AnyAsync(c => c.ObjectKey == arquivo.ChaveDefinitivaPrevista)).Should().BeFalse();
        (await _cenario.NovoRepositorioDeComprovantes(leitura).ObterEmissoesAsync([original.Id])).Should().BeEmpty();
    }
}
