namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>
/// Contraprova do conteúdo canônico da ObrigatoriedadeLegal.
/// </summary>
/// <remarks>
/// <para>
/// A chave <c>tipoProcessoCodigo</c> é conteúdo semântico do hash. Este valor
/// literal é atualizado deliberadamente pela ADR-0114, que renomeia a chave;
/// mudanças futuras sem nova decisão precisam falhar aqui.
/// </para>
/// <para>
/// O valor abaixo é literal de propósito: um hash que muda sozinho tem de fazer
/// um teste falhar.
/// </para>
/// </remarks>
public sealed class HashDeEntidadeImutavelTests
{
    [Theory]
    [InlineData(CategoriaObrigatoriedade.Etapa, "b991812ae8348a03aea342d1c6f6ec821856fb9c45bda674d2e575c19e74eda4")]
    [InlineData(CategoriaObrigatoriedade.Bonus, "20aaca31ebbb2469750b13afedea44ffb17dcc2133e49275583885580e54c529")]
    public void HashDeObrigatoriedadeLegal_CategoriaConhecida_PreservaHash(CategoriaObrigatoriedade categoria, string esperado)
    {
        Result<ObrigatoriedadeLegal> regra = ObrigatoriedadeLegal.Criar(
            ObrigatoriedadeLegal.TipoProcessoUniversal,
            categoria,
            "ETAPA_OBRIGATORIA",
            new EtapaObrigatoria("ProvaObjetiva"),
            new TextoNormativo("Edital deve incluir etapa de Prova Objetiva.", "Lei 12.711/2012 art.1º", null, null),
            new DateOnly(2026, 1, 1),
            null);

        regra.IsSuccess.Should().BeTrue();
        regra.Value!.Hash.Should().Be(
            esperado,
            "a chave tipoProcessoCodigo integra o payload canônico; qualquer mudança posterior exige nova decisão explícita.");
    }
}
