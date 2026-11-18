namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

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
        _cenario.Tipos.ObterAtivoPorCodigoAsync("SISU", Arg.Any<CancellationToken>()).Returns(
            new TipoProcessoView(Guid.CreateVersion7(), "SISU", "Sistema de Seleção Unificada", null));
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
}
