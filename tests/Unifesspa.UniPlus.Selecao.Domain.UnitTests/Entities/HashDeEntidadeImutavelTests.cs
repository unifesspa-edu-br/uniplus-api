namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>
/// Contraprova da emenda ao item 4 da ADR-0100 (ADR-0109 D4).
/// </summary>
/// <remarks>
/// <para>
/// A ADR-0109 declara que o envelope do congelamento <b>preserva</b> <c>null</c>
/// explícito, e delimita o item 4 da ADR-0100 ("campo opcional é omitido") ao
/// caminho de hash de <b>entidade</b> — <c>CanonicalOptions</c>.
/// </para>
/// <para>
/// Esta é a prova de que a emenda ficou <b>restrita</b>: o hash de uma
/// <c>ObrigatoriedadeLegal</c> de referência não muda. Se mudasse, a
/// <c>UNIQUE (hash)</c> parcial e a trilha forense do catálogo quebrariam — e é
/// exatamente por isso que a decisão foi emendar a ADR, e não "corrigir" o
/// canonicalizador para omitir <c>null</c> em toda parte.
/// </para>
/// <para>
/// O valor abaixo é literal de propósito: um hash que muda sozinho tem de fazer
/// um teste falhar.
/// </para>
/// </remarks>
public sealed class HashDeEntidadeImutavelTests
{
    [Fact(DisplayName = "HashDeObrigatoriedadeLegal_NaoMudou — CanonicalOptions permanece byte-idêntico (ADR-0109 D4)")]
    public void HashDeObrigatoriedadeLegal_NaoMudou()
    {
        Result<ObrigatoriedadeLegal> regra = ObrigatoriedadeLegal.Criar(
            tipoEditalCodigo: ObrigatoriedadeLegal.TipoEditalUniversal,
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "ETAPA_OBRIGATORIA",
            predicado: new EtapaObrigatoria("ProvaObjetiva"),
            descricaoHumana: "Edital deve incluir etapa de Prova Objetiva.",
            baseLegal: "Lei 12.711/2012 art.1º",
            vigenciaInicio: new DateOnly(2026, 1, 1));

        regra.IsSuccess.Should().BeTrue();

        regra.Value!.Hash.Should().Be(
            "60add4b6533261d9747a3a9e46cff9999a4c04df3c8aade3ad6f824fdf15a442",
            "o hash de entidade é computado por CanonicalOptions, que a emenda ao item 4 da ADR-0100 NÃO tocou. " +
            "Se este valor mudou, a trilha forense do catálogo de obrigatoriedades legais foi quebrada — " +
            "e a UNIQUE (hash) parcial deixou de valer para as linhas já gravadas.");
    }
}
