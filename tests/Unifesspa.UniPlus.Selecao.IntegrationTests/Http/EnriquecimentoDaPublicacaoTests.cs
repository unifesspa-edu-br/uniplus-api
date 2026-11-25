namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Http;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Http;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A lista de obrigatoriedades reprovadas acompanha a recusa de conformidade legal onde quer que ela esteja entre as
/// causas que a resposta acumula, e só dela.
/// </summary>
public sealed class EnriquecimentoDaPublicacaoTests
{
    private const string ConformidadeLegal = "ProcessoSeletivo.ConformidadeLegalInsuficiente";
    private static readonly Guid ProcessoId = Guid.CreateVersion7();

    [Fact(DisplayName = "A conformidade legal recusada depois de outras causas traz as obrigatoriedades reprovadas")]
    public async Task ConformidadeLegalForaDaRaiz_TrazAsObrigatoriedadesReprovadas()
    {
        IQueryBus queryBus = QueryBusComUmaRegraReprovada();
        Result resultado = Result.ValidationFailure(
            [new FieldError("etapas", new DomainError("ProcessoSeletivo.AvaliacaoSemEtapa", "O processo não tem etapa.")), new FieldError(null, new DomainError(ConformidadeLegal, "Reprovado."))]);
        ObjectResult resposta = Resposta422();

        await EnriquecimentoDaPublicacao.EnriquecerComObrigatoriedadesReprovadasAsync(
            queryBus, resultado, resposta, ProcessoId, null, CancellationToken.None);

        ((ProblemDetails)resposta.Value!).Extensions.Should().ContainKey("obrigatoriedadesReprovadas");
    }

    [Fact(DisplayName = "A recusa sem conformidade legal não consulta nem traz as obrigatoriedades reprovadas")]
    public async Task SemConformidadeLegal_NaoTrazAsObrigatoriedadesReprovadas()
    {
        IQueryBus queryBus = QueryBusComUmaRegraReprovada();
        Result resultado = Result.Failure(new DomainError("ProcessoSeletivo.AvaliacaoSemEtapa", "O processo não tem etapa."));
        ObjectResult resposta = Resposta422();

        await EnriquecimentoDaPublicacao.EnriquecerComObrigatoriedadesReprovadasAsync(
            queryBus, resultado, resposta, ProcessoId, null, CancellationToken.None);

        ((ProblemDetails)resposta.Value!).Extensions.Should().NotContainKey("obrigatoriedadesReprovadas");
        queryBus.ReceivedCalls().Should().BeEmpty();
    }

    private static ObjectResult Resposta422() =>
        new(new ProblemDetails { Status = StatusCodes.Status422UnprocessableEntity }) { StatusCode = StatusCodes.Status422UnprocessableEntity };

    private static IQueryBus QueryBusComUmaRegraReprovada()
    {
        RegraAvaliadaDto reprovada = new(
            Guid.CreateVersion7(), "REGRA_REPROVADA", CodigoComRotulo<CategoriaObrigatoriedade>.De(CategoriaObrigatoriedade.Outros), "UNIVERSAL", new EtapaObrigatoria("ETAPA_NAO_OFERTADA"),
            Aprovada: false, Motivo: "A etapa não é ofertada.", BaseLegal: "Lei de teste", AtoNormativoUrl: null, PortariaInterna: null,
            DescricaoHumana: "Oferta da etapa", VigenciaInicio: new DateOnly(2020, 1, 1), VigenciaFim: null, Hash: "hash");
        IQueryBus queryBus = Substitute.For<IQueryBus>();
        queryBus.Send(Arg.Any<ObterConformidadeLegalProcessoSeletivoQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ConformidadeLegalProcessoSeletivoDto>.Success(
                new ConformidadeLegalProcessoSeletivoDto(ProcessoId, new DateOnly(2026, 1, 1), [reprovada], [])));

        return queryBus;
    }
}
