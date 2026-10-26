namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Um processo com a etapa de nota do ENEM e a edição 2025 declarada, publicado e divulgado pela
/// API de verdade: o escopo do envio e da importação é o da versão divulgada, então só um certame
/// que passou pela publicação o exercita.
/// </summary>
internal static class ProcessoDoEnemPublicadoDeTeste
{
    public const string Edicao = "2025";

    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    public sealed record Publicado(Guid ProcessoId, Guid EtapaDoEnemId);

    public static async Task<Publicado> SemearEPublicarAsync(CascadingFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await TiposDeAtoSeeder.SemearAsync(fixture.Factory.Services);

        Guid etapaDoEnem = Guid.Empty;
        ProcessoSeletivo processo;
        DocumentoAPublicar documento;
        await using (AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (processo, documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
                db,
                $"PS Medicina notas do ENEM {Guid.CreateVersion7()}",
                complementar: p => etapaDoEnem = ConfigurarEnem(p));
        }

        using HttpClient client = fixture.Factory.CreateClient();
        using (HttpResponseMessage publicacao = await PublicarAsync(client, processo.Id, documento.Id))
        {
            publicacao.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicacao.Content.ReadAsStringAsync());
        }

        await using (AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            Guid atoId = await db.VersoesConfiguracao.AsNoTracking()
                .Where(v => v.ProcessoSeletivoId == processo.Id)
                .Select(v => v.AtoCriadorId)
                .SingleAsync();
            await EsperaDeAtoRegistrado.AguardarAsync(fixture.Factory, atoId, "ato da abertura", processo.Id);
        }

        await AguardarDivulgacaoAsync(client, processo.Id);
        return new Publicado(processo.Id, etapaDoEnem);
    }

    private static Guid ConfigurarEnem(ProcessoSeletivo processo)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            "Nota do ENEM", CaraterEtapa.Classificatoria,
            TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!,
            peso: 1m, notaMinima: null, ordem: 1).Value!;
        Exigir(processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(
                regraCalculo: ReferenciaRegra.Criar(RegraCalculoCodigo.FormulaMediaPonderada, "v1", HashFixo).Value!,
                regraArredondamento: ReferenciaRegra.Criar(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", HashFixo).Value!,
                casasArredondamento: 2,
                regraOrdemAlocacao: ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", HashFixo).Value!,
                regrasEliminacao: [],
                baseadoEmEnem: true,
                resolucaoPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Resolucao,
                quadroPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Completo()).Value!,
            PrecondicaoIfMatch.Curinga));

        Exigir(processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Curinga));

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", "Ampla concorrência", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null,
            RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!;
        Exigir(processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(
                Guid.CreateVersion7(), 40, 1m,
                ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo).Value!,
                null, null, [modalidade], grupoAreaEnem: ("TECNOLOGICA", "Tecnológica"), ofertaCurso: OfertaCursoDeTeste.Medicina).Value!],
            FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Curinga));

        Exigir(processo.DefinirOpcoesDeclaradas(
            Regras.Services.EdicaoDoEnem.Fato, [OpcaoDeclaradaFato.Criar(Regras.Services.EdicaoDoEnem.Fato, Edicao, Edicao, 0).Value!], PrecondicaoIfMatch.Curinga));

        FatoColetado[] basicos = [.. FormularioDeTeste.DadosBasicos()];
        Exigir(processo.DefinirItens([.. basicos, FormularioDeTeste.CampoDaEdicaoDoEnem(basicos.Max(static f => f.Ordem) + 1)], PrecondicaoIfMatch.Curinga));

        return etapa.Id;
    }

    private static void Exigir(Result resultado) => resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

    private static async Task<HttpResponseMessage> PublicarAsync(HttpClient client, Guid processoId, Guid documentoEditalId)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = JsonContent.Create(new
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
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }

    private static async Task AguardarDivulgacaoAsync(HttpClient client, Guid processoId)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            using HttpResponseMessage resposta = await client.GetAsync(
                new Uri($"/api/selecao/certames/{processoId}", UriKind.Relative), CancellationToken.None);
            if (resposta.StatusCode == HttpStatusCode.OK)
            {
                return;
            }

            DateTimeOffset.UtcNow.Should().BeBefore(limite, "a divulgação do certame não chegou depois do registro do ato");
            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }
    }
}
