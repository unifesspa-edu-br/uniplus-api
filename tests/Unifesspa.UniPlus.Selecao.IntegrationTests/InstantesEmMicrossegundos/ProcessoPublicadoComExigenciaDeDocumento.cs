namespace Unifesspa.UniPlus.Selecao.IntegrationTests.InstantesEmMicrossegundos;

using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Um processo publicado pela API cuja fase de inscrição exige um documento em PDF: o que o candidato precisa para
/// começar o envio de um arquivo.
/// </summary>
internal static class ProcessoPublicadoComExigenciaDeDocumento
{
    public static async Task<(Guid ProcessoId, Guid ExigenciaId)> PublicarAsync(InstantesEmResiduoApiFactory api)
    {
        api.Relogio.Liberar();
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoEditalId, Guid exigenciaId) = await SemearAsync(api);
        using HttpClient client = api.CreateClient();
        using HttpResponseMessage publicacao = await InstantesDeTeste.EnviarComoAdministradorAsync(
            client, HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/publicacao", PedidoDePublicacao(documentoEditalId));
        publicacao.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicacao.Content.ReadAsStringAsync());

        await EsperaDeAtoRegistrado.AguardarAsync(api, await AtoDaVersaoAsync(api, processoId), "ato da abertura", processoId);
        await AguardarDivulgacaoAsync(client, processoId);
        return (processoId, exigenciaId);
    }

    private static object PedidoDePublicacao(Guid documentoEditalId) => new
    {
        numero = "001/2027",
        documentoEditalId,
        ato = new
        {
            orgao = "CEPS",
            serie = "EDITAL",
            ano = 2027,
            dataPublicacao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            assinante = "Diretor do CEPS",
            tipoAtoCodigo = DadosDoAtoDeTeste.TipoAbertura,
        },
    };

    private static async Task<(Guid ProcessoId, Guid DocumentoEditalId, Guid ExigenciaId)> SemearAsync(InstantesEmResiduoApiFactory api)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        Guid exigenciaId = Guid.Empty;
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
            db,
            $"PS com exigência de documento {Guid.CreateVersion7()}",
            complementar: processo => exigenciaId = ExigirDocumento(processo),
            fimDasInscricoes: new DateTimeOffset(2099, 12, 31, 0, 0, 0, TimeSpan.Zero));
        return (processo.Id, documento.Id, exigenciaId);
    }

    private static Guid ExigirDocumento(ProcessoSeletivo processo)
    {
        DocumentoExigido exigencia = ExigenciaDoPdf(processo.CronogramaFases.Single().Id);
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        return exigencia.Id;
    }

    private static DocumentoExigido ExigenciaDoPdf(Guid faseId) => DocumentoExigido.Criar(
        faseId, TipoDocumentoSeed.Itens[0].Id, "AUTODECLARACAO_ETNICO_RACIAL", "Autodeclaração étnico-racial", "PESSOAL",
        ProtecaoDoDocumentoDeTeste.Pessoal, Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: null, condicoes: [],
        basesLegais: [BaseLegalDaResolucao()], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(false, [("PDF", null)]).Value!,
        tamanhoMaximoBytes: null, modelo: null, finalidade: FinalidadeFormulario.Inscricao).Value!;

    private static DocumentoExigidoBaseLegal BaseLegalDaResolucao() => DocumentoExigidoBaseLegal.Criar(
        "Res. Unifesspa 532/2021, art. 12", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, null).Value!;

    private static async Task<Guid> AtoDaVersaoAsync(InstantesEmResiduoApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>().VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .Select(v => v.AtoCriadorId)
            .SingleAsync();
    }

    /// <summary>A divulgação do certame chega pela fila depois do registro do ato: antes, o certame responde 404.</summary>
    private static async Task AguardarDivulgacaoAsync(HttpClient client, Guid processoId)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            using HttpResponseMessage resposta = await client.GetAsync(new Uri($"/api/selecao/certames/{processoId}", UriKind.Relative));
            if (resposta.StatusCode == HttpStatusCode.OK)
            {
                return;
            }

            DateTimeOffset.UtcNow.Should().BeBefore(limite, "a divulgação do certame não chegou depois do registro do ato");
            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }
    }
}
