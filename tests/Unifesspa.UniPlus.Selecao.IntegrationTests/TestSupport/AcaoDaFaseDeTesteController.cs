namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Rotas que só existem no host de teste: exercem a decisão da ação da fase com o verificador e a política de registro
/// reais do host, sem nenhuma operação de negócio no meio.
/// </summary>
[ApiController]
[Route("_teste/processos-seletivos/{id:guid}/fases/{faseId:guid}")]
[Authorize]
[ApiExplorerSettings(IgnoreApi = true)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class AcaoDaFaseDeTesteController : ControllerBase
{
    private readonly IVerificadorDeAcesso _verificador;
    private readonly IUnidadeDaFaseNaVersaoVigente _fases;

    public AcaoDaFaseDeTesteController(IVerificadorDeAcesso verificador, IUnidadeDaFaseNaVersaoVigente fases)
    {
        _verificador = verificador;
        _fases = fases;
    }

    /// <summary>Exige a ação da fase: decide e registra como a operação de negócio faria.</summary>
    [HttpGet("acao")]
    public async Task<IActionResult> Exigir(Guid id, Guid faseId, CancellationToken cancellationToken) =>
        Ok(new
        {
            resultado = (await AcessoAoProcesso.ExigirNaFaseAsync(
            _verificador, _fases, id, faseId, PermissaoSelecao.DocumentosDaFasePublicar, cancellationToken)).ToString()
        });

    /// <summary>Pergunta a capacidade da ação da fase, sem registrar.</summary>
    [HttpGet("capacidade")]
    public async Task<IActionResult> Perguntar(Guid id, Guid faseId, CancellationToken cancellationToken) =>
        Ok(new
        {
            resultado = (await AcessoAoProcesso.PodeNaFaseAsync(
            _verificador, _fases, id, faseId, PermissaoSelecao.DocumentosDaFasePublicar, cancellationToken)).ToString()
        });
}
