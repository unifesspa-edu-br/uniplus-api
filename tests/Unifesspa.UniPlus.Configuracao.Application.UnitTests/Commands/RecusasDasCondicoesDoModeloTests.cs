namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.CenarioDoHandlerDoModelo;

public sealed class RecusasDasCondicoesDoModeloTests
{
    private readonly CenarioDoHandlerDoModelo _cenario = new();

    [Fact]
    public async Task Criar_ExibicaoComDuasCondicoesInvalidas_RecusaCadaPosicao()
    {
        ConteudoDoModeloInput conteudo = ComComposicao(Campo("MAIOR_IDADE", 0));
        GrupoColetadoInput grupo = conteudo.Grupos![0];
        CondicaoPrecondicaoInput[] condicoes =
        [
            new("", "IGUAL", JsonSerializer.SerializeToElement(true)),
            new("MAIOR_IDADE", "XPTO", JsonSerializer.SerializeToElement(true)),
        ];
        conteudo = conteudo with { Grupos = [grupo with { Regras = grupo.Regras! with { Exibicao = [condicoes] } }] };

        Result<Guid> resultado = await _cenario.CriarAsync(conteudo);

        resultado.Errors.Select(static e => e.Field).Should().Contain(
            ["conteudo.grupos[0].regras.exibicao[0][0].fato", "conteudo.grupos[0].regras.exibicao[0][1].operador"]);
    }
}
