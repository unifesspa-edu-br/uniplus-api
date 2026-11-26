namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

/// <summary>
/// O documento exportado volta inteiro ao ser salvo: ele nomeia a unidade pelo código que ela tem hoje, e os campos só de
/// leitura que a exportação acrescenta não contam nos limites do documento declarado.
/// </summary>
public sealed class ExportacaoDoRascunhoTests
{
    private const string CodigoNovoDaUnidade = "CPS";

    private readonly CenarioDoRascunho _cenario = new();

    private Task<Result<RascunhoSalvoDto>> Substituir(RascunhoDeProcessoSeletivo rascunho, string documento) =>
        SubstituirRascunhoDeProcessoSeletivoCommandHandler.Handle(
            new SubstituirRascunhoDeProcessoSeletivoCommand(rascunho.Id, documento, CenarioDoRascunho.Precondicao(rascunho)),
            _cenario.Preparacao,
            _cenario.UnidadesDoDocumento,
            _cenario.Bloqueio,
            _cenario.UnitOfWork,
            _cenario.Resolucao,
            CancellationToken.None);

    private void OCodigoDaUnidadeMudou()
    {
        UnidadeView renomeada = CenarioDoRascunho.Ceps with { Codigo = CodigoNovoDaUnidade };
        _cenario.Unidades.ObterPorCodigoAsync(CenarioDoRascunho.Ceps.Codigo, Arg.Any<CancellationToken>()).Returns((UnidadeView?)null);
        _cenario.Unidades.ObterPorCodigoAsync(CodigoNovoDaUnidade, Arg.Any<CancellationToken>()).Returns(renomeada);
        _cenario.Unidades.ObterPorIdAsync(CenarioDoRascunho.Ceps.Id, Arg.Any<CancellationToken>()).Returns(renomeada);
    }

    // A moldura tem 8 nós declarados (a raiz, o formato, a versão, a identificação com o nome e os dois códigos, e a lista); o resto são os valores da lista.
    private static string DocumentoComNos(int nos) =>
        $$"""
        { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
          "identificacao": { "nome": "Processo Seletivo de Exemplo", "unidadeAdministradoraCodigo": "CEPS", "tipoProcessoCodigo": "SISU" },
          "valores": [ {{string.Join(',', Enumerable.Range(0, nos - 8))}} ] }
        """;

