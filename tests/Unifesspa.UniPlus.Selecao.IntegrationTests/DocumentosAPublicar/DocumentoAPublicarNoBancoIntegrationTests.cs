namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosAPublicar;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// O que o repositório do documento a publicar garante sobre o Postgres real, sem armazenamento: a reivindicação
/// atômica da confirmação, a transação ambiente do comando e a leitura restrita ao processo e à finalidade.
/// </summary>
public sealed class DocumentoAPublicarNoBancoIntegrationTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private const string NomeDoProcesso = "PS 2026 — SiSU";

    private readonly ProcessoSeletivoDbFixture _dbFixture;

    public DocumentoAPublicarNoBancoIntegrationTests(ProcessoSeletivoDbFixture dbFixture)
    {
        ArgumentNullException.ThrowIfNull(dbFixture);
        _dbFixture = dbFixture;
    }

    [Fact(DisplayName = "TentarReivindicarConfirmacaoAsync é atômico: só a primeira chamada ganha (Postgres real)")]
    public async Task TentarReivindicarConfirmacaoAsync_SoAPrimeiraChamadaGanha()
    {
        await using UniPlusDbContext context = _dbFixture.CreateDbContext();
        ProcessoSeletivo processo = await ProcessoSeletivoDeTeste.GravarAsync(context, NomeDoProcesso);
        DocumentoAPublicar documento = await GravarPendenteAsync(context, processo.Id);
        DocumentoAPublicarRepository repositorio = new(context);

        bool primeira = await repositorio.TentarReivindicarConfirmacaoAsync(documento.Id, CancellationToken.None);
        bool segunda = await repositorio.TentarReivindicarConfirmacaoAsync(documento.Id, CancellationToken.None);

        primeira.Should().BeTrue();
        segunda.Should().BeFalse();
    }

    [Fact(DisplayName = "A finalidade fica gravada e a listagem filtra por ela, sem misturar o anexo PDF com o edital")]
    public async Task Listar_FiltraPelaFinalidadeGravada()
    {
        await using UniPlusDbContext context = _dbFixture.CreateDbContext();
        ProcessoSeletivo processo = await ProcessoSeletivoDeTeste.GravarAsync(context, NomeDoProcesso);
        (DocumentoAPublicar edital, DocumentoAPublicar anexoPdf, DocumentoAPublicar anexoDocx) = await GravarEditalEDoisAnexosAsync(context, processo.Id);
        await using UniPlusDbContext leitura = _dbFixture.CreateDbContext();
        DocumentoAPublicarRepository repositorio = new(leitura);

        IReadOnlyList<DocumentoAPublicar> editais = await repositorio.ListarPorProcessoAsync(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital);
        IReadOnlyList<DocumentoAPublicar> anexos = await repositorio.ListarPorProcessoAsync(processo.Id, FinalidadeDoDocumento.AnexoDoEdital);

        editais.Select(static d => d.Id).Should().Equal(edital.Id);
        anexos.Select(static d => d.Id).Should().BeEquivalentTo([anexoPdf.Id, anexoDocx.Id]);
        (await repositorio.ListarPorProcessoAsync(processo.Id, null)).Should().HaveCount(3);
        (await repositorio.ListarPorProcessoAsync(processo.Id, FinalidadeDoDocumento.DocumentoDeAto)).Should().BeEmpty();
    }

    [Fact(DisplayName = "ListarDoProcessoAsync só devolve documento do processo pedido, mesmo com o id de outro processo")]
    public async Task ListarDoProcesso_NaoDevolveDocumentoDeOutroProcesso()
    {
        await using UniPlusDbContext context = _dbFixture.CreateDbContext();
        ProcessoSeletivo processoA = await ProcessoSeletivoDeTeste.GravarAsync(context, NomeDoProcesso);
        ProcessoSeletivo processoB = await ProcessoSeletivoDeTeste.GravarAsync(context, NomeDoProcesso);
        DocumentoAPublicar doA = await GravarPendenteAsync(context, processoA.Id, FinalidadeDoDocumento.AnexoDoEdital, "a.pdf");
        DocumentoAPublicar doB = await GravarPendenteAsync(context, processoB.Id, FinalidadeDoDocumento.AnexoDoEdital, "b.pdf");
        await using UniPlusDbContext leitura = _dbFixture.CreateDbContext();
        DocumentoAPublicarRepository repositorio = new(leitura);

        (await repositorio.ListarDoProcessoAsync(processoA.Id, [doB.Id])).Should().BeEmpty(
            "o id de um documento de outro processo não pode atravessar a fronteira do processo");
        (await repositorio.ListarDoProcessoAsync(processoA.Id, [doA.Id, doB.Id])).Select(static d => d.Id).Should().Equal(doA.Id);
    }

    /// <summary>
    /// Simula o mesmo cenário do handler sob Wolverine (AutoApplyTransactions): a reivindicação roda dentro da
    /// transação ambiente do comando. Se um passo posterior falhar (ex.: o storage indisponível), o rollback dessa
    /// transação desfaz a reivindicação junto — o documento volta a Pendente e a confirmação é retentável.
    /// </summary>
    [Fact(DisplayName = "TentarReivindicarConfirmacaoAsync participa da transação ambiente: reverte em rollback")]
    public async Task TentarReivindicarConfirmacaoAsync_RevertNoRollbackDaTransacaoAmbiente()
    {
        await using UniPlusDbContext context = _dbFixture.CreateDbContext();
        ProcessoSeletivo processo = await ProcessoSeletivoDeTeste.GravarAsync(context, NomeDoProcesso);
        DocumentoAPublicar documento = await GravarPendenteAsync(context, processo.Id);
        DocumentoAPublicarRepository repositorio = new(context);

        await using (IDbContextTransaction transacao = await context.Database.BeginTransactionAsync())
        {
            (await repositorio.TentarReivindicarConfirmacaoAsync(documento.Id, CancellationToken.None)).Should().BeTrue();
            await transacao.RollbackAsync();
        }

        await using UniPlusDbContext independente = _dbFixture.CreateDbContext();
        (await independente.DocumentosAPublicar.FirstAsync(d => d.Id == documento.Id)).Status.Should().Be(StatusArquivoEnviado.Pendente);
    }

    private static async Task<DocumentoAPublicar> GravarPendenteAsync(
        UniPlusDbContext context,
        Guid processoId,
        FinalidadeDoDocumento finalidade = FinalidadeDoDocumento.DocumentoDoEdital,
        string nomeArquivo = "edital.pdf",
        string tipoDeMidia = TiposDeMidiaDeDocumento.Pdf)
    {
        DocumentoAPublicar documento = EnviosPendentesDeTeste.Documento(processoId, finalidade: finalidade, nomeArquivo: nomeArquivo, tipoDeMidia: tipoDeMidia);
        context.DocumentosAPublicar.Add(documento);
        await context.SaveChangesAsync();
        return documento;
    }

    private static async Task<(DocumentoAPublicar Edital, DocumentoAPublicar AnexoPdf, DocumentoAPublicar AnexoDocx)> GravarEditalEDoisAnexosAsync(
        UniPlusDbContext context, Guid processoId) =>
        (await GravarPendenteAsync(context, processoId),
         await GravarPendenteAsync(context, processoId, FinalidadeDoDocumento.AnexoDoEdital, "quadro.pdf"),
         await GravarPendenteAsync(context, processoId, FinalidadeDoDocumento.AnexoDoEdital, "formulario.docx", TiposDeMidiaDeDocumento.Docx));
}
