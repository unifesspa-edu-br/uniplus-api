namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Rota que só existe no host de teste: devolve uma resposta que não passou pelo projetor, para provar a defesa do
/// serializador.
/// </summary>
[ApiController]
[Route("_teste/processos-seletivos/{id:guid}/inscricoes/{inscricaoId:guid}")]
[Authorize]
[ApiExplorerSettings(IgnoreApi = true)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class RespostaSemProjetorDeTesteController : ControllerBase
{
    /// <summary>
    /// A resposta de um handler que esqueceu o projetor: monta o DTO inteiro, sem decidir nada. A defesa do serializador
    /// tem de omitir o que é classificado.
    /// </summary>
    [HttpGet("esquecida")]
    public IActionResult ObterSemProjetor(Guid id, Guid inscricaoId) =>
        Ok(new InscricaoDoProcessoDto(
            inscricaoId, "000001", SituacaoInscricao.Submetida, Guid.CreateVersion7(), null, "Ariel Drumond",
            new CpfMascaradoDto("***.598.363-**", PodeRevelar: false), DateTimeOffset.UtcNow));
}
