namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ObrigatoriedadesLegais;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

[Collection(CascadingCollection.Name)]
public sealed class RotulosDeObrigatoriedadeLegalEndpointTests(CascadingFixture fixture) : IAsyncLifetime
{
    private readonly List<Guid> _regras = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        IQueryable<ObrigatoriedadeLegal> regras = banco.ObrigatoriedadesLegais.Where(regra => _regras.Contains(regra.Id));
        foreach (ObrigatoriedadeLegal regra in regras)
        {
            regra.MarkAsDeleted("teste", DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        }

        await banco.SaveChangesAsync();
    }

    [Theory]
    [InlineData(TodosOsTiposDeProcesso.Codigo, TodosOsTiposDeProcesso.Rotulo)]
    [InlineData("SISU", "SiSU")]
    public async Task Consultas_TipoDaRegra_RetornamCodigoERotulo(string codigo, string rotulo)
    {
        Guid id = await CriarRegraAsync(codigo);
        using HttpClient cliente = fixture.Factory.CreateClient();
        using JsonDocument porId = JsonDocument.Parse(await cliente.GetStringAsync(new Uri($"/api/selecao/obrigatoriedades-legais/{id}", UriKind.Relative)));
        using JsonDocument lista = JsonDocument.Parse(await cliente.GetStringAsync(new Uri($"/api/selecao/obrigatoriedades-legais?tipoProcesso={codigo}&limit=100", UriKind.Relative)));

        Conferir(porId.RootElement, codigo, rotulo);
        Conferir(lista.RootElement.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == id), codigo, rotulo);
    }

    private async Task<Guid> CriarRegraAsync(string codigo)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ObrigatoriedadeLegal regra = ObrigatoriedadeLegal.Criar(codigo, CategoriaObrigatoriedade.Outros,
            $"TEST_{Guid.NewGuid():N}".ToUpperInvariant(), new ConcorrenciaDuplaObrigatoria(),
            new TextoNormativo("Descrição", "Lei", null, null), new DateOnly(2020, 1, 1), null).Value!;
        banco.ObrigatoriedadesLegais.Add(regra);
        await banco.SaveChangesAsync();
        _regras.Add(regra.Id);
        return regra.Id;
    }

    private static void Conferir(JsonElement regra, string codigo, string rotulo)
    {
        JsonElement tipo = regra.GetProperty("tipoProcesso");
        tipo.GetProperty("codigo").GetString().Should().Be(codigo);
        tipo.GetProperty("rotulo").GetString().Should().Be(rotulo);
        regra.TryGetProperty("tipoProcessoCodigo", out _).Should().BeFalse();
    }
}
