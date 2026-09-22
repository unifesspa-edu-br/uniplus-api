namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

/// <summary>
/// Os atributos que decidem o que um ato determina nascem falsos em toda linha antiga quando a
/// coluna é acrescentada, e o valor neutro desliga capacidade sem recusar nada. Estes testes
/// provam que o ambiente passa a acusar quando o cadastro não sustenta o catálogo declarado.
/// </summary>
/// <remarks>
/// O arranjo semeia o catálogo declarado inteiro, porque é esse o estado de um ambiente correto —
/// e é contra ele que cada caso introduz um desvio só. O catálogo vem do mesmo recurso embutido
/// que a conferência lê: uma lista escrita aqui passaria a valer contra si mesma.
/// </remarks>
[Collection(PublicacoesEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class CatalogoDeTiposAtoHealthCheckTests
{
    private const string NomeDaConferencia = "catalogo-tipos-ato";
    private const string NomeSintetico = "Tipo sintético da conferência";

    private readonly PublicacoesEndpointFixture _fixture;

    public CatalogoDeTiposAtoHealthCheckTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Ambiente sem catálogo carregado não é catálogo contraditório: a conferência fala sobre o
    /// que está cadastrado. Acusar ali degradaria todo ambiente recém-criado, e foi o que derrubou
    /// o teste de saúde do módulo Seleção quando a ausência chegou a contar.
    /// </summary>
    [Fact(DisplayName = "Cadastro vazio não degrada")]
    public async Task Conferencia_QuandoOCadastroEstaVazio_NaoDeveDegradar()
    {
        // A precondição é a tabela vazia, e a fixture é compartilhada por cinco classes: limpar
        // só as linhas sintéticas desta classe deixaria as das outras de pé e o caso não seria
        // exercitado. Esvaziar é seguro porque cada teste da coleção arranja o que precisa.
        await EsvaziarCadastroAsync();

        HealthReportEntry entrada = await ExecutarAsync();

        entrada.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact(DisplayName = "Cadastro que sustenta o catálogo declarado não degrada")]
    public async Task Conferencia_QuandoOCadastroSustentaOCatalogo_DeveFicarSaudavel()
    {
        try
        {
            await SemearCatalogoDeclaradoAsync();

            HealthReportEntry entrada = await ExecutarAsync();

            entrada.Status.Should().Be(HealthStatus.Healthy);
        }
        finally
        {
            await LimparAsync();
        }
    }

    [Fact(DisplayName = "Atributo declarado que o cadastro nega degrada e nomeia o código")]
    public async Task Conferencia_QuandoOCadastroNegaUmAtributoDeclarado_DeveDegradar()
    {
        try
        {
            IReadOnlyList<LinhaDoCatalogo> declaradas = await SemearCatalogoDeclaradoAsync();
            LinhaDoCatalogo alvo = declaradas.First(linha => linha.EhResultado);

            await RemoverAsync(alvo.Codigo);
            await GravarAsync(alvo with { EhResultado = false });

            HealthReportEntry entrada = await ExecutarAsync();

            entrada.Status.Should().Be(
                HealthStatus.Degraded,
                "sem o sinalizador nenhuma etapa que dependa deste ato admite recurso, e nada mais no sistema acusa isso");
            entrada.Description.Should().Contain(alvo.Codigo);
            entrada.Description.Should().Contain("determina a situação do candidato");
        }
        finally
        {
            await LimparAsync();
        }
    }

    /// <summary>
    /// A causa é genérica: qualquer atributo do catálogo nasce neutro e desliga capacidade em
    /// silêncio. Sem congelar a configuração, publicar e retificar param.
    /// </summary>
    [Fact(DisplayName = "A conferência alcança os demais atributos, não só o de resultado")]
    public async Task Conferencia_QuandoOCadastroNegaOutroAtributo_DeveDegradar()
    {
        try
        {
            IReadOnlyList<LinhaDoCatalogo> declaradas = await SemearCatalogoDeclaradoAsync();
            LinhaDoCatalogo alvo = declaradas.First(linha => linha.CongelaConfiguracao);

            await RemoverAsync(alvo.Codigo);
            await GravarAsync(alvo with { CongelaConfiguracao = false });

            HealthReportEntry entrada = await ExecutarAsync();

            entrada.Status.Should().Be(HealthStatus.Degraded);
            entrada.Description.Should().Contain("congela a configuração");
        }
        finally
        {
            await LimparAsync();
        }
    }

    /// <summary>
    /// A direção inversa não degrada, e é decisão e não esquecimento: ligar um atributo habilita
    /// uma capacidade, com efeito visível para quem configura o certame, e a decisão sobre alguns
    /// códigos é do administrador, por cadastro. Alarmar aqui obrigaria um deploy para silenciar
    /// um ato que o produto permite.
    /// </summary>
    [Fact(DisplayName = "Cadastro que liga um atributo onde o catálogo o nega não degrada")]
    public async Task Conferencia_QuandoOCadastroLigaOQueOCatalogoNega_NaoDeveDegradar()
    {
        try
        {
            IReadOnlyList<LinhaDoCatalogo> declaradas = await SemearCatalogoDeclaradoAsync();
            LinhaDoCatalogo alvo = declaradas.First(linha => !linha.EhResultado);

            await RemoverAsync(alvo.Codigo);
            await GravarAsync(alvo with { EhResultado = true });

            HealthReportEntry entrada = await ExecutarAsync();

            entrada.Status.Should().Be(HealthStatus.Healthy);
        }
        finally
        {
            await LimparAsync();
        }
    }

    /// <summary>
    /// A garantia que protege as demais suítes: sem o ambiente declarar que recebe o catálogo pelo
    /// bootstrap, a conferência não opina. Catálogo divergente é legítimo em ambiente que carrega
    /// só o que usa — as suítes do módulo Seleção criam tipos de ato com os atributos que cada
    /// cenário precisa —, e ligar a conferência ali degradaria um ambiente correto.
    /// </summary>
    [Fact(DisplayName = "Sem o ambiente declarar, a conferência não opina")]
    public async Task Conferencia_QuandoOAmbienteNaoDeclara_DeveFicarSaudavel()
    {
        try
        {
            IReadOnlyList<LinhaDoCatalogo> declaradas = await SemearCatalogoDeclaradoAsync();
            LinhaDoCatalogo alvo = declaradas.First(linha => linha.EhResultado);

            await RemoverAsync(alvo.Codigo);
            await GravarAsync(alvo with { EhResultado = false });

            HealthCheckService servico =
                _fixture.Factory.Services.GetRequiredService<HealthCheckService>();
            HealthReport relatorio = await servico.CheckHealthAsync(
                registro => string.Equals(registro.Name, NomeDaConferencia, StringComparison.Ordinal),
                CancellationToken.None);

            relatorio.Entries[NomeDaConferencia].Status.Should().Be(
                HealthStatus.Healthy,
                "a contradição existe, e mesmo assim a conferência se cala num ambiente que não a pediu");
            relatorio.Entries[NomeDaConferencia].Description.Should().Contain("está desligada");
        }
        finally
        {
            await LimparAsync();
        }
    }

    /// <summary>
    /// A conferência fica fora de <c>/health/ready</c> porque dado de cadastro não é critério de
    /// prontidão. A asserção é sobre o <b>registro</b>, e não executa conferência nenhuma — rodar
    /// os checks de prontidão aqui dispararia o descobrimento OIDC contra um Keycloak que a
    /// fixture não tem.
    /// </summary>
    [Fact(DisplayName = "A conferência não entra na prontidão")]
    public void Conferencia_NaoDeveIntegrarAProntidao()
    {
        IOptions<HealthCheckServiceOptions> opcoes =
            _fixture.Factory.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

        HealthCheckRegistration registro = opcoes.Value.Registrations
            .Should().ContainSingle(r => r.Name == NomeDaConferencia).Subject;

        registro.Tags.Should().NotContain("ready");
        registro.FailureStatus.Should().Be(
            HealthStatus.Degraded,
            "dado de cadastro errado não pode derrubar o ambiente em que alguém precisa entrar para corrigi-lo");
        registro.Timeout.Should().NotBe(
            Timeout.InfiniteTimeSpan,
            "a conferência consulta uma tabela, e espera infinita seguraria a sonda até o pod sair do Service");
    }

    /// <summary>
    /// A conferência é opt-in por ambiente, e a fixture não a declara — como nenhum ambiente de
    /// teste declara. Este host a liga, apontando para o mesmo banco, que é o que permite exercitar
    /// a lógica sem ligá-la para as demais suítes.
    /// </summary>
    private WebApplicationFactory<HostAssemblyMarker> ComConferenciaLigada() =>
        _fixture.Factory.WithWebHostBuilder(builder =>
            builder.UseSetting($"{CatalogoDeTiposAtoOptionsSection}:Conferir", "true"));

    private const string CatalogoDeTiposAtoOptionsSection = "Publicacoes:CatalogoDeTiposAto";

    private async Task<HealthReportEntry> ExecutarAsync()
    {
        using WebApplicationFactory<HostAssemblyMarker> host = ComConferenciaLigada();
        HealthCheckService servico = host.Services.GetRequiredService<HealthCheckService>();

        HealthReport relatorio = await servico.CheckHealthAsync(
            registro => string.Equals(registro.Name, NomeDaConferencia, StringComparison.Ordinal),
            CancellationToken.None);

        relatorio.Entries.Should().ContainKey(
            NomeDaConferencia,
            "a conferência precisa estar registrada para dizer alguma coisa");

        return relatorio.Entries[NomeDaConferencia];
    }

    /// <summary>Esvazia o cadastro inteiro, e não só as linhas desta classe.</summary>
    private async Task EsvaziarCadastroAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        PublicacoesDbContext ctx = scope.ServiceProvider.GetRequiredService<PublicacoesDbContext>();

        await ctx.Set<TipoAtoPublicado>().ExecuteDeleteAsync(CancellationToken.None);
    }

    private async Task<IReadOnlyList<LinhaDoCatalogo>> SemearCatalogoDeclaradoAsync()
    {
        await EsvaziarCadastroAsync();

        IReadOnlyList<LinhaDoCatalogo> declaradas = CatalogoDeclarado();
        declaradas.Should().NotBeEmpty("o catálogo embutido precisa chegar ao assembly do módulo");

        foreach (LinhaDoCatalogo linha in declaradas)
        {
            await GravarAsync(linha);
        }

        return declaradas;
    }

    /// <summary>
    /// Lê o catálogo do mesmo recurso embutido que a conferência usa, resolvendo a vigência pela
    /// mesma janela semiaberta e pelo mesmo relógio: a fixture pode substituir o
    /// <see cref="TimeProvider"/>, e usar o relógio do sistema aqui semearia o conjunto de um dia
    /// enquanto a conferência confere o de outro.
    /// </summary>
    private IReadOnlyList<LinhaDoCatalogo> CatalogoDeclarado()
    {
        TimeProvider relogio = _fixture.Factory.Services.GetRequiredService<TimeProvider>();
        DateOnly hoje = DateOnly.FromDateTime(relogio.GetUtcNow().UtcDateTime);

        Assembly assembly = typeof(PublicacoesDbContext).Assembly;
        using Stream? recurso = assembly.GetManifestResourceStream(
            "Unifesspa.UniPlus.Publicacoes.Infrastructure.seed-tipos-ato.json");

        if (recurso is null)
        {
            return [];
        }

        using JsonDocument documento = JsonDocument.Parse(recurso);
        return
        [
            .. documento.RootElement.EnumerateArray()
                .Where(linha => VigenteEm(linha, hoje))
                .Select(linha => new LinhaDoCatalogo(
                    linha.GetProperty("codigo").GetString()!,
                    linha.GetProperty("congelaConfiguracao").GetBoolean(),
                    linha.GetProperty("unicoPorObjeto").GetBoolean(),
                    linha.GetProperty("efeitoIrreversivel").GetBoolean(),
                    linha.GetProperty("ehResultado").GetBoolean())),
        ];
    }

    private static bool VigenteEm(JsonElement linha, DateOnly data)
    {
        DateOnly inicio = DateOnly.Parse(
            linha.GetProperty("vigenciaInicio").GetString()!, CultureInfo.InvariantCulture);

        JsonElement fimDeclarado = linha.GetProperty("vigenciaFim");
        DateOnly? fim = fimDeclarado.ValueKind == JsonValueKind.Null
            ? null
            : DateOnly.Parse(fimDeclarado.GetString()!, CultureInfo.InvariantCulture);

        return inicio <= data && (fim is null || fim > data);
    }

    private async Task GravarAsync(LinhaDoCatalogo linha)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        PublicacoesDbContext ctx = scope.ServiceProvider.GetRequiredService<PublicacoesDbContext>();

        Result<TipoAtoPublicado> criado = TipoAtoPublicado.Criar(
            codigo: linha.Codigo,
            nome: NomeSintetico,
            congelaConfiguracao: linha.CongelaConfiguracao,
            unicoPorObjeto: linha.UnicoPorObjeto,
            efeitoIrreversivel: linha.EfeitoIrreversivel,
            ehResultado: linha.EhResultado,
            vigenciaInicio: new DateOnly(2020, 1, 1),
            vigenciaFim: null,
            baseLegal: null);

        criado.IsSuccess.Should().BeTrue(
            "o arranjo precisa dizer qual regra recusou, e não morrer numa referência nula");

        ctx.Set<TipoAtoPublicado>().Add(criado.Value!);
        await ctx.SaveChangesAsync(CancellationToken.None);
    }

    private async Task RemoverAsync(string codigo)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        PublicacoesDbContext ctx = scope.ServiceProvider.GetRequiredService<PublicacoesDbContext>();

        await ctx.Set<TipoAtoPublicado>()
            .Where(tipo => tipo.Codigo == codigo)
            .ExecuteDeleteAsync(CancellationToken.None);
    }

    /// <summary>
    /// Devolve a tabela ao estado em que a encontrou. A fixture é compartilhada por cinco classes,
    /// e linha sintética esquecida vira falha em teste alheio.
    /// </summary>
    private async Task LimparAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        PublicacoesDbContext ctx = scope.ServiceProvider.GetRequiredService<PublicacoesDbContext>();

        await ctx.Set<TipoAtoPublicado>()
            .Where(tipo => tipo.Nome == NomeSintetico)
            .ExecuteDeleteAsync(CancellationToken.None);
    }

    private sealed record LinhaDoCatalogo(
        string Codigo,
        bool CongelaConfiguracao,
        bool UnicoPorObjeto,
        bool EfeitoIrreversivel,
        bool EhResultado);
}
