namespace Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

/// <summary>
/// A exceção do termo de responsabilidade na instalação: os gestores instalados não aceitam o termo, e a confirmação
/// registra se havia ou não versão vigente, na trilha da instalação e na das concessões dos gestores (ADR-0162).
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class TermoNaInstalacaoNoHostTests : InstalacaoTestBase, IAsyncLifetime
{
    public TermoNaInstalacaoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    public Task InitializeAsync() => ResetarAsync();

    public Task DisposeAsync() => ResetarAsync();

    [Fact(DisplayName = "A instalação sem termo publicado registra a exceção, e as trilhas da instalação e da concessão a mostram")]
    public async Task SemTermo_RegistraAExcecao()
    {
        Guid id = await ConfirmarAsync();

        (await TiposDaTrilhaDaInstalacaoAsync(id)).Should().Contain("termo_excecao_da_instalacao");
        (await TiposDaTrilhaDaConcessaoAsync(id, Diogo)).Should().Contain("termo_excecao_da_instalacao");
    }

    [Fact(DisplayName = "A instalação com termo já vigente, estado anômalo, segue e registra a exceção com a versão lida")]
    public async Task ComTermoVigente_SegueERegistraAVersao()
    {
        await TermoNoHostDeTeste.PublicarAsync(Fixture.Factory.Services, "anterior à instalação");

        Guid id = await ConfirmarAsync();

        (await TiposDaTrilhaDaInstalacaoAsync(id)).Should().Contain("termo_excecao_da_instalacao");
        (await ListarGestoresAtivosAsync()).Should().HaveCount(2);
    }

    private async Task<Guid> ConfirmarAsync()
    {
        using HttpClient paulo = Cliente(Paulo);
        Guid id = await ChegarAoCodigoEnviadoAsync(paulo);
        string codigo = await UltimoCodigoAsync(Paulo);
        (await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/confirmacao", new { codigo })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        return id;
    }
}
