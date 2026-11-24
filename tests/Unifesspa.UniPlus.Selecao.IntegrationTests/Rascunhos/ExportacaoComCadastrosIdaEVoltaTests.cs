namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using System.Net;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static ClienteDoRascunhoDeTeste;

[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class ExportacaoComCadastrosIdaEVoltaTests(CascadingFixture fixture) : IAsyncLifetime
{
    private const string Conta = "leandro.viana";
    private const string Descricao = "Descrição fictícia do motivo";
    private const string NomeDocumento = "Comprovante fictício";
    private const string Finalidade = "Comprovação documental.";
    private readonly ClienteDoRascunhoDeTeste _cliente = new(fixture);

    public async Task InitializeAsync() => await ConcessoesDeTeste.RemoverDaPessoaAsync(fixture.Factory.Services, Conta);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ExportarEImportar_ComCadastros_PreservaEscolhasEReconstroiDescritivos()
    {
        string codigo = await SemearCadastrosAsync();
        await _cliente.ConcederAsync(Conta, UnidadesDeTeste.Ceps.OrigemId);
        Guid original = await CriarAsync(DocumentoCompleto(codigo));
        JsonObject primeiro = await ExportarAsync(original);
        await ConferirDescritivosAsync(primeiro, codigo);
        new EsquemaDoDocumentoDaConfiguracao().Avaliar(primeiro).Should().BeEmpty();
        Guid copia = await CriarAsync(AlterarDescritivos(primeiro));
        copia.Should().NotBe(original);
        await ConferirDocumentosGuardadosAsync(original, copia, codigo);
        JsonObject segundo = await ExportarAsync(copia);
        CompararExportacoes(primeiro, original, segundo, copia);
    }

    private async Task<string> SemearCadastrosAsync()
    {
        string codigo = $"TESTE_{Guid.CreateVersion7():N}".ToUpperInvariant();
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        TipoDocumento tipo = TipoDocumento.Criar(new DeclaracaoDoTipoDocumento(codigo, NomeDocumento, null, "IDENTIFICACAO", null, null, null,
            ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal)).Value!;
        MotivoDeDecisao motivo = MotivoDeDecisao.Criar(new CamposDoMotivo("ISENCAO_DA_TAXA", codigo, Descricao,
            FundamentoIsencao.CadastroUnico.Codigo(), SentidoDoMotivo.Desfavoravel)).Value!;
        banco.Add(tipo);
        banco.Add(motivo);
        await banco.SaveChangesAsync();
        return codigo;
    }

    private static string DocumentoCompleto(string codigo) => $$"""
        {
          "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
          "identificacao": { "nome": "Processo fictício", "unidadeAdministradoraCodigo": "CEPS", "tipoProcessoCodigo": "SiSU" },
          "cronograma": { "fases": [{ "chave": "inscricao", "tipoFaseCodigo": "INSCRICAO", "unidadeResponsavelCodigo": "CEPS" }] },
          "documentosExigidos": { "exigencias": [{ "chave": "comprovante", "tipoDocumentoCodigo": "{{codigo}}", "faseChave": "inscricao" }] },
          "motivosDeDecisao": [{ "faseChave": "inscricao", "tipoDeDecisaoCodigo": "ISENCAO_DA_TAXA", "codigo": "{{codigo}}" }],
          "provaPresencial": { "temProvaPresencial": true, "municipios": [{ "codigoIbge": "1504208", "uf": "PA" }, { "codigoIbge": "1501402", "uf": "PA" }] }
        }
        """;

    private async Task<Guid> CriarAsync(string documento)
    {
        using HttpResponseMessage resposta = await _cliente.EnviarAsync(Conta, HttpMethod.Post, Rota, documento, chave: Guid.CreateVersion7().ToString());
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        return (await Corpo(resposta)).GetProperty("id").GetGuid();
    }

    private async Task<JsonObject> ExportarAsync(Guid id)
    {
        using HttpResponseMessage resposta = await _cliente.EnviarAsync(Conta, HttpMethod.Get, $"{Rota}/{id}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!.AsObject();
    }

    private async Task ConferirDescritivosAsync(JsonObject documento, string codigo)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Unidade unidade = await banco.Unidades.AsNoTracking().SingleAsync(u => u.Id == UnidadesDeTeste.Ceps.OrigemId);
        TipoProcesso tipo = await banco.TiposProcesso.AsNoTracking().SingleAsync(t => t.Codigo == "SiSU" && t.Ativo);
        JsonNode identificacao = documento["identificacao"]!;
        identificacao["tipoProcessoNome"]!.GetValue<string>().Should().Be(tipo.Nome);
        JsonNode fase = documento["cronograma"]!["fases"]![0]!;
        fase["unidadeResponsavelCodigo"]!.GetValue<string>().Should().Be("CEPS");
        fase["unidadeResponsavelNome"]!.GetValue<string>().Should().Be(unidade.Nome);
        fase["unidadeResponsavelSigla"]!.GetValue<string>().Should().Be(unidade.Sigla);
        ConferirDocumentoEMotivo(documento, codigo);
        documento["provaPresencial"]!["municipios"]!.AsArray().Should().OnlyContain(m => m!.AsObject().ContainsKey("nome") == false);
    }

    private static void ConferirDocumentoEMotivo(JsonObject documento, string codigo)
    {
        JsonNode exigencia = documento["documentosExigidos"]!["exigencias"]![0]!;
        exigencia["tipoDocumentoCodigo"]!.GetValue<string>().Should().Be(codigo);
        exigencia["tipoDocumentoNome"]!.GetValue<string>().Should().Be(NomeDocumento);
        exigencia["tipoDocumentoCategoria"]!.GetValue<string>().Should().Be("IDENTIFICACAO");
        exigencia["classificacaoProtecao"]!.GetValue<string>().Should().Be("PESSOAL");
        exigencia["finalidadeTratamento"]!.GetValue<string>().Should().Be(Finalidade);
        exigencia["hipoteseLegal"]!.GetValue<string>().Should().Be("CUMPRIMENTO_OBRIGACAO_LEGAL");
        JsonNode motivo = documento["motivosDeDecisao"]![0]!;
        motivo["descricao"]!.GetValue<string>().Should().Be(Descricao);
        motivo["subclassificacao"]!.GetValue<string>().Should().Be("CADASTRO_UNICO");
        motivo["sentido"]!.GetValue<string>().Should().Be("DESFAVORAVEL");
        motivo["sentidoRotulo"]!.GetValue<string>().Should().Be("desfavorável");
        motivo["versaoDoEditalDeEntrada"]!.GetValue<int>().Should().Be(1);
    }

    private static string AlterarDescritivos(JsonObject exportado)
    {
        JsonObject alterado = exportado.DeepClone().AsObject();
        alterado["identificacao"]!["tipoProcessoNome"] = "Nome alterado";
        alterado["cronograma"]!["fases"]![0]!["unidadeResponsavelNome"] = "Unidade alterada";
        alterado["motivosDeDecisao"]![0]!["descricao"] = "Descrição alterada";
        alterado["motivosDeDecisao"]![0]!["sentidoRotulo"] = "Rótulo alterado";
        return alterado.ToJsonString();
    }

    private async Task ConferirDocumentosGuardadosAsync(Guid original, Guid copia, string codigo)
    {
        JsonNode guardado = await DocumentoGuardadoAsync(original);
        JsonNode.DeepEquals(guardado, JsonNode.Parse(DocumentoCompleto(codigo))).Should().BeTrue();
        JsonNode.DeepEquals(guardado, await DocumentoGuardadoAsync(copia)).Should().BeTrue();
    }

    private async Task<JsonNode> DocumentoGuardadoAsync(Guid id)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        string documento = await banco.RascunhosDeProcessosSeletivos.AsNoTracking().Where(r => r.Id == id).Select(r => r.Documento).SingleAsync();
        return JsonNode.Parse(documento)!;
    }

    private static void CompararExportacoes(JsonObject primeiro, Guid original, JsonObject segundo, Guid copia)
    {
        primeiro["id"]!.GetValue<Guid>().Should().Be(original);
        segundo["id"]!.GetValue<Guid>().Should().Be(copia);
        primeiro.Remove("id");
        segundo.Remove("id");
        JsonNode.DeepEquals(primeiro, segundo).Should().BeTrue();
    }
}