    [Fact(DisplayName = "A exportação traz o código que a unidade tem hoje, achada pelo identificador guardado, e não o do documento salvo")]
    public async Task Exportacao_TrazOCodigoAtualDaUnidade()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps);
        OCodigoDaUnidadeMudou();

        JsonObject exportado = await _cenario.Exportacao.ExportarAsync(rascunho, CancellationToken.None);

        exportado["identificacao"]!["unidadeAdministradoraCodigo"]!.GetValue<string>().Should().Be(CodigoNovoDaUnidade);
        exportado["identificacao"]!["unidadeAdministradoraNome"]!.GetValue<string>().Should().Be(CenarioDoRascunho.Ceps.Nome);
        await _cenario.Unidades.Received(1).ObterPorIdAsync(rascunho.UnidadeAdministradoraOrigemId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Salvar o documento exportado depois que o código da unidade mudou é aceito, sem recusar o acesso à unidade")]
    public async Task DocumentoExportado_DepoisDaMudancaDoCodigo_ESalvoDeVolta()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps);
        OCodigoDaUnidadeMudou();
        JsonObject exportado = await _cenario.Exportacao.ExportarAsync(rascunho, CancellationToken.None);

        Result<RascunhoSalvoDto> salvo = await Substituir(rascunho, exportado.ToJsonString());

        salvo.IsSuccess.Should().BeTrue();
        rascunho.UnidadeAdministradoraOrigemId.Should().Be(CenarioDoRascunho.Ceps.Id);
    }

    [Fact(DisplayName = "O documento guardado no limite de nós volta aceito depois de exportado com o id e os descritivos")]
    public async Task DocumentoNoLimiteDeNos_ExportadoEReimportado_EAceito()
    {
        Result<DocumentoPreparado> guardado = _cenario.Preparacao.Preparar(DocumentoComNos(LimitesDoDocumento.QuantidadeMaximaDeNos));
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps, guardado.Value!.Documento.Conteudo);
        _cenario.Tipos.ObterNomesPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string> { ["SISU"] = "Sistema de Seleção Unificada" });
        JsonObject exportado = await _cenario.Exportacao.ExportarAsync(rascunho, CancellationToken.None);

        Result<DocumentoPreparado> reimportado = _cenario.Preparacao.Preparar(exportado.ToJsonString());

        guardado.IsSuccess.Should().BeTrue();
        exportado["id"].Should().NotBeNull();
        exportado["identificacao"]!["tipoProcessoNome"].Should().NotBeNull();
        reimportado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Um nó acima do limite no documento declarado continua recusado, com ou sem os campos só de leitura")]
    public void DocumentoAcimaDoLimiteDeNos_ERecusado()
    {
        Result<DocumentoPreparado> recusado = _cenario.Preparacao.Preparar(DocumentoComNos(LimitesDoDocumento.QuantidadeMaximaDeNos + 1));

        recusado.IsFailure.Should().BeTrue();
        recusado.Errors.Select(static causa => causa.Error.Code).Should().Contain(DocumentoDaConfiguracaoErrorCodes.AcimaDoLimite);
    }

    private const string DocumentoComLocalidadeENomeAbreviado = """
        { "identificacao": { "unidadeAdministradoraCodigo": "CEPS", "localidadeCodigoIbge": "1504208" },
          "divulgacao": { "camposPublicos": [ "NOME_ABREVIADO" ] } }
        """;

    private Task<JsonObject> ExportarComLocalidadeENomeAbreviado() =>
        _cenario.Exportacao.ExportarAsync(
            _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps, DocumentoComLocalidadeENomeAbreviado), CancellationToken.None);

    [Fact(DisplayName = "A exportação traz o nome e a UF da localidade e a regra do nome abreviado")]
    public async Task Exportar_LocalidadeENomeAbreviado_AcrescentaOsDescritivos()
    {
        _cenario.Geo.ConferirMunicipioAsync("1504208", "PA", Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Marabá"));

        JsonObject documento = await ExportarComLocalidadeENomeAbreviado();

        documento["identificacao"]!["localidadeNome"]!.GetValue<string>().Should().Be("Marabá");
        documento["identificacao"]!["localidadeUf"]!.GetValue<string>().Should().Be("PA");
        documento["divulgacao"]!["regraNomeAbreviado"]!.GetValue<string>().Should().Be(RegrasDeNomeAbreviado.Vigente);
    }

    [Fact(DisplayName = "Com o Geo fora do ar a exportação segue, sem o nome e a UF da localidade e com a regra do nome abreviado")]
    public async Task Exportar_GeoForaDoAr_OmiteSoANomeEAUfDaLocalidade()
    {
        _cenario.Geo.ConferirMunicipioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel, "O Geo não respondeu.")));

        JsonObject documento = await ExportarComLocalidadeENomeAbreviado();

        documento["identificacao"]!["localidadeCodigoIbge"]!.GetValue<string>().Should().Be("1504208");
        documento["identificacao"]!["localidadeNome"].Should().BeNull();
        documento["identificacao"]!["localidadeUf"].Should().BeNull();
        documento["divulgacao"]!["regraNomeAbreviado"].Should().NotBeNull();
    }

    private const string DocumentoComFases = """
        { "identificacao": { "unidadeAdministradoraCodigo": "CEPS", "tipoProcessoCodigo": "SISU" },
          "cronograma": { "fases": [ { "unidadeResponsavelCodigo": "CGA", "chave": "a" },
            { "unidadeResponsavelCodigo": "CGA", "chave": "b" }, { "unidadeResponsavelCodigo": "AUSENTE" } ] } }
        """;

    [Fact(DisplayName = "Cada cadastro citado é consultado uma vez por código e o nome entra ao lado do código")]
    public async Task Exportar_CadastrosCitados_ConsultaCodigosDistintosEEscreveAoLado()
    {
        _cenario.Tipos.ObterNomesPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string> { ["SISU"] = "Seleção Exemplo" });
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps, DocumentoComFases);
        JsonObject documento = await _cenario.Exportacao.ExportarAsync(rascunho, CancellationToken.None);
        JsonArray fases = documento["cronograma"]!["fases"]!.AsArray();
        fases[0]!.AsObject().Select(static propriedade => propriedade.Key).Should().Equal(
            "unidadeResponsavelCodigo", "unidadeResponsavelNome", "unidadeResponsavelSigla", "chave");
        fases[1]!["unidadeResponsavelNome"]!.GetValue<string>().Should().Be(CenarioDoRascunho.Cga.Nome);
        fases[2]!["unidadeResponsavelNome"].Should().BeNull();
        await _cenario.Unidades.Received(1).ObterPorCodigoAsync("CGA", Arg.Any<CancellationToken>());
        await _cenario.Unidades.Received(1).ObterPorCodigoAsync("AUSENTE", Arg.Any<CancellationToken>());
        await _cenario.Tipos.Received(1).ObterNomesPorCodigosAsync(
            Arg.Is<IReadOnlyCollection<string>>(codigos => codigos.Count == 1 && codigos.Contains("SISU")), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O descritivo que o esquema não declara só de leitura não é escrito")]
    public async Task Exportar_AtributosNaoDeclaradosOuEditaveis_OmiteAtributos()
    {
        _cenario.Esquema.Forma.Returns(FormaSemDescritivoSomenteLeitura());
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps, DocumentoComFases);

        JsonObject documento = await _cenario.Exportacao.ExportarAsync(rascunho, CancellationToken.None);

        JsonObject fase = documento["cronograma"]!["fases"]!.AsArray()[0]!.AsObject();
        fase["unidadeResponsavelNome"].Should().BeNull();
        fase["unidadeResponsavelSigla"].Should().BeNull();
    }

    private static FormaDoNo FormaSemDescritivoSomenteLeitura() =>
        FormaDeTeste.Objeto(new()
        {
            ["cronograma"] = FormaDeTeste.Objeto(new()
            {
                ["fases"] = FormaDeTeste.Lista(FormaDeTeste.Objeto(new()
                {
                    ["unidadeResponsavelCodigo"] = FormaDeTeste.Valor(),
                    ["unidadeResponsavelNome"] = FormaDeTeste.Valor(),
                }))
            }),
        });

    [Fact]
    public async Task Exportar_ExigenciasComTiposCitados_ConsultaEmLoteEDescreveCadaUma()
    {
        TipoDocumentoView tipo = TipoDeDocumento("DIPLOMA", "Diploma", ClassificacaoDeProtecao.Pessoal);
        TipoDocumentoView outro = TipoDeDocumento("CERTIDAO", "Certidão", ClassificacaoDeProtecao.Sensivel);
        _cenario.TiposDeDocumento.ListarVivosPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { tipo, outro });
        JsonArray exigencias = await ExportarExigencias("""
            [{"tipoDocumentoCodigo":"DIPLOMA","chave":"a"},{"tipoDocumentoCodigo":"CERTIDAO"},{"tipoDocumentoCodigo":"DIPLOMA"}]
            """);

        ConferirDescricao(exigencias[0]!.AsObject(), tipo, "PESSOAL");
        ConferirDescricao(exigencias[1]!.AsObject(), outro, "SENSIVEL");
        ConferirDescricao(exigencias[2]!.AsObject(), tipo, "PESSOAL");
        await _cenario.TiposDeDocumento.Received(1).ListarVivosPorCodigosAsync(
            Arg.Is<IReadOnlyCollection<string>>(codigos => codigos.Count == 2 && codigos.Contains("DIPLOMA") && codigos.Contains("CERTIDAO")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Exportar_TipoSemCadastro_OmiteAtributos()
    {
        JsonArray exigencias = await ExportarExigencias("""[{"tipoDocumentoCodigo":"AUSENTE"}]""");

        exigencias[0]!.AsObject().Select(static propriedade => propriedade.Key).Should().Equal("tipoDocumentoCodigo");
        await _cenario.TiposDeDocumento.Received(1).ListarVivosPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Exportar_ExigenciaSemCodigo_NaoConsultaTipos()
    {
        await ExportarExigencias("""[{"chave":"a"}]""");

        await _cenario.TiposDeDocumento.DidNotReceive().ListarVivosPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    private async Task<JsonArray> ExportarExigencias(string exigencias)
    {
        string conteudo = $$"""{ "documentosExigidos": { "exigencias": {{exigencias}} } }""";
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps, conteudo);
        JsonObject documento = await _cenario.Exportacao.ExportarAsync(rascunho, CancellationToken.None);
        return documento["documentosExigidos"]!["exigencias"]!.AsArray();
    }

    private static TipoDocumentoView TipoDeDocumento(string codigo, string nome, ClassificacaoDeProtecao classificacao) =>
        new(Guid.CreateVersion7(), codigo, nome, "ACADEMICO", classificacao, "Análise da inscrição", "OBRIGACAO_LEGAL", true);

    private static void ConferirDescricao(JsonObject exigencia, TipoDocumentoView tipo, string classificacao)
    {
        exigencia.Select(static propriedade => propriedade.Key).Take(6).Should().Equal(
            "tipoDocumentoCodigo", "tipoDocumentoNome", "tipoDocumentoCategoria", "classificacaoProtecao", "finalidadeTratamento", "hipoteseLegal");
        exigencia["tipoDocumentoNome"]!.GetValue<string>().Should().Be(tipo.Nome);
        exigencia["tipoDocumentoCategoria"]!.GetValue<string>().Should().Be(tipo.Categoria);
        exigencia["classificacaoProtecao"]!.GetValue<string>().Should().Be(classificacao);
        exigencia["finalidadeTratamento"]!.GetValue<string>().Should().Be(tipo.FinalidadeTratamento);
        exigencia["hipoteseLegal"]!.GetValue<string>().Should().Be(tipo.HipoteseLegal);
        exigencia["fatosComprovados"].Should().BeNull();
    }
}
