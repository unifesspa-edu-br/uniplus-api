namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// A matriz normativa de derivação de modalidade publicada por leitura: recortada para o que o
/// processo oferta, e na forma exata que o <c>PUT</c> recebe.
/// </summary>
/// <remarks>
/// O round-trip é o ponto: quem monta o edital pega a proposta e a envia de volta sem
/// transformação nenhuma. Se as duas formas divergirem, a proposta vira trabalho manual de
/// tradução no cliente — e é justamente essa tradução que faria a lei ser reescrita fora daqui.
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class RegrasDerivacaoNormativasEndpointTests
{
    /** 64 hex — o formato de SHA-256 que a referência de regra exige; valor arbitrário. */
    private static readonly string HashDeTeste = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    private const string NormativasMediaType = "application/vnd.uniplus.regras-derivacao-normativas.v1+json";

    private readonly CascadingFixture _fixture;

    public RegrasDerivacaoNormativasEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "A matriz normativa só propõe regra que contribui modalidade ofertada")]
    public async Task Get_RecortaPelaOfertaDoProcesso()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Get_RecortaPelaOfertaDoProcesso));

        using JsonDocument doc = await ctx.ObterNormativasAsync();
        JsonElement raiz = doc.RootElement;

        raiz.GetArrayLength().Should().Be(1);
        JsonElement config = raiz[0];
        config.GetProperty("codigoFato").GetString().Should().Be("MODALIDADE");

        JsonElement regras = config.GetProperty("regras");
        string[] contribuidos = [.. regras.EnumerateArray().Select(r => r.GetProperty("contribui").GetString()!)];

        // O seeder oferta só ampla concorrência; as nove regras de cota da matriz contribuem
        // código que este processo não oferta, e o PUT as recusaria uma a uma.
        contribuidos.Should().Equal("AC");
        regras[0].GetProperty("quando").ValueKind.Should().Be(
            JsonValueKind.Null, "a ampla concorrência é a âncora incondicional — quando null, nunca []");
    }

    [Fact(DisplayName = "A matriz normativa volta pelo PUT sem transformação do cliente")]
    public async Task Get_RoundTripDiretoNoPut()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Get_RoundTripDiretoNoPut));

        using JsonDocument proposta = await ctx.ObterNormativasAsync();

        HttpResponseMessage gravacao = await ctx.PutRegrasAsync(proposta.RootElement);
        gravacao.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using JsonDocument processo = await ctx.ObterProcessoAsync();
        JsonElement gravadas = processo.RootElement.GetProperty("regrasDerivacao");
        gravadas.GetArrayLength().Should().Be(1);
        gravadas[0].GetProperty("codigoFato").GetString().Should().Be("MODALIDADE");
        gravadas[0].GetProperty("regras").GetArrayLength().Should().Be(1);
    }

    /**
     * O caso que o endpoint existe para servir, e que o round-trip de ampla concorrência não
     * alcança: ofertando cota, a matriz traz regras COM condição, e essas condições citam o que
     * o candidato responde na inscrição — se quer concorrer a cada cota, e se veio de escola
     * pública. Quem gravar a matriz antes de coletar esses campos é recusado, e é por isso que
     * o cliente pergunta pela matriz ANTES de gravar os campos do formulário.
     */
    [Fact(DisplayName = "A matriz com cota traz as condições, e só é gravável depois de o processo coletar os fatos que ela cita")]
    public async Task Get_ComCota_ExigeOsFatosQueAsRegrasCitam()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Get_ComCota_ExigeOsFatosQueAsRegrasCitam));
        await ctx.OfertarCotaAsync();

        using JsonDocument doc = await ctx.ObterNormativasAsync();
        JsonElement regras = doc.RootElement[0].GetProperty("regras");

        string[] contribuidos = [.. regras.EnumerateArray().Select(r => r.GetProperty("contribui").GetString()!)];
        contribuidos.Should().BeEquivalentTo(["AC", "LB_PPI"]);

        string[] citados =
        [
            .. regras.EnumerateArray()
                .Select(r => r.GetProperty("quando"))
                .Where(q => q.ValueKind == JsonValueKind.Array)
                .SelectMany(q => q.EnumerateArray())
                .SelectMany(c => c.EnumerateArray())
                .Select(c => c.GetProperty("fato").GetString()!)
                .Distinct(),
        ];
        citados.Should().BeEquivalentTo(["EGRESSO_ESCOLA_PUBLICA", "CONCORRER_PPI", "CONCORRER_RENDA"]);

        // Sem coletar o que as regras citam, o próprio PUT recusa a matriz que acabou de ser
        // proposta — a proposta é coerente com o domínio, não com o estado do processo.
        HttpResponseMessage semColeta = await ctx.PutRegrasAsync(doc.RootElement);
        semColeta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await ctx.ColetarAsync(citados);

        HttpResponseMessage comColeta = await ctx.PutRegrasAsync(doc.RootElement);
        comColeta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "Processo inexistente não tem matriz a propor")]
    public async Task Get_ProcessoInexistente_404()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Get_ProcessoInexistente_404));

        HttpResponseMessage resposta = await ctx.ObterNormativasBrutoAsync(Guid.CreateVersion7());

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record Contexto(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId)
    {
        public async Task<HttpResponseMessage> PutRegrasAsync(JsonElement corpo)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/regras-derivacao", UriKind.Relative))
            {
                Content = JsonContent.Create(corpo),
            };
            Autenticar(request);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        /** Acrescenta ao quadro de vagas uma cota da Lei de Cotas, ao lado da ampla concorrência. */
        public async Task OfertarCotaAsync()
        {
            await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            IProcessoSeletivoRepository repositorio =
                scope.ServiceProvider.GetRequiredService<IProcessoSeletivoRepository>();

            ProcessoSeletivo processo = (await repositorio.ObterParaMutacaoAsync(ProcessoId, default))!;
            ConfiguracaoDistribuicaoVagas atual = processo.DistribuicaoVagas.First();

            // O quadro é montado do zero, e não acrescentando à coleção carregada: reaproveitar
            // as modalidades já rastreadas pelo contexto as levaria para uma segunda
            // configuração, e o que interessa aqui é só o par ampla concorrência + cota.
            Result<ConfiguracaoDistribuicaoVagas> comCota = ConfiguracaoDistribuicaoVagas.Criar(
                ofertaCursoOrigemId: atual.OfertaCursoOrigemId,
                voBase: atual.VoBase,
                pr: atual.Pr,
                // Referência nova, não a carregada: a do agregado é entidade de propriedade com
                // chave identificadora, e reaproveitá-la noutra configuração é reapontar uma
                // chave — o contexto recusa.
                regraDistribuicao: ReferenciaRegra.Criar(
                    RegraDistribuicaoVagasCodigo.Institucional, "v1", HashDeTeste).Value!,
                regraAjuste: null,
                referenciaDemografica: null,
                modalidades: [AmplaConcorrencia(), Cota()]);
            comCota.IsSuccess.Should().BeTrue(comCota.Error?.Message);

            processo.DefinirDistribuicaoVagas([comCota.Value!], PrecondicaoIfMatch.Ausente)
                .IsSuccess.Should().BeTrue();
            await db.SaveChangesAsync();
        }

        /** Declara no formulário de inscrição os campos que as regras da matriz citam. */
        public async Task ColetarAsync(IReadOnlyList<string> codigos)
        {
            object[] fatos =
            [
                .. codigos.Select((codigo, ordem) => new
                {
                    fatoCodigo = codigo,
                    ordem,
                    rotulo = codigo,
                    tipoRenderizacao = "BOOLEANO",
                    obrigatorio = true,
                    precondicao = (object?)null,
                }),
            ];

            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/fatos-coletados", UriKind.Relative))
            {
                Content = JsonContent.Create(fatos),
            };
            Autenticar(request);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
            HttpResponseMessage resposta = await Client.SendAsync(request).ConfigureAwait(false);
            resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        public async Task<JsonDocument> ObterNormativasAsync()
        {
            HttpResponseMessage resposta = await ObterNormativasBrutoAsync(ProcessoId).ConfigureAwait(false);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync().ConfigureAwait(false));
        }

        public async Task<HttpResponseMessage> ObterNormativasBrutoAsync(Guid processoId)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                new Uri(
                    $"/api/selecao/processos-seletivos/{processoId}/regras-derivacao/normativas",
                    UriKind.Relative));
            Autenticar(request);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(NormativasMediaType));
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task<JsonDocument> ObterProcessoAsync()
        {
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}", UriKind.Relative));
            Autenticar(request);
            request.Headers.Accept.Add(
                MediaTypeWithQualityHeaderValue.Parse("application/vnd.uniplus.processo-seletivo.v1+json"));
            HttpResponseMessage resposta = await Client.SendAsync(request).ConfigureAwait(false);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync().ConfigureAwait(false));
        }
    }

    private async Task<Contexto> SemearRascunhoAsync(string nome)
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        HttpClient client = api.CreateClient();

        Guid processoId;
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder
                .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
            processoId = processo.Id;
        }

        return new Contexto(api, client, processoId);
    }

    /** A ampla concorrência, que toda distribuição do ramo da Lei de Cotas contém. */
    private static ModalidadeSelecionada AmplaConcorrencia() =>
        ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: "Ampla concorrência",
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 30).Value!;

    /** A cota de pretos, pardos e indígenas de baixa renda egressos de escola pública. */
    private static ModalidadeSelecionada Cota() =>
        ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "LB_PPI",
            descricao: "Baixa renda, PPI, egresso de escola pública",
            naturezaLegal: NaturezaLegalModalidade.CotaReservada,
            composicaoVagas: ComposicaoVagasModalidade.DentroDoVr,
            composicaoOrigemCodigo: null,
            // Cota reservada da Lei de Cotas segue a cascata legal — o domínio recusa
            // "sem remanejamento" para ela.
            regraRemanejamento: RegraRemanejamentoModalidade.SegueCascata,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Lei 12.711/2012",
            quantidadeDeclarada: 10).Value!;

    private static void Autenticar(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }
}
