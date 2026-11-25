namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

public sealed class ObterRascunhoDeProcessoSeletivoQueryHandlerTests
{
    private readonly CenarioDoRascunho _cenario = new();

    private Task<Result<RascunhoExportadoDto>> Obter(Guid id) =>
        ObterRascunhoDeProcessoSeletivoQueryHandler.Handle(
            new ObterRascunhoDeProcessoSeletivoQuery(id), _cenario.Rascunhos, _cenario.Verificador, _cenario.Exportacao, CancellationToken.None);

    private static string DocumentoComTipo(string codigoDoTipo) =>
        $$"""{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1, "identificacao": { "nome": "Processo", "unidadeAdministradoraCodigo": "CEPS", "tipoProcessoCodigo": "{{codigoDoTipo}}" } }""";

    [Fact(DisplayName = "A exportação traz o id do rascunho na raiz, o ETag da revisão e os descritivos da unidade")]
    public async Task Exportacao_IdEDescritivosDaUnidade()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps);

        Result<RascunhoExportadoDto> resultado = await Obter(rascunho.Id);

        JsonObject documento = resultado.Value!.Documento;
        documento["id"]!.GetValue<string>().Should().Be(rascunho.Id.ToString());
        documento["identificacao"]!["unidadeAdministradoraNome"]!.GetValue<string>().Should().Be(CenarioDoRascunho.Ceps.Nome);
        documento["identificacao"]!["unidadeAdministradoraSigla"]!.GetValue<string>().Should().Be(CenarioDoRascunho.Ceps.Sigla);
        documento["identificacao"]!["unidadeAdministradoraCodigo"]!.GetValue<string>().Should().Be("CEPS");
        resultado.Value.ETag.Should().Be(rascunho.ETag);
    }

    [Fact(DisplayName = "A exportação não traz versão nem publicadoEm: o rascunho não foi publicado")]
    public async Task Exportacao_SemVersaoNemPublicadoEm()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps);

        JsonObject documento = (await Obter(rascunho.Id)).Value!.Documento;

        documento.ContainsKey("versao").Should().BeFalse();
        documento.ContainsKey("publicadoEm").Should().BeFalse();
    }

    [Fact(DisplayName = "O id que o documento guardado traga é substituído pelo do rascunho")]
    public async Task IdDoDocumento_ESubstituido()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(
            CenarioDoRascunho.Ceps, """{ "id": "de-outro-processo", "identificacao": { "unidadeAdministradoraCodigo": "CEPS" } }""");

        JsonObject documento = (await Obter(rascunho.Id)).Value!.Documento;

        documento["id"]!.GetValue<string>().Should().Be(rascunho.Id.ToString());
    }

    [Fact(DisplayName = "O nome do tipo de processo entra quando o documento cita o código e o cadastro o resolve")]
    public async Task TipoDeProcesso_NomeQuandoResolve()
    {
        _cenario.Tipos.ObterNomesPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string> { ["SISU"] = "Sistema de Seleção Unificada" });
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps, DocumentoComTipo("SISU"));

        JsonObject documento = (await Obter(rascunho.Id)).Value!.Documento;

        documento["identificacao"]!["tipoProcessoNome"]!.GetValue<string>().Should().Be("Sistema de Seleção Unificada");
    }

    [Fact(DisplayName = "O descritivo cujo cadastro não resolve é omitido, sem falhar")]
    public async Task DescritivoSemCadastro_EOmitido()
    {
        _cenario.Unidades.ObterPorIdAsync(CenarioDoRascunho.Ceps.Id, Arg.Any<CancellationToken>()).Returns((UnidadeView?)null);
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps, DocumentoComTipo("EXTINTO"));

        Result<RascunhoExportadoDto> resultado = await Obter(rascunho.Id);

        resultado.IsSuccess.Should().BeTrue();
        JsonObject identificacao = resultado.Value!.Documento["identificacao"]!.AsObject();
        identificacao.ContainsKey("unidadeAdministradoraNome").Should().BeFalse();
        identificacao.ContainsKey("unidadeAdministradoraSigla").Should().BeFalse();
        identificacao.ContainsKey("tipoProcessoNome").Should().BeFalse();
    }

    [Fact(DisplayName = "O documento sem tipo de processo não consulta o cadastro de tipos")]
    public async Task SemTipo_NaoConsultaOCadastro()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps);

        await Obter(rascunho.Id);

        await _cenario.Tipos.DidNotReceiveWithAnyArgs().ObterNomesPorCodigosAsync(default!, default);
    }

    [Fact(DisplayName = "O rascunho que não existe recusa 404 como o processo que não existe")]
    public async Task Inexistente_NaoEncontrado()
    {
        Result<RascunhoExportadoDto> resultado = await Obter(Guid.CreateVersion7());

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "A unidade que a linha tem agora, onde a pessoa não configura, recusa 404 e não exporta o documento")]
    public async Task UnidadeAtualSemPermissao_404SemExportar()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Cga);

        Result<RascunhoExportadoDto> resultado = await Obter(rascunho.Id);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await _cenario.Unidades.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
    }

    [Fact(DisplayName = "A unidade atual onde a pessoa configura entrega o documento, ainda que não seja a da autorização da rota")]
    public async Task UnidadeAtualComPermissao_Exporta()
    {
        _cenario.PodeConfigurarEm(CenarioDoRascunho.Cga);
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Cga);

        Result<RascunhoExportadoDto> resultado = await Obter(rascunho.Id);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ETag.Should().Be(rascunho.ETag);
    }

    [Fact(DisplayName = "Os argumentos nulos são recusados")]
    public async Task ArgumentosNulos_Lancam()
    {
        Func<Task> semConsulta = () => ObterRascunhoDeProcessoSeletivoQueryHandler.Handle(
            null!, _cenario.Rascunhos, _cenario.Verificador, _cenario.Exportacao, CancellationToken.None);

        await semConsulta.Should().ThrowAsync<ArgumentNullException>();
    }
}
