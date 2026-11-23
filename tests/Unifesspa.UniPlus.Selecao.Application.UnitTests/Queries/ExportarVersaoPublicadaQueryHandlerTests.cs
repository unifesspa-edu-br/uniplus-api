namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

public sealed class ExportarVersaoPublicadaQueryHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);
    private readonly Guid _processoId = Guid.CreateVersion7();
    private readonly IProcessoSeletivoRepository _repositorio = Substitute.For<IProcessoSeletivoRepository>();
    private readonly IProjecaoDaVersaoPublicada _projecao = Substitute.For<IProjecaoDaVersaoPublicada>();

    [Fact]
    public async Task Handle_VersaoVigente_UsaRelogioEProjetaDocumento()
    {
        VersaoConfiguracao versao = NovaVersao();
        _repositorio.ObterVersaoVigenteAsync(_processoId, Agora, Arg.Any<CancellationToken>()).Returns(versao);
        _projecao.Projetar(Arg.Any<VersaoPublicada>()).Returns(new JsonObject { ["teste"] = true });
        Result<VersaoPublicadaExportadaDto> resultado = await ConsultarAsync(null);
        resultado.Value!.ETag.Should().Be($"\"{versao.HashConfiguracao}\"");
        resultado.Value.Documento["teste"]!.GetValue<bool>().Should().BeTrue();
        _projecao.Received(1).Projetar(Arg.Is<VersaoPublicada>(v => v.ProcessoSeletivoId == _processoId && v.Numero == 1 && v.PublicadaEm == Agora && v.Conteudo == versao.ConfiguracaoCongelada));
    }

    [Fact]
    public async Task Handle_NumeroInformado_ConsultaVersaoSolicitada()
    {
        _repositorio.ObterVersaoPorNumeroAsync(_processoId, 1, Arg.Any<CancellationToken>()).Returns(NovaVersao());
        _projecao.Projetar(Arg.Any<VersaoPublicada>()).Returns(new JsonObject());
        Result<VersaoPublicadaExportadaDto> resultado = await ConsultarAsync(1);
        resultado.IsSuccess.Should().BeTrue();
        await _repositorio.DidNotReceive().ObterVersaoVigenteAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(2, true, "ProcessoSeletivo.VersaoNaoEncontrada")]
    [InlineData(2, false, "ProcessoSeletivo.NaoEncontrado")]
    [InlineData(null, true, "Snapshot.VigenteAusente")]
    [InlineData(null, false, "ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_SemVersao_RetornaErroCorrespondente(int? numero, bool existe, string codigo)
    {
        _repositorio.ExisteAsync(_processoId, Arg.Any<CancellationToken>()).Returns(existe);
        Result<VersaoPublicadaExportadaDto> resultado = await ConsultarAsync(numero);
        resultado.Error!.Code.Should().Be(codigo);
        _projecao.DidNotReceive().Projetar(Arg.Any<VersaoPublicada>());
    }

    private Task<Result<VersaoPublicadaExportadaDto>> ConsultarAsync(int? numero) =>
        ExportarVersaoPublicadaQueryHandler.Handle(new(_processoId, numero), _repositorio, _projecao, new RelogioFixo(), CancellationToken.None);

    private VersaoConfiguracao NovaVersao() => VersaoConfiguracao.Abrir(
        _processoId, new DocumentoCongelado(Encoding.UTF8.GetBytes("{}"), "1.0", "canonical-json/sha256@v1", new string('a', 64)),
        Guid.CreateVersion7(), "conta-teste", Agora);

    private sealed class RelogioFixo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Agora;
    }
}
