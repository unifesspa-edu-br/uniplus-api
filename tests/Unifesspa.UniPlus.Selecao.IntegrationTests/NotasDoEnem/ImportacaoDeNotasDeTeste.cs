namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using System.Net.Http.Headers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem.ProcessoDoEnemPublicadoDeTeste;

/// <summary>A importação de um arquivo de retorno do INEP pela API, como administrador da plataforma.</summary>
internal static class ImportacaoDeNotasDeTeste
{
    public static async Task<HttpResponseMessage> ImportarAsync(HttpClient client, Publicado processo, byte[] arquivo)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(processo);

        string rota = $"/api/selecao/processos-seletivos/{processo.ProcessoId}/etapas/{processo.EtapaDoEnemId}/edicoes-do-enem/{Edicao}/lotes-de-notas";
        using MultipartFormDataContent formulario = Formulario(arquivo);
        using HttpRequestMessage pedido = new(HttpMethod.Post, new Uri(rota, UriKind.Relative)) { Content = formulario };
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        return await client.SendAsync(pedido).ConfigureAwait(false);
    }

    /// <summary>Grava, no processo publicado, a inscrição submetida de cada pessoa indicada, pela posição em <see cref="PersonasDeTeste.Candidatas"/>.</summary>
    public static async Task<Dictionary<int, Guid>> InscreverAsync(
        CascadingFixture fixture, Publicado publicado, IEnumerable<int> inscritas, IReadOnlyCollection<int> semSegundaOpcao)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Guid versaoId = await VersaoDoProcessoAsync(db, publicado);
        Dictionary<int, Guid> inscricoes = [];
        foreach (int i in inscritas)
        {
            inscricoes[i] = await InscreverUmaAsync(scope.ServiceProvider, db, publicado, versaoId, i, semSegundaOpcao.Contains(i));
        }

        return inscricoes;
    }

    private static Task<Guid> VersaoDoProcessoAsync(UniPlusDbContext db, Publicado publicado) =>
        db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == publicado.ProcessoId)
            .Select(v => v.Id)
            .SingleAsync();

    private static Task<Guid> InscreverUmaAsync(
        IServiceProvider servicos, UniPlusDbContext db, Publicado publicado, Guid versaoId, int pessoa, bool semSegundaOpcao) =>
        InscricoesDoEnemDeTeste.NovaInscricaoAsync(
            db,
            servicos.GetRequiredService<IUniPlusEncryptionService>(),
            servicos.GetRequiredService<IUniPlusBlindIndexService>(),
            publicado.ProcessoId, PersonasDeTeste.Candidatas[pessoa], SituacaoInscricao.Submetida, Edicao,
            ofertaCursoId: publicado.OfertaTecnologicaId,
            ofertaCursoSegundaOpcaoId: semSegundaOpcao ? null : publicado.OfertaHumanisticaId,
            versaoFormularioId: versaoId,
            nome: PersonasDeTeste.Candidatas[pessoa].Nome);

    private static MultipartFormDataContent Formulario(byte[] arquivo)
    {
        MultipartFormDataContent formulario = new();
        ByteArrayContent conteudo = new(arquivo);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        formulario.Add(conteudo, "arquivo", "retorno.txt");
        formulario.Add(new StringContent("INEP_ENEM_RETORNO"), "leiauteCodigo");
        formulario.Add(new StringContent("v1"), "leiauteVersao");
        return formulario;
    }
}
