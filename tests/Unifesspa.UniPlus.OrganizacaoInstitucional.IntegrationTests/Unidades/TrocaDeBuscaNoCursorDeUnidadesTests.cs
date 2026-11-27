namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>Continuar o cursor emitido para uma busca de unidades sob outra busca.</summary>
[Collection(OrganizacaoEndpointCollection.Name)]
public sealed class TrocaDeBuscaNoCursorDeUnidadesTests : IAsyncLifetime
{
    private readonly OrganizacaoEndpointFixture _fixture;
    private readonly List<string> _marcas = [];

    public TrocaDeBuscaNoCursorDeUnidadesTests(OrganizacaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (string marca in _marcas)
        {
            await _fixture.RemoverUnidadesDaMarcaAsync(marca);
        }
    }

    [Fact(DisplayName = "O cursor emitido para uma busca de unidades é recusado quando continuado com outra busca")]
    public async Task Listar_CursorDeUmaBuscaContinuadoComOutra_Retorna400() =>
        (await ContinuarComAsync(static _ => "outrabusca")).Should().Be(HttpStatusCode.BadRequest);

    [Fact(DisplayName = "O cursor emitido para uma busca de unidades continua com a mesma busca, sem contar os espaços das pontas")]
    public async Task Listar_CursorDeUmaBuscaContinuadoComEspacosNasPontas_Retorna200() =>
        (await ContinuarComAsync(static marca => $"%20%20{marca}%20")).Should().Be(HttpStatusCode.OK);

    [Fact(DisplayName = "O cursor emitido para um tipo de unidade é recusado quando continuado com outro tipo")]
    public async Task Listar_CursorDeUmTipoContinuadoComOutro_Retorna400()
    {
        string marca = Guid.NewGuid().ToString("N")[..10];
        _marcas.Add(marca);
        await _fixture.SemearUnidadeAsync($"{marca}1", TipoUnidade.Centro);
        await _fixture.SemearUnidadeAsync($"{marca}2", TipoUnidade.Centro);
        using HttpClient client = _fixture.Factory.CreateClient();
        PaginaDeUnidades primeira = await PaginaDeUnidades.ConsultarAsync(client, $"q={marca}&tipo={(int)TipoUnidade.Centro}&limit=1");
        primeira.Next.Should().NotBeNull("limit=1 com 2 unidades do tipo deve emitir próxima página");

        PaginaDeUnidades seguinte = await PaginaDeUnidades.LerAsync(
            client, primeira.Next!.Replace($"tipo={(int)TipoUnidade.Centro}", $"tipo={(int)TipoUnidade.Departamento}", StringComparison.Ordinal));

        seguinte.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<HttpStatusCode> ContinuarComAsync(Func<string, string> buscaDaContinuacao)
    {
        string marca = Guid.NewGuid().ToString("N")[..10];
        _marcas.Add(marca);
        await _fixture.SemearUnidadeAsync($"{marca}1", TipoUnidade.Centro);
        await _fixture.SemearUnidadeAsync($"{marca}2", TipoUnidade.Centro);
        using HttpClient client = _fixture.Factory.CreateClient();
        PaginaDeUnidades primeira = await PaginaDeUnidades.ConsultarAsync(client, $"q={marca}&limit=1");
        primeira.Next.Should().NotBeNull("limit=1 com 2 unidades na busca deve emitir próxima página");

        PaginaDeUnidades seguinte = await PaginaDeUnidades.LerAsync(
            client, primeira.Next!.Replace($"q={marca}", $"q={buscaDaContinuacao(marca)}", StringComparison.Ordinal));

        return seguinte.Status;
    }
}
