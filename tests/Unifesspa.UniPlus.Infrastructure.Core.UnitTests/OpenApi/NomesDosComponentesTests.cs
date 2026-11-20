namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi.Apoio;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi.Apoio.ModeloDeCodigosDeTeste;

public sealed class NomesDosComponentesTests
{
    public static class Escola
    {
        public enum Andamento
        {
            [CodigoDeContrato("aberto", "Aberto")]
            Aberto = 1,
        }
    }

    public static class Oficina
    {
        public enum Andamento
        {
            [CodigoDeContrato("fechado", "Fechado")]
            Fechado = 1,
        }

        public sealed record Resposta(CodigoComRotulo<Andamento> Situacao);
    }

    public sealed record RespostaDaEscola(CodigoComRotulo<Escola.Andamento> Situacao);

    public sealed record DoisCamposComOMesmoTipo(CodigoComRotulo<Escola.Andamento> Primeiro, CodigoComRotulo<Escola.Andamento> Segundo);

    public sealed record ComDtoDeMesmoNome(CodigoComRotulo<Escola.Andamento> Situacao, Andamento Outro);

    public enum Passo
    {
        Primeiro = 1,
    }

    public sealed record ComPassoAnulavel(Passo Atual, Passo? Anterior);

    public sealed record ComTipoDeNomeDosCodigos(CodigoComRotulo<Escola.Andamento> Situacao, AndamentoCodigo Outro);

    public sealed record ComTipoDeNomeComDescricao(CodigoComDescricao<Fase> Situacao, FaseComDescricao Outro);

    // Tipos comuns com o nome que o gerador daria ao componente dos códigos e ao componente com descrição do enum.
    public sealed record AndamentoCodigo(string Texto);

    public sealed record FaseComDescricao(string Texto);

    // Um tipo comum chamado Andamento, que o gerador publicaria com o mesmo nome do componente do enum.
    public sealed record Andamento(string Texto);

    [Fact(DisplayName = "O componente leva o nome do enum, e o mesmo enum em dois campos usa um só componente e um só componente dos códigos")]
    public async Task MesmoEnum_UmSoComponente()
    {
        OpenApiDocument documento = await GerarAsync(app => app.MapGet("/a", () => new DoisCamposComOMesmoTipo(
            CodigoComRotulo<Escola.Andamento>.De(Escola.Andamento.Aberto), CodigoComRotulo<Escola.Andamento>.De(Escola.Andamento.Aberto))));

        ComponentesPublicados(documento).Should().BeEquivalentTo(["DoisCamposComOMesmoTipo", "Andamento", "AndamentoCodigo"]);
    }

    [Fact(DisplayName = "O tipo e a sua versão anulável dividem o mesmo componente sem falhar")]
    public async Task TipoEVersaoAnulavel_MesmoComponente()
    {
        OpenApiDocument documento = await GerarAsync(app => app.MapGet("/passo", () => new ComPassoAnulavel(Passo.Primeiro, null)));

        ComponentesPublicados(documento).Should().BeEquivalentTo(["ComPassoAnulavel", "Passo"]);
    }

    [Fact(DisplayName = "Dois enums de mesmo nome em lugares diferentes fazem a geração do contrato falhar, e a mensagem cita os dois")]
    public async Task EnumsHomonimos_FalhaComMensagemClara()
    {
        Func<Task> geracao = () => GerarAsync(app =>
        {
            app.MapGet("/escola", () => new RespostaDaEscola(CodigoComRotulo<Escola.Andamento>.De(Escola.Andamento.Aberto)));
            app.MapGet("/oficina", () => new Oficina.Resposta(CodigoComRotulo<Oficina.Andamento>.De(Oficina.Andamento.Fechado)));
        });

        InvalidOperationException erro = (await geracao.Should().ThrowAsync<InvalidOperationException>()).Which;

        erro.Message.Should().Contain("'Andamento'").And.Contain("Escola+Andamento").And.Contain("Oficina+Andamento");
    }

    [Fact(DisplayName = "Um tipo comum de mesmo nome que o enum faz a geração do contrato falhar, e a mensagem cita os dois")]
    public async Task TipoComumDeMesmoNome_FalhaComMensagemClara()
    {
        Func<Task> geracao = () => GerarAsync(app =>
            app.MapGet("/misto", () => new ComDtoDeMesmoNome(CodigoComRotulo<Escola.Andamento>.De(Escola.Andamento.Aberto), new Andamento("x"))));

        InvalidOperationException erro = (await geracao.Should().ThrowAsync<InvalidOperationException>()).Which;

        erro.Message.Should().Contain("'Andamento'").And.Contain("Escola+Andamento").And.Contain("NomesDosComponentesTests+Andamento");
    }

