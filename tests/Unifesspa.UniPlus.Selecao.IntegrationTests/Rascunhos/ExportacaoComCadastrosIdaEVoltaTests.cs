namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

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
        await using WebApplicationFactory<HostAssemblyMarker> host = ClienteDoRascunhoDeTeste.HostComGeo(fixture, GeoDosMunicipiosDeProva());
        ClienteDoRascunhoDeTeste cliente = new(fixture) { HostComGeoSubstituido = host };
        await cliente.ConcederAsync(Conta, UnidadesDeTeste.Ceps.OrigemId);
        Guid original = await cliente.CriarComDocumentoAsync(Conta, DocumentoCompleto(codigo));
        JsonObject primeiro = await cliente.ExportarAsync(Conta, original);
        await ConferirDescritivosAsync(primeiro, codigo);
        new EsquemaDoDocumentoDaConfiguracao().Avaliar(primeiro).Should().BeEmpty();
        Guid copia = await cliente.CriarComDocumentoAsync(Conta, AlterarDescritivos(primeiro));
        copia.Should().NotBe(original);
        await ConferirDocumentosGuardadosAsync(original, copia, codigo);
        JsonObject segundo = await cliente.ExportarAsync(Conta, copia);
        CompararExportacoes(primeiro, original, segundo, copia);
    }

    [Fact]
    public async Task ExportarEImportar_ComDescritivosDosCadastros_TrazONomeAoLadoDoCodigoESoPreservaOsCodigos()
    {
        CodigosSemeados codigos = await SemearCadastrosDosDescritivosAsync();
        await _cliente.ConcederAsync(Conta, UnidadesDeTeste.Ceps.OrigemId);
        Guid original = await _cliente.CriarComDocumentoAsync(Conta, DocumentoComDescritivos(codigos));
        JsonObject primeiro = await _cliente.ExportarAsync(Conta, original);
        await ConferirNomesDosCadastrosAsync(primeiro, codigos);
        new EsquemaDoDocumentoDaConfiguracao().Avaliar(primeiro).Should().BeEmpty();
        Guid copia = await _cliente.CriarComDocumentoAsync(Conta, AlterarDescritivosDosCadastros(primeiro));
        JsonNode.DeepEquals(await _cliente.DocumentoGuardadoAsync(copia), await _cliente.DocumentoGuardadoAsync(original)).Should().BeTrue();
        CompararExportacoes(primeiro, original, await _cliente.ExportarAsync(Conta, copia), copia);
    }

    private sealed record CodigosSemeados(string Condicao, string Recurso, string TipoDeficiencia, string BaseLegal);

    private static string NomeDe(string codigo) => $"Nome fictício de {codigo}";

    private async Task<CodigosSemeados> SemearCadastrosDosDescritivosAsync()
    {
        string sufixo = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        BaseLegalDoFada baseLegal = BaseLegalDoFada.Criar(
            "PORTARIA", $"Portaria fictícia {sufixo}", "Atenua as desigualdades de acesso.", [("1504208", "Marabá", "PA")]).Value!;
        CodigosSemeados codigos = new($"COND_{sufixo}", $"REC_{sufixo}", $"DEF_{sufixo}", baseLegal.Codigo.Valor);
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        banco.Add(CondicaoAtendimentoEspecializado.Criar(codigos.Condicao, NomeDe(codigos.Condicao), null).Value!);
        banco.Add(RecursoAcessibilidade.Criar(codigos.Recurso, NomeDe(codigos.Recurso), null).Value!);
        banco.Add(TipoDeficiencia.Criar(codigos.TipoDeficiencia, NomeDe(codigos.TipoDeficiencia), "Descrição fictícia").Value!);
        banco.Add(baseLegal);
        await banco.SaveChangesAsync();
        return codigos;
    }

    private static string DocumentoComDescritivos(CodigosSemeados codigos) => $$"""
        {
          "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
          "identificacao": { "nome": "Processo fictício", "unidadeAdministradoraCodigo": "CEPS", "tipoProcessoCodigo": "SISU" },
          "cronograma": { "fases": [{ "chave": "inscricao", "tipoFaseCodigo": "INSCRICAO", "unidadeResponsavelCodigo": "CEPS" }] },
          "atendimento": {
            "condicoes": [{ "condicaoCodigo": "{{codigos.Condicao}}" }],
            "recursos": [{ "recursoCodigo": "{{codigos.Recurso}}" }],
            "tiposDeficiencia": [{ "tipoDeficienciaCodigo": "{{codigos.TipoDeficiencia}}" }]
          },
          "fada": { "aplica": true, "regra": { "codigo": "FADA_REGIONAL", "versao": "v1" }, "fator": 1.1, "baseLegalCodigo": "{{codigos.BaseLegal}}" }
        }
        """;

    private async Task ConferirNomesDosCadastrosAsync(JsonObject documento, CodigosSemeados codigos)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        IFaseCanonicaReader fases = escopo.ServiceProvider.GetRequiredService<IFaseCanonicaReader>();
        IBaseLegalDoFadaReader bases = escopo.ServiceProvider.GetRequiredService<IBaseLegalDoFadaReader>();
        FaseCanonicaView inscricao = (await fases.ListarVivasPorCodigosAsync(["INSCRICAO"])).Single();
        documento["cronograma"]!["fases"]![0]!["tipoFaseNome"]!.GetValue<string>().Should().Be(inscricao.Nome);
        ConferirNomesDoAtendimento(documento["atendimento"]!, codigos);
        ConferirFada(documento["fada"]!, (await bases.ObterPorCodigoAsync(codigos.BaseLegal, CancellationToken.None))!);
    }

    private static void ConferirNomesDoAtendimento(JsonNode atendimento, CodigosSemeados codigos)
    {
        atendimento["condicoes"]![0]!["condicaoNome"]!.GetValue<string>().Should().Be(NomeDe(codigos.Condicao));
        atendimento["recursos"]![0]!["recursoNome"]!.GetValue<string>().Should().Be(NomeDe(codigos.Recurso));
        atendimento["tiposDeficiencia"]![0]!["tipoDeficienciaNome"]!.GetValue<string>().Should().Be(NomeDe(codigos.TipoDeficiencia));
    }

    private static void ConferirFada(JsonNode fada, BaseLegalDoFadaView baseLegal)
    {
        fada["tipoInstrumento"]!.GetValue<string>().Should().Be(baseLegal.TipoInstrumento);
        fada["identificacao"]!.GetValue<string>().Should().Be(baseLegal.Identificacao);
        fada["descricao"]!.GetValue<string>().Should().Be(baseLegal.Descricao);
        JsonNode municipio = fada["municipios"]![0]!;
        municipio["codigoIbge"]!.GetValue<string>().Should().Be("1504208");
        municipio["nome"]!.GetValue<string>().Should().Be("Marabá");
        municipio["uf"]!.GetValue<string>().Should().Be("PA");
    }

    private static string AlterarDescritivosDosCadastros(JsonObject exportado)
    {
        JsonObject alterado = exportado.DeepClone().AsObject();
        alterado["cronograma"]!["fases"]![0]!["tipoFaseNome"] = "Nome alterado";
        alterado["atendimento"]!["condicoes"]![0]!["condicaoNome"] = "Condição alterada";
        alterado["atendimento"]!["recursos"]![0]!["recursoNome"] = "Recurso alterado";
        alterado["atendimento"]!["tiposDeficiencia"]![0]!["tipoDeficienciaNome"] = "Tipo alterado";
        alterado["fada"]!["descricao"] = "Descrição alterada";
        alterado["fada"]!["municipios"]![0]!["nome"] = "Município alterado";
        return alterado.ToJsonString();
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
          "identificacao": { "nome": "Processo fictício", "unidadeAdministradoraCodigo": "CEPS", "tipoProcessoCodigo": "SISU" },
          "cronograma": { "fases": [{ "chave": "inscricao", "tipoFaseCodigo": "INSCRICAO", "unidadeResponsavelCodigo": "CEPS" }] },
          "documentosExigidos": { "exigencias": [{ "chave": "comprovante", "tipoDocumentoCodigo": "{{codigo}}", "faseChave": "inscricao" }] },
          "motivosDeDecisao": [{ "faseChave": "inscricao", "tipoDeDecisaoCodigo": "ISENCAO_DA_TAXA", "codigo": "{{codigo}}" }],
          "provaPresencial": { "temProvaPresencial": true, "municipios": [{ "codigoIbge": "1504208", "uf": "PA" }, { "codigoIbge": "1501402", "uf": "PA" }] }
        }
        """;

    private async Task ConferirDescritivosAsync(JsonObject documento, string codigo)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Unidade unidade = await banco.Unidades.AsNoTracking().SingleAsync(u => u.Id == UnidadesDeTeste.Ceps.OrigemId);
        TipoProcesso tipo = await banco.TiposProcesso.AsNoTracking().SingleAsync(t => t.Codigo == "SISU" && t.Ativo);
        JsonNode identificacao = documento["identificacao"]!;
        identificacao["tipoProcessoNome"]!.GetValue<string>().Should().Be(tipo.Nome);
        JsonNode fase = documento["cronograma"]!["fases"]![0]!;
        fase["unidadeResponsavelCodigo"]!.GetValue<string>().Should().Be("CEPS");
        fase["unidadeResponsavelNome"]!.GetValue<string>().Should().Be(unidade.Nome);
        fase["unidadeResponsavelSigla"]!.GetValue<string>().Should().Be(unidade.Sigla);
        ConferirDocumentoEMotivo(documento, codigo);
        ConferirNomesDosMunicipiosDeProva(documento["provaPresencial"]!["municipios"]!.AsArray());
    }

    private static void ConferirNomesDosMunicipiosDeProva(JsonArray municipios) =>
        municipios.Select(static municipio => municipio!["nome"]!.GetValue<string>()).Should().Equal("Marabá", "Belém");

    private static FakeConferenciaEnderecoGeo GeoDosMunicipiosDeProva() =>
        new FakeConferenciaEnderecoGeo().ComMunicipio("1504208", "PA", "Marabá").ComMunicipio("1501402", "PA", "Belém");

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
        alterado["provaPresencial"]!["municipios"]![0]!["nome"] = "Município alterado";
        return alterado.ToJsonString();
    }

    private async Task ConferirDocumentosGuardadosAsync(Guid original, Guid copia, string codigo)
    {
        JsonNode guardado = await _cliente.DocumentoGuardadoAsync(original);
        JsonNode.DeepEquals(guardado, JsonNode.Parse(DocumentoCompleto(codigo))).Should().BeTrue();
        JsonNode.DeepEquals(guardado, await _cliente.DocumentoGuardadoAsync(copia)).Should().BeTrue();
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
