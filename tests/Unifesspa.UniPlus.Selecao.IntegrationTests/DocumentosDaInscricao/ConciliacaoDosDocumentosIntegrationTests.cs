namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosDaInscricao;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Integração real (Postgres) do que o envio da inscrição faz com os documentos: só a apresentação com
/// arquivo confirmado conta, e a que perde todos os arquivos sai junto com o último deles.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ConciliacaoDosDocumentosIntegrationTests : IClassFixture<InscricaoDbFixture>
{
    private readonly InscricaoDbFixture _db;

    public ConciliacaoDosDocumentosIntegrationTests(InscricaoDbFixture db) => _db = db;

    [Fact(DisplayName = "Só a apresentação com ao menos um arquivo verificado conta; o arquivo em verificação não atende a exigência")]
    public async Task ApresentacoesVerificadas_IgnoraEmVerificacaoEPendente()
    {
        (UniPlusDbContext context, Guid inscricaoId, DocumentoExigido laudo) = await InscricaoComLaudoAsync();
        DocumentoDaInscricaoRepository repositorio = new(context);
        ApresentacaoDeDocumento confirmada = await ApresentacaoComArquivosAsync(repositorio, inscricaoId, laudo, confirmados: 1);
        await ApresentacaoComArquivosAsync(repositorio, inscricaoId, laudo, confirmados: 0);
        await ApresentacaoComArquivosAsync(repositorio, inscricaoId, laudo, confirmados: 1, verificar: false);
        await context.SaveChangesAsync();

        ArquivosDaInscricao arquivos = await new DocumentosEnviadosDaInscricao(repositorio).CarregarAsync(inscricaoId, CancellationToken.None);

        arquivos.ApresentacoesVerificadas().Should().ContainSingle()
            .Which.Value.Should().ContainSingle().Which.Id.Should().Be(confirmada.Id);
    }

    [Fact(DisplayName = "Ao remover todos os arquivos de uma apresentação no mesmo envio, ela sai junto; o que constou de envio fica")]
    public async Task Conciliacao_RemoveApresentacaoSemArquivo_EPreservaOQueConstou()
    {
        (UniPlusDbContext context, Guid inscricaoId, DocumentoExigido laudo) = await InscricaoComLaudoAsync();
        DocumentoDaInscricaoRepository repositorio = new(context);
        ApresentacaoDeDocumento frenteEVerso = await ApresentacaoComArquivosAsync(repositorio, inscricaoId, laudo, confirmados: 2);
        ApresentacaoDeDocumento jaEnviada = await ApresentacaoComArquivosAsync(repositorio, inscricaoId, laudo, confirmados: 1);
        await context.SaveChangesAsync();
        await MarcarComoJaEnviadaAsync(repositorio, inscricaoId, jaEnviada.Id);
        await context.SaveChangesAsync();

        await ConciliarSemExigenciasAsync(context, repositorio, inscricaoId);

        await using UniPlusDbContext leitura = _db.CreateDbContext();
        (await leitura.ApresentacoesDeDocumento.Where(a => a.InscricaoId == inscricaoId).Select(static a => a.Id).ToListAsync())
            .Should().BeEquivalentTo([jaEnviada.Id], "a apresentação cujos arquivos saíram vai junto, e a que já constou de um envio fica");
    }

    private static async Task ConciliarSemExigenciasAsync(UniPlusDbContext context, DocumentoDaInscricaoRepository repositorio, Guid inscricaoId)
    {
        DocumentosEnviadosDaInscricao documentos = new(repositorio);
        ArquivosDaInscricao arquivos = await documentos.CarregarAsync(inscricaoId, CancellationToken.None);
        await documentos.ConciliarNoEnvioAsync(arquivos, FormularioDeTeste.AvaliacaoSemOcultos, [], DateTimeOffset.UtcNow, CancellationToken.None);
        await context.SaveChangesAsync();
    }

    private static async Task MarcarComoJaEnviadaAsync(DocumentoDaInscricaoRepository repositorio, Guid inscricaoId, Guid apresentacaoId)
    {
        foreach (ArquivoDaApresentacao arquivo in await repositorio.ListarArquivosDaInscricaoAsync(inscricaoId))
        {
            if (arquivo.ApresentacaoId == apresentacaoId)
            {
                ArquivoDaApresentacao rastreado = (await repositorio.ObterArquivoAsync(arquivo.Id))!;
                rastreado.MarcarComoConstanteDeEnvio();
            }
        }

    }

    private async Task<(UniPlusDbContext Context, Guid InscricaoId, DocumentoExigido Laudo)> InscricaoComLaudoAsync()
    {
        UniPlusDbContext context = _db.CreateDbContext();
        InscricaoRepository inscricoes = RepositoriosDaInscricaoDeTeste.Inscricoes(context, _db);
        Inscricao inscricao = Inscricao.Iniciar(Guid.CreateVersion7(), PersonasDeTeste.FernandaDrumond, "conta-1", "https://issuer", Guid.CreateVersion7()).Value!;
        await inscricoes.AdicionarAsync(inscricao);
        await context.SaveChangesAsync();

        DocumentoExigido laudo = DocumentoExigido.Criar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "LAUDO", "Laudo médico", "SAUDE", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: null,
            [], [], null, FormatosPermitidos.Criar(false, [("PDF", null)]).Value!, null).Value!;
        return (context, inscricao.Id, laudo);
    }

    private static async Task<ApresentacaoDeDocumento> ApresentacaoComArquivosAsync(
        DocumentoDaInscricaoRepository repositorio, Guid inscricaoId, DocumentoExigido laudo, int confirmados, bool verificar = true)
    {
        ApresentacaoDeDocumento apresentacao = ApresentacaoDeDocumento.Criar(inscricaoId, laudo, null);
        await repositorio.AdicionarApresentacaoAsync(apresentacao);
        for (int i = 0; i < Math.Max(confirmados, 1); i++)
        {
            ArquivoDaApresentacao arquivo = CriarArquivo(apresentacao, inscricaoId, laudo, i < confirmados, verificar);
            await repositorio.AdicionarArquivoAsync(arquivo);
        }

        return apresentacao;
    }
    private static ArquivoDaApresentacao CriarArquivo(
        ApresentacaoDeDocumento apresentacao, Guid inscricaoId, DocumentoExigido laudo, bool confirmar, bool verificar)
    {
        ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
                apresentacao.Id, inscricaoId, laudo, "application/pdf", 1024, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        if (confirmar)
        {
            arquivo.Confirmar("\"etag\"", TimeProvider.System);
            if (verificar)
            {
                arquivo.RegistrarVerificado(1024, new string('a', 64), FormatoPermitido.Pdf);
            }
        }

        return arquivo;
    }
}