    [Fact(DisplayName = "Um tipo comum com o nome do componente dos códigos do enum faz a geração falhar, e a mensagem cita os dois")]
    public async Task TipoComumComONomeDosCodigos_FalhaComMensagemClara()
    {
        Func<Task> geracao = () => GeradorDeDocumentoDeTeste.GerarAsync(app =>
            app.MapGet("/codigos", () => new ComTipoDeNomeDosCodigos(CodigoComRotulo<Escola.Andamento>.De(Escola.Andamento.Aberto), new AndamentoCodigo("x"))));

        InvalidOperationException erro = (await geracao.Should().ThrowAsync<InvalidOperationException>()).Which;

        erro.Message.Should().Contain("'AndamentoCodigo'").And.Contain("Escola+Andamento").And.Contain("NomesDosComponentesTests+AndamentoCodigo");
    }

    [Fact(DisplayName = "Um tipo comum com o nome do componente com descrição do enum faz a geração falhar, e a mensagem cita os dois")]
    public async Task TipoComumComONomeComDescricao_FalhaComMensagemClara()
    {
        Func<Task> geracao = () => GeradorDeDocumentoDeTeste.GerarAsync(app =>
            app.MapGet("/descricao", () => new ComTipoDeNomeComDescricao(CodigoComDescricao<Fase>.De(Fase.Inscricao), new FaseComDescricao("x"))));

        InvalidOperationException erro = (await geracao.Should().ThrowAsync<InvalidOperationException>()).Which;

        erro.Message.Should().Contain("'FaseComDescricao'").And.Contain("ModeloDeCodigosDeTeste+Fase").And.Contain("NomesDosComponentesTests+FaseComDescricao");
    }

    [Fact(DisplayName = "Um componente que o documento já tem com o nome dos códigos faz a geração falhar, em vez de ser trocado")]
    public async Task ComponenteJaExistenteComONomeDosCodigos_FalhaEmVezDeSerTrocado()
    {
        string documento = $"nomes-{Guid.NewGuid():N}";
        WebApplicationBuilder builder = GeradorDeDocumentoDeTeste.NovoConstrutor(documento, opcoes => opcoes.AddDocumentTransformer(AdicionarComponenteAndamentoCodigoAsync));
        await using WebApplication app = builder.Build();
        app.MapGet("/andamento", () => new RespostaDaEscola(CodigoComRotulo<Escola.Andamento>.De(Escola.Andamento.Aberto)));
        await app.StartAsync();

        Func<Task> geracao = () => GeradorDeDocumentoDeTeste.PublicarAsync(app, documento);

        InvalidOperationException erro = (await geracao.Should().ThrowAsync<InvalidOperationException>()).Which;
        erro.Message.Should().Contain("'AndamentoCodigo'").And.Contain("Escola+Andamento");
    }

    [Fact(DisplayName = "Enums de mesmo nome em documentos diferentes são legítimos: o dono do nome vale por documento")]
    public async Task EnumsHomonimosEmDocumentosDiferentes_CadaDocumentoTemOSeuComponente()
    {
        WebApplicationBuilder builder = GeradorDeDocumentoDeTeste.NovoConstrutor("escola");
        builder.Services.AddUniPlusOpenApi("oficina", builder.Configuration);
        await using WebApplication app = builder.Build();
        app.MapGet("/escola", () => new RespostaDaEscola(CodigoComRotulo<Escola.Andamento>.De(Escola.Andamento.Aberto))).WithGroupName("escola");
        app.MapGet("/oficina", () => new Oficina.Resposta(CodigoComRotulo<Oficina.Andamento>.De(Oficina.Andamento.Fechado))).WithGroupName("oficina");
        await app.StartAsync();

        DocumentoPublicado escola = await GeradorDeDocumentoDeTeste.PublicarAsync(app, "escola");
        DocumentoPublicado oficina = await GeradorDeDocumentoDeTeste.PublicarAsync(app, "oficina");

        CodigosDoComponente(escola, "AndamentoCodigo").Should().Equal("aberto");
        CodigosDoComponente(oficina, "AndamentoCodigo").Should().Equal("fechado");
        DocumentoPublicado.Referencia(oficina.Propriedade("Andamento", "codigo")).Should().Be("#/components/schemas/AndamentoCodigo");
    }

    private static string[] CodigosDoComponente(DocumentoPublicado documento, string componente) =>
        [.. documento.Componente(componente).Campo("enum").Textos()];

    private static Task AdicionarComponenteAndamentoCodigoAsync(OpenApiDocument documento, OpenApiDocumentTransformerContext contexto, CancellationToken cancellationToken)
    {
        documento.Components ??= new OpenApiComponents();
        documento.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        documento.Components.Schemas["AndamentoCodigo"] = new OpenApiSchema { Type = JsonSchemaType.String };

        return Task.CompletedTask;
    }

    private static IEnumerable<string> ComponentesPublicados(OpenApiDocument documento) =>
        documento.Components?.Schemas?.Keys ?? throw new InvalidOperationException("O documento gerado não tem componentes.");

    private static async Task<OpenApiDocument> GerarAsync(Action<WebApplication> mapas)
    {
        string documento = $"nomes-{Guid.NewGuid():N}";
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddUniPlusJsonOptions();
        builder.Services.AddUniPlusOpenApi(documento, builder.Configuration);
        await using WebApplication app = builder.Build();
        mapas(app);
        await app.StartAsync();

        return await app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(documento).GetOpenApiDocumentAsync();
    }
}
