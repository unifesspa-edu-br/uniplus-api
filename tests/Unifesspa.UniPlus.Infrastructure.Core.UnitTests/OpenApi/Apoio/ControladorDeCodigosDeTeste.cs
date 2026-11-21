namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi.Apoio;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi.Apoio.ModeloDeCodigosDeTeste;

/// <summary>
/// As ações que o ApiExplorer descreve para o contrato dos códigos, com o atributo no parâmetro da ação e na propriedade
/// de um tipo de consulta. Só o documento de nome <see cref="Documento"/> as inclui.
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = Documento)]
[Route("api/fases")]
public sealed class ControladorDeCodigosDeTeste : ControllerBase
{
    public const string Documento = "codigos-dos-controladores";

    [HttpGet("direto")]
    public RespostaDeFase ComParametroDireto(
        [FromQuery, AceitaCodigosDe<Fase>] string? fase,
        [FromQuery] string? livre,
        [FromQuery, AceitaCodigosDe<Fase>] IReadOnlyList<string>? fases,
        [FromQuery, BindRequired, AceitaCodigosDe<Fase>] string faseObrigatoria) =>
        throw new NotSupportedException();

    [HttpGet("textual")]
    public RespostaTextualDeFase ComRespostaTextual() => throw new NotSupportedException();

    [HttpGet("filtro")]
    public RespostaDeFase ComTipoDeConsulta([FromQuery] FiltroDeFase filtro) => throw new NotSupportedException();

    [HttpPost]
    public RespostaDeFase Criar([FromBody] PedidoDeFase pedido) => throw new NotSupportedException();

    [HttpPost("posicional")]
    public RespostaDeFase CriarPosicional([FromBody] PedidoPosicional pedido) => throw new NotSupportedException();

    [HttpPost("construtor-marcado")]
    public RespostaDeFase CriarComConstrutorMarcado([FromBody] PedidoComConstrutorMarcado pedido) => throw new NotSupportedException();

    [HttpPost("outra-sobrecarga-marcada")]
    public RespostaDeFase CriarComOutraSobrecargaMarcada([FromBody] PedidoComOutraSobrecargaMarcada pedido) => throw new NotSupportedException();
}
