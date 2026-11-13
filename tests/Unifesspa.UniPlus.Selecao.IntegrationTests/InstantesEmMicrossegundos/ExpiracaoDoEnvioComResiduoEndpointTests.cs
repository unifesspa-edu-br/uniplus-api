namespace Unifesspa.UniPlus.Selecao.IntegrationTests.InstantesEmMicrossegundos;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

/// <summary>
/// O fim da validade do envio e do rascunho, pela borda HTTP, com um relógio que entrega a 7ª casa decimal: a resposta, que
/// sai da entidade em memória, e a linha gravada trazem o instante em microssegundos inteiros.
/// </summary>
[Collection(ColecaoInstantesEmResiduo.Name)]
public sealed class ExpiracaoDoEnvioComResiduoEndpointTests
{
    private static readonly TimeSpan PrazoDoEnvio = TimeSpan.FromSeconds(900);
    private static readonly TimeSpan PrazoDoRascunho = TimeSpan.FromDays(30);

    private readonly InstantesEmResiduoFixture _fixture;

    public ExpiracaoDoEnvioComResiduoEndpointTests(InstantesEmResiduoFixture fixture) => _fixture = fixture;

    private InstantesEmResiduoApiFactory Fabrica => _fixture.Factory;

    [Fact(DisplayName = "Iniciar o envio do documento a publicar devolve e grava a validade em microssegundos inteiros")]
    public async Task DocumentoAPublicar_ValidadeEmMicrossegundos()
    {
        Guid processoId = await SemearProcessoAsync();

        JsonElement corpo = await EnviarComORelogioNoResiduoAsync(
            HttpMethod.Post,
            $"/api/selecao/processos-seletivos/{processoId}/documentos-a-publicar",
            new { finalidade = FinalidadeDoDocumento.DocumentoDoEdital, nomeArquivo = "edital.pdf", tipoDeMidia = TiposDeMidiaDeDocumento.Pdf });

        DateTimeOffset esperado = InstantesDeTeste.EmMicrossegundos + PrazoDoEnvio;
        corpo.GetProperty("expiraEm").GetDateTimeOffset().Should().Be(esperado);
        Guid id = corpo.GetProperty("documentoAPublicarId").GetGuid();
        (await LerAsync(db => db.DocumentosAPublicar.AsNoTracking().SingleAsync(d => d.Id == id))).ExpiraEm.Should().Be(esperado);
    }

    [Fact(DisplayName = "Iniciar o envio do modelo de documento devolve e grava a validade em microssegundos inteiros")]
    public async Task ModeloDeDocumento_ValidadeEmMicrossegundos()
    {
        Guid processoId = await SemearProcessoAsync();

        JsonElement corpo = await EnviarComORelogioNoResiduoAsync(
            HttpMethod.Post,
            $"/api/selecao/processos-seletivos/{processoId}/modelos-de-documento",
            new { nomeArquivo = "modelo.docx", formato = "DOCX" });

        DateTimeOffset esperado = InstantesDeTeste.EmMicrossegundos + PrazoDoEnvio;
        corpo.GetProperty("expiraEm").GetDateTimeOffset().Should().Be(esperado);
        Guid id = corpo.GetProperty("modeloDeDocumentoId").GetGuid();
        (await LerAsync(db => db.ModelosDeDocumento.AsNoTracking().SingleAsync(m => m.Id == id))).ExpiraEm.Should().Be(esperado);
    }

    [Fact(DisplayName = "Salvar o rascunho da publicação grava a validade de 30 dias em microssegundos inteiros")]
    public async Task RascunhoDaPublicacao_ValidadeEmMicrossegundos()
    {
        Guid processoId = await SemearProcessoAsync();

        await EnviarComORelogioNoResiduoAsync(
            HttpMethod.Put,
            $"/api/selecao/processos-seletivos/{processoId}/rascunho-da-publicacao",
            new { versao = 1, conteudo = new { numero = "001/2027" } });

        RascunhoDePublicacao gravado = await LerAsync(db => db.RascunhosDePublicacao.AsNoTracking()
            .SingleAsync(r => r.ProcessoSeletivoId == processoId));
        gravado.ExpiraEm.Should().Be(InstantesDeTeste.EmMicrossegundos + PrazoDoRascunho);
    }

    private async Task<JsonElement> EnviarComORelogioNoResiduoAsync(HttpMethod metodo, string rota, object corpo)
    {
        Fabrica.Relogio.ParaNo(InstantesDeTeste.ComResiduoDeTicks);
        using HttpClient client = Fabrica.CreateClient();
        using HttpResponseMessage resposta = await InstantesDeTeste.EnviarComoAdministradorAsync(client, metodo, rota, corpo);
        string texto = await resposta.Content.ReadAsStringAsync();
        resposta.IsSuccessStatusCode.Should().BeTrue(texto);
        return texto.Length == 0 ? default : JsonDocument.Parse(texto).RootElement.Clone();
    }

    private async Task<Guid> SemearProcessoAsync()
    {
        await using AsyncServiceScope escopo = Fabrica.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
            db, $"Instantes em microssegundos {Guid.CreateVersion7()}");
        return processo.Id;
    }

    private async Task<T> LerAsync<T>(Func<SelecaoDbContext, Task<T>> leitura)
    {
        await using AsyncServiceScope escopo = Fabrica.Services.CreateAsyncScope();
        return await leitura(escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>());
    }
}
