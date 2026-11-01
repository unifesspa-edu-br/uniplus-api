namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// A rota do detalhe da inscrição pertence à issue que expõe a consulta administrativa; até lá, o teste exercita a
/// leitura projetada, o serializador e o registro por esta rota, que só existe no host de teste.
/// </summary>
[ApiController]
[Route("_teste/processos-seletivos/{id:guid}/inscricoes/{inscricaoId:guid}")]
[Authorize]
[ApiExplorerSettings(IgnoreApi = true)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class InscricaoAdministrativaDeTesteController : ControllerBase
{
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public InscricaoAdministrativaDeTesteController(IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _queryBus = queryBus;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<IActionResult> Obter(Guid id, Guid inscricaoId, CancellationToken cancellationToken)
    {
        Result<InscricaoAdministrativaDto> resultado = await _queryBus
            .Send(new ObterInscricaoAdministrativaQuery(id, inscricaoId), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

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
