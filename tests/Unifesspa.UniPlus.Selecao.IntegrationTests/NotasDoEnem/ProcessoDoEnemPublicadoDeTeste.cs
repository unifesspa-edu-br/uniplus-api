namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
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

    /// <summary>
    /// O processo publicado: a etapa de nota do ENEM e as duas ofertas do edital, uma no grupo
    /// Tecnológica e outra no grupo Humanística I, que as inscrições de teste concorrem como 1ª e 2ª opção.
    /// </summary>
    public sealed record Publicado(Guid ProcessoId, Guid EtapaDoEnemId, Guid OfertaTecnologicaId, Guid OfertaHumanisticaId);

    /// <param name="fimDasInscricoes">O fim das inscrições; sem ele, o período já terminou.</param>
    public static async Task<Publicado> SemearEPublicarAsync(CascadingFixture fixture, DateTimeOffset? fimDasInscricoes = null)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await TiposDeAtoSeeder.SemearAsync(fixture.Factory.Services);

        (Publicado publicado, Guid documentoId) = await SemearProcessoAsync(fixture, fimDasInscricoes);

        using HttpClient client = fixture.Factory.CreateClient();
        await PublicarEConferirAsync(client, publicado.ProcessoId, documentoId);

        await AguardarRegistroAsync(fixture, publicado.ProcessoId);

        await AguardarDivulgacaoAsync(client, publicado.ProcessoId);
        return publicado;
    }

    private static async Task PublicarEConferirAsync(HttpClient client, Guid processoId, Guid documentoId)
    {
        using (HttpResponseMessage publicacao = await PublicarAsync(client, processoId, documentoId))
        {
            publicacao.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicacao.Content.ReadAsStringAsync());
        }

    }

    private static async Task<(Publicado Publicado, Guid DocumentoId)> SemearProcessoAsync(CascadingFixture fixture, DateTimeOffset? fimDasInscricoes)
    {
        Guid etapaDoEnem = Guid.Empty;
        Guid ofertaTecnologica = Guid.CreateVersion7();
        Guid ofertaHumanistica = Guid.CreateVersion7();
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Dictionary<string, ReferenciaRegra> regras = await db.RolDeRegras.Where(r => r.Versao == "v1").ToDictionaryAsync(r => r.Codigo, r => r.Referencia());
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"PS Medicina notas do ENEM {Guid.CreateVersion7()}", Complementar = p => etapaDoEnem = ConfigurarEnem(p, ofertaTecnologica, ofertaHumanistica, regras), FimDasInscricoes = fimDasInscricoes, ComBancaDeImportacaoDasNotas = true });
        return (new Publicado(processo.Id, etapaDoEnem, ofertaTecnologica, ofertaHumanistica), documento.Id);
    }

    private static async Task AguardarRegistroAsync(CascadingFixture fixture, Guid processoId)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Guid atoId = await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .Select(v => v.AtoCriadorId)
            .SingleAsync();
        await AtoNoRegistroCentral.LerAsync(fixture.Factory, atoId);
    }

    private static Guid ConfigurarEnem(ProcessoSeletivo processo, Guid ofertaTecnologica, Guid ofertaHumanistica, Dictionary<string, ReferenciaRegra> regras)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Nota do ENEM", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        DefinirClassificacao(processo, regras);

        Exigir(processo.DefinirEtapas([etapa]));

        DefinirDistribuicao(processo, ofertaTecnologica, ofertaHumanistica, regras);

        DefinirDadosDoEnem(processo);

        return etapa.Id;
    }

    private static void DefinirDistribuicao(ProcessoSeletivo processo, Guid ofertaTecnologica, Guid ofertaHumanistica, Dictionary<string, ReferenciaRegra> regras)
    {
        Exigir(processo.DefinirDistribuicaoVagas(
            [
                DistribuicaoDaOferta(ofertaTecnologica, ("TECNOLOGICA", "Tecnológica"), OfertaCursoDeTeste.Medicina, regras),
                DistribuicaoDaOferta(ofertaHumanistica, ("HUMANISTICA_I", "Humanística I"), OfertaCursoDeTeste.Pedagogia, regras),
            ],
            FatosDeModalidadeDeTeste.DoCatalogo));

    }

    private static void DefinirDadosDoEnem(ProcessoSeletivo processo)
    {
        Exigir(processo.DefinirOpcoesDeclaradas(
            Regras.Services.EdicaoDoEnem.Fato, [OpcaoDeclaradaFato.Criar(Regras.Services.EdicaoDoEnem.Fato, Edicao, Edicao, 0).Value!]));

        FatoColetado[] basicos = [.. FormularioDeTeste.DadosBasicos()];
        Exigir(processo.DefinirItens([.. basicos, FormularioDeTeste.CampoDaEdicaoDoEnem(basicos.Max(static f => f.Ordem) + 1)]));

    }

    private static void DefinirClassificacao(ProcessoSeletivo processo, Dictionary<string, ReferenciaRegra> regras)
    {
        Exigir(processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
            {
                RegraCalculo = regras[RegraDeCalculoDaNota.MediaPonderada.Codigo()],
                RegraArredondamento = regras[RegraDeArredondamentoDaNota.Truncar.Codigo()],
                CasasArredondamento = 2,
                RegraOrdemAlocacao = regras[RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo()],
                BaseadoEmEnem = true,
                ResolucaoPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Resolucao,
                QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(),
                EscalaNotaFinal = 100m,
            }).Value!));

    }

    private static ConfiguracaoDistribuicaoVagas DistribuicaoDaOferta(Guid ofertaCursoOrigemId, (string? Codigo, string? Rotulo) grupoAreaEnem, CopiaDaOfertaDeCurso ofertaCurso, Dictionary<string, ReferenciaRegra> regras)
    {
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { Descricao = "Ampla concorrência", RegraRemanejamento = null, BaseLegal = "Res. Unifesspa 532/2021" }, 40).Value!;

        return ConfiguracaoDistribuicaoVagas.Criar(ofertaCursoOrigemId,
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = Copiar(regras[RegraDeDistribuicaoDeVagas.Institucional.Codigo()]), Modalidades = [modalidade] },
            new FatosDaOfertaNoCadastro { GrupoAreaEnem = grupoAreaEnem, OfertaCurso = ofertaCurso }).Value!;
    }

    private static ReferenciaRegra Copiar(ReferenciaRegra regra) => ReferenciaRegra.De(regra.Codigo, regra.Versao, regra.Hash);

    private static void Exigir(Result resultado) => resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

    private static async Task<HttpResponseMessage> PublicarAsync(HttpClient client, Guid processoId, Guid documentoEditalId)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = JsonContent.Create(CorpoDaPublicacao(documentoEditalId)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }

    private static object CorpoDaPublicacao(Guid documentoEditalId)
    {
        return new
        {
            numero = "001/2027",
            documentoEditalId,
            ato = new
            {
                orgao = "CEPS",
                serie = "EDITAL",
                ano = 2027,
                dataDoAto = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                assinante = "Diretor do CEPS",
                tipoAtoCodigo = DadosDoAtoDeTeste.TipoAbertura,
            },
        };
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

            DateTimeOffset.UtcNow.Should().BeBefore(limite, "a divulgação do certame não existe depois da publicação");
            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }
    }
}
