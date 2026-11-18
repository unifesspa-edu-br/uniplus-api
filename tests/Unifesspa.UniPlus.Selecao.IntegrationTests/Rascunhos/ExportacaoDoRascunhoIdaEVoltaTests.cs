namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using static ClienteDoRascunhoDeTeste;

/// <summary>
/// O documento exportado volta como veio: a unidade que muda de código no cadastro continua a mesma pelo identificador, a
/// exportação traz o código novo no formato que o esquema aceita, e o envio do documento exportado é aceito.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class ExportacaoDoRascunhoIdaEVoltaTests : IAsyncLifetime
{
    private const string Leandro = "leandro.viana";

    private readonly CascadingFixture _fixture;
    private readonly ClienteDoRascunhoDeTeste _cliente;

    public ExportacaoDoRascunhoIdaEVoltaTests(CascadingFixture fixture)
    {
        _fixture = fixture;
        _cliente = new ClienteDoRascunhoDeTeste(fixture);
    }

    public async Task InitializeAsync() => await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Leandro);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "A unidade que muda de código continua a mesma: o documento exportado traz o código novo e o envio dele é aceito")]
    public async Task UnidadeQueMudaDeCodigo_ExportacaoVoltaAceitaENaoTrocaAUnidade()
    {
        string sufixo = Guid.CreateVersion7().ToString("N")[^8..];
        Guid unidadeId = await CriarUnidadeAsync(sufixo, $"CEPS-{sufixo}");
        await _cliente.ConcederAsync(Leandro, unidadeId);
        Guid id = await _cliente.CriarAsync(Leandro, $"CEPS-{sufixo}");

        (await AtualizarOCodigoAsync(unidadeId, $"CEPS.{sufixo}")).IsSuccess.Should().BeTrue();
        await SubstituirComOExportadoAsync(id, $"CEPS.{sufixo}");

        (await UnidadeDoRascunhoAsync(id)).Should().Be(unidadeId);
    }

    [Fact(DisplayName = "Mudar o código da unidade para um com barra é recusado e o cadastro fica com o código que tinha")]
    public async Task CodigoComBarra_NaoMudaOCadastro()
    {
        string sufixo = Guid.CreateVersion7().ToString("N")[^8..];
        string codigo = $"CEPS-{sufixo}";
        Guid unidadeId = await CriarUnidadeAsync(sufixo, codigo);

        Result atualizado = await AtualizarOCodigoAsync(unidadeId, $"CEPS/{sufixo}");

        atualizado.IsFailure.Should().BeTrue();
        atualizado.Errors.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(("codigo", UnidadeErrorCodes.CodigoFormatoInvalido));
        (await CodigoDaUnidadeAsync(unidadeId)).Should().Be(codigo);
    }

    private async Task SubstituirComOExportadoAsync(Guid id, string codigoNovo)
    {
        string exportado = await LerAsync(id, "\"1\"", codigoNovo);

        using HttpResponseMessage substituido = await _cliente.EnviarAsync(Leandro, HttpMethod.Put, $"{Rota}/{id}", exportado, ifMatch: "\"1\"");

        substituido.StatusCode.Should().Be(HttpStatusCode.OK);
        substituido.Headers.ETag!.Tag.Should().Be("\"2\"");
        await LerAsync(id, "\"2\"", codigoNovo);
    }

    private async Task<string> LerAsync(Guid id, string eTagEsperado, string codigoEsperado)
    {
        using HttpResponseMessage lido = await _cliente.EnviarAsync(Leandro, HttpMethod.Get, $"{Rota}/{id}");

        lido.StatusCode.Should().Be(HttpStatusCode.OK);
        lido.Headers.ETag!.Tag.Should().Be(eTagEsperado);
        string corpo = await lido.Content.ReadAsStringAsync();
        using JsonDocument documento = JsonDocument.Parse(corpo);
        documento.RootElement.GetProperty("identificacao").GetProperty("unidadeAdministradoraCodigo").GetString().Should().Be(codigoEsperado);
        return corpo;
    }

    private async Task<Guid> CriarUnidadeAsync(string sufixo, string codigo)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Unidade unidade = Unidade.Criar(
            $"Unidade de ida e volta {sufixo}",
            new IdentificadoresDaUnidade($"ida-e-volta-{sufixo}", $"IV{sufixo}", codigo, null),
            new CidadeDaUnidade("1504208", "Marabá", "PA"),
            new VigenciaDaUnidade(new DateOnly(2020, 1, 1), null),
            null,
            TipoUnidade.Centro,
            true).Value!;
        banco.Unidades.Add(unidade);
        await banco.SaveChangesAsync();
        return unidade.Id;
    }

    private async Task<Result> AtualizarOCodigoAsync(Guid unidadeId, string codigo)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Unidade unidade = await banco.Unidades.Include(static u => u.Historico).SingleAsync(u => u.Id == unidadeId);
        Result resultado = AtualizarSoOCodigo(unidade, codigo);
        if (resultado.IsSuccess)
        {
            await banco.SaveChangesAsync();
        }

        return resultado;
    }

    private static Result AtualizarSoOCodigo(Unidade unidade, string codigo) =>
        unidade.Atualizar(
            unidade.Nome,
            new MudancaDeIdentificadores(
                new IdentificadoresDaUnidade(unidade.Slug.Valor, unidade.Sigla, codigo, unidade.Alias),
                DateOnly.FromDateTime(DateTime.UtcNow),
                null),
            new CidadeDaUnidade(unidade.CidadeCodigoIbge, unidade.CidadeNome, unidade.CidadeUf),
            unidade.VigenciaFim,
            unidade.UnidadeSuperiorId,
            unidade.Tipo,
            unidade.UnidadeAcademica);

    private async Task<string> CodigoDaUnidadeAsync(Guid unidadeId)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return (await banco.Unidades.AsNoTracking().SingleAsync(u => u.Id == unidadeId)).Codigo;
    }

    private async Task<Guid> UnidadeDoRascunhoAsync(Guid id)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return (await banco.RascunhosDeProcessosSeletivos.AsNoTracking().SingleAsync(r => r.Id == id)).UnidadeAdministradoraOrigemId;
    }
}
