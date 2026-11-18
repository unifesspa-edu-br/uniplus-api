namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using static RascunhosSemeadosParaTeste;

public sealed class DocumentoDoRascunhoPersistenciaFielTests(ProcessoSeletivoDbFixture fixture) : IClassFixture<ProcessoSeletivoDbFixture>
{
    private const string Conteudo = """{"formato":"uniplus.selecao.configuracao","versaoFormato":1,"identificacao":{"nome":"Certame — COMISSÃO D'EXEMPLO","localidadeCodigoIbge":"1504208","unidadeAdministradoraCodigo":"CEPS"},"classificacao":{"casasArredondamento":3}}""";

    [Fact]
    public async Task GravarELer_JsonComTiposEAcentuacao_ConservaConteudoERevisao()
    {
        Guid id = await SemearRascunhoAsync(fixture, Guid.CreateVersion7());
        await SubstituirDuasVezesAsync(id);
        await using UniPlusDbContext db = fixture.CreateDbContext();
        RascunhoDeProcessoSeletivo lido = (await Repositorio(db).ObterAsync(id))!;
        lido.Revisao.Should().Be(3);
        using JsonDocument documento = JsonDocument.Parse(lido.Documento);
        using JsonDocument esperado = JsonDocument.Parse(Conteudo);
        JsonElement.DeepEquals(documento.RootElement, esperado.RootElement).Should().BeTrue();
        documento.RootElement.GetProperty("identificacao").GetProperty("localidadeCodigoIbge").ValueKind.Should().Be(JsonValueKind.String);
        documento.RootElement.GetProperty("classificacao").GetProperty("casasArredondamento").GetInt32().Should().Be(3);
        documento.RootElement.GetProperty("identificacao").GetProperty("nome").GetString().Should().Be("Certame — COMISSÃO D'EXEMPLO");
        documento.RootElement.GetProperty("versaoFormato").GetInt32().Should().Be(1);
    }

    private async Task SubstituirDuasVezesAsync(Guid id)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        RascunhoDeProcessoSeletivo rascunho = (await Repositorio(db).ObterParaMutacaoAsync(id))!;
        DocumentoDoRascunho documento = DocumentoDoRascunho.Criar(Conteudo, "Certame de teste").Value!;
        rascunho.Substituir(documento, rascunho.UnidadeAdministradoraOrigemId, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
        rascunho.Substituir(documento, rascunho.UnidadeAdministradoraOrigemId, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
    }
}
