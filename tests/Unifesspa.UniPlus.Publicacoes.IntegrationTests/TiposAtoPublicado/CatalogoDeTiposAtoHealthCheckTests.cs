namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.HealthChecks;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Publicacoes.Infrastructure.HealthChecks.CatalogoDeTiposAtoDeclarado;

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
public sealed class CatalogoDeTiposAtoHealthCheckTests : IAsyncLifetime
{
    private const string NomeDaConferencia = "catalogo-tipos-ato";
    private const string NomeSintetico = "Tipo sintético da conferência";

    private static readonly DateOnly DiaSeguinteAoDeBelem = new(2026, 6, 16);

    private readonly PublicacoesEndpointFixture _fixture;

    public CatalogoDeTiposAtoHealthCheckTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Devolve a tabela ao estado em que a encontrou. A fixture é compartilhada por cinco classes,
    /// e linha sintética esquecida vira falha em teste alheio.
    /// </summary>
    public async Task DisposeAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        await contexto.Set<TipoAtoPublicado>()
            .Where(tipo => tipo.Nome == NomeSintetico)
            .ExecuteDeleteAsync(CancellationToken.None);
    }

    /// <summary>
    /// Ambiente sem catálogo carregado não é catálogo contraditório: a conferência fala sobre o
    /// que está cadastrado. Acusar ali degradaria todo ambiente recém-criado, e foi o que derrubou
    /// o teste de saúde do módulo Seleção quando a ausência chegou a contar. A precondição é a
    /// tabela vazia, e a fixture é compartilhada por cinco classes: limpar só as linhas sintéticas
    /// desta classe deixaria as das outras de pé. Esvaziar é seguro porque cada teste da coleção
    /// arranja o que precisa.
    /// </summary>
    [Fact(DisplayName = "Cadastro vazio não degrada")]
    public async Task Conferencia_QuandoOCadastroEstaVazio_NaoDeveDegradar()
    {
        await EsvaziarCadastroAsync();

        using WebApplicationFactory<HostAssemblyMarker> host = _fixture.Factory.ComConferenciaDoCatalogoLigada();
        HealthReportEntry entrada = await ConsultarAsync(host);

        entrada.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact(DisplayName = "Cadastro que sustenta o catálogo declarado não degrada")]
    public async Task Conferencia_QuandoOCadastroSustentaOCatalogo_DeveFicarSaudavel()
    {
        await SemearCatalogoDeclaradoAsync();

        using WebApplicationFactory<HostAssemblyMarker> host = _fixture.Factory.ComConferenciaDoCatalogoLigada();
        HealthReportEntry entrada = await ConsultarAsync(host);

        entrada.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact(DisplayName = "Atributo declarado que o cadastro nega degrada e nomeia o código")]
    public async Task Conferencia_QuandoOCadastroNegaUmAtributoDeclarado_DeveDegradar()
    {
        LinhaDeclarada alvo = await SemearNegandoAsync(
            linha => linha.EhResultado, linha => linha with { EhResultado = false });

        using WebApplicationFactory<HostAssemblyMarker> host = _fixture.Factory.ComConferenciaDoCatalogoLigada();
        HealthReportEntry entrada = await ConsultarAsync(host);

        entrada.Status.Should().Be(
            HealthStatus.Degraded,
            "sem o sinalizador nenhuma etapa que dependa deste ato admite recurso, e nada mais no sistema acusa isso");
        entrada.Description.Should().Contain(alvo.Codigo);
        entrada.Description.Should().Contain("determina a situação do candidato");
    }

    /// <summary>
    /// A causa é genérica: qualquer atributo do catálogo nasce neutro e desliga capacidade em
    /// silêncio. Sem congelar a configuração, publicar e retificar param.
    /// </summary>
    [Fact(DisplayName = "A conferência alcança os demais atributos, não só o de resultado")]
    public async Task Conferencia_QuandoOCadastroNegaOutroAtributo_DeveDegradar()
    {
        await SemearNegandoAsync(
            linha => linha.CongelaConfiguracao, linha => linha with { CongelaConfiguracao = false });

        using WebApplicationFactory<HostAssemblyMarker> host = _fixture.Factory.ComConferenciaDoCatalogoLigada();
        HealthReportEntry entrada = await ConsultarAsync(host);

        entrada.Status.Should().Be(HealthStatus.Degraded);
        entrada.Description.Should().Contain("congela a configuração");
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
        await SemearNegandoAsync(
            linha => !linha.EhResultado, linha => linha with { EhResultado = true });

        using WebApplicationFactory<HostAssemblyMarker> host = _fixture.Factory.ComConferenciaDoCatalogoLigada();
        HealthReportEntry entrada = await ConsultarAsync(host);

        entrada.Status.Should().Be(HealthStatus.Healthy);
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
        await SemearNegandoAsync(
            linha => linha.EhResultado, linha => linha with { EhResultado = false });

        HealthReportEntry entrada = await ConsultarAsync(_fixture.Factory);

        entrada.Status.Should().Be(
            HealthStatus.Healthy,
            "a contradição existe, e mesmo assim a conferência se cala num ambiente que não a pediu");
        entrada.Description.Should().Contain("está desligada");
    }

    /// <summary>
    /// Às 22h30 em Belém já é o dia seguinte em UTC. A versão que nega o atributo e termina no dia
    /// seguinte ainda vigora no dia civil de Belém, e a conferência tem de acusá-la: pelo relógio
    /// UTC ela já estaria encerrada e o ambiente apareceria saudável.
    /// </summary>
    [Fact(DisplayName = "Versão que nega o atributo e termina no dia seguinte em UTC degrada às 22h30 em Belém")]
    public async Task Conferencia_QuandoAVersaoQueNegaTerminaNoDiaSeguinteEmUtc_DeveDegradarEmBelem()
    {
        await SemearNegandoAsync(
            linha => linha.EhResultado, linha => linha with { EhResultado = false }, DiaSeguinteAoDeBelem);
        using WebApplicationFactory<HostAssemblyMarker> host = _fixture.Factory
            .ComConferenciaDoCatalogoLigada()
            .ComRelogioEm(AjustesDoHostDeTeste.VinteEDuasEMeiaEmBelem);

        HealthReportEntry entrada = await ConsultarAsync(host);

        entrada.Status.Should().Be(HealthStatus.Degraded);
        entrada.Description.Should().Contain("determina a situação do candidato");
    }

    [Fact(DisplayName = "Fuso institucional não reconhecido degrada e diz que a conferência não está sendo feita")]
    public async Task Conferencia_QuandoOFusoNaoEReconhecido_DeveDegradarComARecusaNaDescricao()
    {
        await SemearCatalogoDeclaradoAsync();

        using WebApplicationFactory<HostAssemblyMarker> host = _fixture.Factory.ComConferenciaDoCatalogoLigada().ComFusoNaoReconhecido();
        HealthReportEntry entrada = await ConsultarAsync(host);

        entrada.Status.Should().Be(HealthStatus.Degraded);
        entrada.Description.Should().Contain("O dia de hoje no fuso institucional não pôde ser calculado");
        entrada.Description.Should().Contain($"'{FusoInstitucional.ZoneId}' não é reconhecido");
    }

    [Fact(DisplayName = "Conferência desligada não lê o fuso e fica saudável mesmo com o fuso não reconhecido")]
    public async Task Conferencia_QuandoDesligadaEOFusoNaoEReconhecido_DeveFicarSaudavel()
    {
        using WebApplicationFactory<HostAssemblyMarker> host = _fixture.Factory.ComFusoNaoReconhecido();
        HealthReportEntry entrada = await ConsultarAsync(host);

        entrada.Status.Should().Be(HealthStatus.Healthy);
        entrada.Description.Should().Contain("está desligada");
    }

    [Fact(DisplayName = "Falha na leitura do cadastro degrada pelo registro, com a exceção anexada")]
    public async Task Conferencia_QuandoALeituraDoCadastroLanca_DeveDegradarComAExcecaoAnexada()
    {
        using ServiceProvider provedor = ServicosDeSaudeComBancoInacessivel();

        HealthReport relatorio = await provedor.GetRequiredService<HealthCheckService>().CheckHealthAsync(CancellationToken.None);

        relatorio.Entries[NomeDaConferencia].Status.Should().Be(HealthStatus.Degraded);
        relatorio.Entries[NomeDaConferencia].Exception.Should().NotBeNull();
    }

    /// <summary>
    /// O serviço de saúde isolado, com a conferência registrada como no host (mesmo status de falha e mesmas
    /// marcas) e um banco que não existe: trocar o banco do host inteiro derrubaria a sua inicialização, e o que
    /// se quer provar é só a conversão da exceção da leitura.
    /// </summary>
    private ServiceProvider ServicosDeSaudeComBancoInacessivel()
    {
        HealthCheckRegistration registroDoHost = _fixture.Factory.Services
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Single(registro => registro.Name == NomeDaConferencia);
        ServiceCollection servicos = new();
        servicos.AddLogging();
        servicos.AddHealthChecks().Add(new HealthCheckRegistration(
            NomeDaConferencia, _ => ConferenciaSemBanco(), registroDoHost.FailureStatus, registroDoHost.Tags));

        return servicos.BuildServiceProvider();
    }

    private static CatalogoDeTiposAtoHealthCheck ConferenciaSemBanco() =>
        new(
            AjustesDoHostDeTeste.ContextoSemBanco(),
            TimeProvider.System,
            new ResolvedorFusoDeTeste(),
            Options.Create(new CatalogoDeTiposAtoOptions { Conferir = true }));

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
            .Should().ContainSingle(registrado => registrado.Name == NomeDaConferencia).Subject;

        registro.Tags.Should().NotContain("ready");
        registro.FailureStatus.Should().Be(
            HealthStatus.Degraded,
            "dado de cadastro errado não pode derrubar o ambiente em que alguém precisa entrar para corrigi-lo");
        registro.Timeout.Should().NotBe(
            Timeout.InfiniteTimeSpan,
            "a conferência consulta uma tabela, e espera infinita seguraria a sonda até o pod sair do Service");
    }

    /// <summary>Roda só a conferência do catálogo; quem cria o host derivado é quem o descarta.</summary>
    private static async Task<HealthReportEntry> ConsultarAsync(WebApplicationFactory<HostAssemblyMarker> host)
    {
        HealthCheckService servico = host.Services.GetRequiredService<HealthCheckService>();

        HealthReport relatorio = await servico.CheckHealthAsync(
            registro => string.Equals(registro.Name, NomeDaConferencia, StringComparison.Ordinal),
            CancellationToken.None);

        relatorio.Entries.Should().ContainKey(
            NomeDaConferencia,
            "a conferência precisa estar registrada para dizer alguma coisa");

        return relatorio.Entries[NomeDaConferencia];
    }

    /// <summary>Semeia o catálogo declarado e troca a linha escolhida por outra, que nega o atributo.</summary>
    private async Task<LinhaDeclarada> SemearNegandoAsync(
        Func<LinhaDeclarada, bool> escolha,
        Func<LinhaDeclarada, LinhaDeclarada> nega,
        DateOnly? vigenciaFim = null)
    {
        LinhaDeclarada alvo = (await SemearCatalogoDeclaradoAsync()).First(escolha);

        await RemoverAsync(alvo.Codigo);
        await GravarAsync(nega(alvo), vigenciaFim);

        return alvo;
    }

    private async Task<LinhaDeclarada[]> SemearCatalogoDeclaradoAsync()
    {
        await EsvaziarCadastroAsync();

        LinhaDeclarada[] declaradas = CatalogoDeclarado();
        declaradas.Should().NotBeEmpty("o catálogo embutido precisa chegar ao assembly do módulo");

        foreach (LinhaDeclarada linha in declaradas)
        {
            await GravarAsync(linha);
        }

        return declaradas;
    }

    /// <summary>
    /// O catálogo declarado vigente hoje, lido pelo mesmo código que a conferência usa e no mesmo dia
    /// civil do fuso institucional: a fixture pode substituir o <see cref="TimeProvider"/>, e usar
    /// o relógio do sistema aqui semearia o conjunto de um dia enquanto a conferência confere o de outro.
    /// </summary>
    private LinhaDeclarada[] CatalogoDeclarado()
    {
        IServiceProvider servicos = _fixture.Factory.Services;
        TimeProvider relogio = servicos.GetRequiredService<TimeProvider>();
        IResolvedorFusoInstitucional fuso = servicos.GetRequiredService<IResolvedorFusoInstitucional>();

        Result<DateOnly> hoje = fuso.DiaCivilDe(relogio.GetUtcNow());

        return VigentesEm(hoje.Value);
    }

    private async Task EsvaziarCadastroAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        await contexto.Set<TipoAtoPublicado>().ExecuteDeleteAsync(CancellationToken.None);
    }

    private async Task GravarAsync(LinhaDeclarada linha, DateOnly? vigenciaFim = null)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        contexto.Set<TipoAtoPublicado>().Add(Versao(linha, vigenciaFim));
        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private static TipoAtoPublicado Versao(LinhaDeclarada linha, DateOnly? vigenciaFim)
    {
        Result<TipoAtoPublicado> criado = TipoAtoPublicado.Criar(
            codigo: linha.Codigo, nome: NomeSintetico,
            congelaConfiguracao: linha.CongelaConfiguracao, unicoPorObjeto: linha.UnicoPorObjeto,
            efeitoIrreversivel: linha.EfeitoIrreversivel, ehResultado: linha.EhResultado,
            vigenciaInicio: new DateOnly(2020, 1, 1), vigenciaFim: vigenciaFim,
            baseLegal: null, publicavelComoAvulso: linha.PublicavelComoAvulso,
            exigeAtoCorrigido: linha.ExigeAtoCorrigido);

        criado.IsSuccess.Should().BeTrue("o arranjo precisa dizer qual regra recusou, e não morrer numa referência nula");

        return criado.Value!;
    }

    private async Task RemoverAsync(string codigo)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        await contexto.Set<TipoAtoPublicado>()
            .Where(tipo => tipo.Codigo == codigo)
            .ExecuteDeleteAsync(CancellationToken.None);
    }
}
