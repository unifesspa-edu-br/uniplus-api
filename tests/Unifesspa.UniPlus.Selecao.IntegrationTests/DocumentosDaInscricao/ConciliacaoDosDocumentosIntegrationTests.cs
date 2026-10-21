namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosDaInscricao;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

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

    [Fact(DisplayName = "Só a apresentação com ao menos um arquivo confirmado conta para a exigência")]
    public async Task ApresentacoesConfirmadas_IgnoraAQueSoTemPendente()
    {
        (SelecaoDbContext context, Guid inscricaoId, DocumentoExigido laudo) = await InscricaoComLaudoAsync();
        DocumentoDaInscricaoRepository repositorio = new(context);
        ApresentacaoDeDocumento confirmada = await ApresentacaoComArquivosAsync(repositorio, inscricaoId, laudo, confirmados: 1);
        await ApresentacaoComArquivosAsync(repositorio, inscricaoId, laudo, confirmados: 0);
        await context.SaveChangesAsync();

        IReadOnlyList<ApresentacaoDeDocumento> contam = await repositorio.ListarApresentacoesConfirmadasAsync(inscricaoId);

        contam.Should().ContainSingle().Which.Id.Should().Be(confirmada.Id);
    }

    [Fact(DisplayName = "Ao remover todos os arquivos de uma apresentação no mesmo envio, ela sai junto; o que constou de envio fica")]
    public async Task Conciliacao_RemoveApresentacaoSemArquivo_EPreservaOQueConstou()
    {
        (SelecaoDbContext context, Guid inscricaoId, DocumentoExigido laudo) = await InscricaoComLaudoAsync();
        DocumentoDaInscricaoRepository repositorio = new(context);
        ApresentacaoDeDocumento frenteEVerso = await ApresentacaoComArquivosAsync(repositorio, inscricaoId, laudo, confirmados: 2);
        ApresentacaoDeDocumento jaEnviada = await ApresentacaoComArquivosAsync(repositorio, inscricaoId, laudo, confirmados: 1);
        await context.SaveChangesAsync();
        foreach (ArquivoDaApresentacao arquivo in await repositorio.ListarArquivosDaInscricaoAsync(inscricaoId))
        {
            if (arquivo.ApresentacaoId == jaEnviada.Id)
            {
                ArquivoDaApresentacao rastreado = (await repositorio.ObterArquivoAsync(arquivo.Id))!;
                rastreado.MarcarComoConstanteDeEnvio();
            }
        }

        await context.SaveChangesAsync();

        // Nenhuma exigência se aplica mais na versão fixada.
        await DocumentosEnviadosDaInscricao.ConciliarNoEnvioAsync(
            repositorio, inscricaoId, FormularioDeTeste.AvaliacaoSemOcultos, DateTimeOffset.UtcNow, CancellationToken.None);
        await context.SaveChangesAsync();

        await using SelecaoDbContext leitura = _db.CreateDbContext();
        (await leitura.ApresentacoesDeDocumento.Where(a => a.InscricaoId == inscricaoId).Select(static a => a.Id).ToListAsync())
            .Should().BeEquivalentTo([jaEnviada.Id], "a apresentação cujos arquivos saíram vai junto, e a que já constou de um envio fica");
    }

    private async Task<(SelecaoDbContext Context, Guid InscricaoId, DocumentoExigido Laudo)> InscricaoComLaudoAsync()
    {
        SelecaoDbContext context = _db.CreateDbContext();
        InscricaoRepository inscricoes = new(context, _db.Encryption, _db.BlindIndex, TimeProvider.System);
        Inscricao inscricao = Inscricao.Iniciar(Guid.CreateVersion7(), PersonasDeTeste.FernandaDrumond, "sub-1", "https://issuer", Guid.CreateVersion7()).Value!;
        await inscricoes.AdicionarAsync(inscricao);
        await context.SaveChangesAsync();

        DocumentoExigido laudo = DocumentoExigido.Criar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "LAUDO", "Laudo médico", "SAUDE",
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: null,
            [], [], null, FormatosPermitidos.Criar(false, [("PDF", null)]).Value!, null).Value!;
        return (context, inscricao.Id, laudo);
    }

    private static async Task<ApresentacaoDeDocumento> ApresentacaoComArquivosAsync(
        DocumentoDaInscricaoRepository repositorio, Guid inscricaoId, DocumentoExigido laudo, int confirmados)
    {
        ApresentacaoDeDocumento apresentacao = ApresentacaoDeDocumento.Criar(inscricaoId, laudo.Id, null);
        await repositorio.AdicionarApresentacaoAsync(apresentacao);
        for (int i = 0; i < Math.Max(confirmados, 1); i++)
        {
            ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
                apresentacao.Id, inscricaoId, laudo, "application/pdf", 1024, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
            if (i < confirmados)
            {
                arquivo.Confirmar(1024, new string('a', 64), TimeProvider.System);
            }

            await repositorio.AdicionarArquivoAsync(arquivo);
        }

        return apresentacao;
    }
}
