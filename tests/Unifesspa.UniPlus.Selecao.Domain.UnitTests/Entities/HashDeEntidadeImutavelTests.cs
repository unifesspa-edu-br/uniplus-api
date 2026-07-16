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
    [Fact(DisplayName = "HashDeObrigatoriedadeLegal_PermaneceEstavelComAChaveTipoProcessoCodigo")]
    public void HashDeObrigatoriedadeLegal_PermaneceEstavelComAChaveTipoProcessoCodigo()
    {
        Result<ObrigatoriedadeLegal> regra = ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: ObrigatoriedadeLegal.TipoProcessoUniversal,
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "ETAPA_OBRIGATORIA",
            predicado: new EtapaObrigatoria("ProvaObjetiva"),
            descricaoHumana: "Edital deve incluir etapa de Prova Objetiva.",
            baseLegal: "Lei 12.711/2012 art.1º",
            vigenciaInicio: new DateOnly(2026, 1, 1));

        regra.IsSuccess.Should().BeTrue();
        regra.Value!.Hash.Should().Be(
            "5ca48e8d4d74dd61cd2ff1b0e76799b08a68836d63d8595d4fef21e8d4c11c23",
            "a chave tipoProcessoCodigo integra o payload canônico; qualquer mudança posterior exige nova decisão explícita.");
    }
}
