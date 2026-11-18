namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Errors;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class RecusaDeColisaoNoQuadroDeVagasTests
{
    [Fact]
    public void Calcular_CodigoDeModalidadeFederalEmRetirada_RetornaProblemaPublicado()
    {
        ReferenciaReservaDemograficaSnapshot demografica = ReferenciaReservaDemograficaSnapshot
            .Criar(Guid.CreateVersion7(), "2022", 50, 10, 10, "Censo 2022").Value!;
        Result<QuadroDeVagasDaLeiDeCotas> resultado = CalculadoraQuadroVagasLei12711.Calcular(40, 0.5m, demografica,
            new Dictionary<string, int> { [ModalidadesFederaisLei12711.Ac] = 2 }, new Dictionary<string, int>());

        resultado.IsFailure.Should().BeTrue();
        ObjectResult resposta = (ObjectResult)resultado.ToActionResult(MapperDoModuloDeTeste.Criar());
        ProblemDetails problema = (ProblemDetails)resposta.Value!;
        resposta.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problema.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problema.Extensions["code"].Should().Be("uniplus.selecao.configuracao_distribuicao_vagas.quadro_chave_colide");
        problema.Type.Should().Be(MapperDoModuloDeTeste.BaseUriDeErros + "uniplus.selecao.configuracao_distribuicao_vagas.quadro_chave_colide");
    }
}
