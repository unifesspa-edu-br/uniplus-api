namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain;
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
    [InlineData(CategoriaObrigatoriedade.Etapa, "a841824e76f413d391f840a922d2dc58c9fdc68f5a5b475289623426008819bc")]
    [InlineData(CategoriaObrigatoriedade.Bonus, "a7f82467902eb2d935931af38eabb08900c144e3764f584411510d6bf4218fa7")]
    public void HashDeObrigatoriedadeLegal_CategoriaConhecida_PreservaHash(CategoriaObrigatoriedade categoria, string esperado)
    {
        Result<ObrigatoriedadeLegal> regra = ObrigatoriedadeLegal.Criar(
            TodosOsTiposDeProcesso.Codigo,
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
