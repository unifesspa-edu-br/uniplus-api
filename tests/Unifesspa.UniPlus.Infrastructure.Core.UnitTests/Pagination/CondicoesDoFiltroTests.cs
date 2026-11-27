namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// A chave que o cursor leva só serve à consulta que a emitiu: trocar uma condição do recorte, a posição dela
/// ou o instante congelado recusa a continuação, e nada do texto digitado viaja na chave.
/// </summary>
public sealed class CondicoesDoFiltroTests
{
    private static readonly DateTimeOffset Instante = new(2026, 3, 10, 14, 30, 0, TimeSpan.FromHours(-3));

    [Fact(DisplayName = "A chave emitida é aceita pelas mesmas condições")]
    public void ConferirChave_MesmasCondicoes_Aceita()
    {
        CondicoesDoFiltro condicoes = CondicoesDoFiltro.De(ParteDoFiltro.De("engenharia"), ParteDoFiltro.De(true));

        Action conferir = () => condicoes.ConferirChave(condicoes.EmitirChave());

        conferir.Should().NotThrow();
    }

    [Fact(DisplayName = "A listagem sem filtro aceita a própria chave")]
    public void ConferirChave_Nenhuma_AceitaAPropriaChave()
    {
        Action conferir = () => CondicoesDoFiltro.Nenhuma.ConferirChave(CondicoesDoFiltro.Nenhuma.EmitirChave());

        conferir.Should().NotThrow();
    }

    [Fact(DisplayName = "A chave de uma listagem filtrada não serve à listagem sem filtro, nem o contrário")]
    public void ConferirChave_NenhumaContraFiltrada_Recusa()
    {
        CondicoesDoFiltro filtrada = CondicoesDoFiltro.De(ParteDoFiltro.De("a"));

        RecusaDaChave(filtrada, CondicoesDoFiltro.Nenhuma.EmitirChave());
        RecusaDaChave(CondicoesDoFiltro.Nenhuma, filtrada.EmitirChave());
    }

    [Fact(DisplayName = "Trocar o texto da busca recusa a continuação")]
    public void ConferirChave_BuscaTrocada_Recusa()
    {
        string chave = CondicoesDoFiltro.De(ParteDoFiltro.De("engenharia")).EmitirChave();

        RecusaDaChave(CondicoesDoFiltro.De(ParteDoFiltro.De("medicina")), chave);
    }

    [Fact(DisplayName = "Filtro ausente é diferente de filtro com texto vazio")]
    public void ConferirChave_AusenteContraVazio_Recusa()
    {
        string chave = CondicoesDoFiltro.De(ParteDoFiltro.Ausente).EmitirChave();

        RecusaDaChave(CondicoesDoFiltro.De(ParteDoFiltro.De(string.Empty)), chave);
    }

    [Fact(DisplayName = "O mesmo valor em outra posição do filtro recusa")]
    public void ConferirChave_PosicaoTrocada_Recusa()
    {
        string chave = CondicoesDoFiltro.De(ParteDoFiltro.De("a"), ParteDoFiltro.Ausente).EmitirChave();

        RecusaDaChave(CondicoesDoFiltro.De(ParteDoFiltro.Ausente, ParteDoFiltro.De("a")), chave);
    }

    [Fact(DisplayName = "Conjunto reordenado ou com repetição é o mesmo recorte")]
    public void ConferirChave_ConjuntoReordenadoERepetido_Aceita()
    {
        string chave = CondicoesDoFiltro.De(ParteDoFiltro.Conjunto(["b", "a"])).EmitirChave();

        Action conferir = () => CondicoesDoFiltro.De(ParteDoFiltro.Conjunto(["a", "b", "a"])).ConferirChave(chave);

        conferir.Should().NotThrow();
    }

    [Fact(DisplayName = "Conjunto com outro membro recusa, e o conjunto vazio difere do filtro ausente")]
    public void ConferirChave_ConjuntoDiferente_Recusa()
    {
        string chave = CondicoesDoFiltro.De(ParteDoFiltro.Conjunto(["a", "b"])).EmitirChave();

        RecusaDaChave(CondicoesDoFiltro.De(ParteDoFiltro.Conjunto(["a"])), chave);
        RecusaDaChave(CondicoesDoFiltro.De(ParteDoFiltro.Conjunto(Array.Empty<string>())), CondicoesDoFiltro.De(ParteDoFiltro.Ausente).EmitirChave());
    }

    [Fact(DisplayName = "Verdadeiro, falso e ausente são três recortes diferentes")]
    public void ConferirChave_Booleano_TresRecortes()
    {
        string verdadeiro = CondicoesDoFiltro.De(ParteDoFiltro.De(true)).EmitirChave();

        RecusaDaChave(CondicoesDoFiltro.De(ParteDoFiltro.De(false)), verdadeiro);
        RecusaDaChave(CondicoesDoFiltro.De(ParteDoFiltro.De((bool?)null)), verdadeiro);
    }

    [Theory(DisplayName = "Chave ausente, vazia ou que não é chave recusa")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nao-e-chave")]
    public void ConferirChave_ChaveInvalida_Recusa(string? chave) =>
        RecusaDaChave(CondicoesDoFiltro.Nenhuma, chave);

    [Fact(DisplayName = "O texto da busca não aparece na chave, que carrega só um resumo de 43 caracteres")]
    public void EmitirChave_NaoExpoeOTextoDigitado()
    {
        const string textoDigitado = "texto muito particular da busca";

        string chave = CondicoesDoFiltro.De(ParteDoFiltro.De(textoDigitado)).EmitirChave();

        chave.Should().NotContain("particular");
        ChaveDeContinuacao.TentarLer(chave, colunas: 0, out ChaveDeContinuacao.LeituraDaChave leitura).Should().BeTrue();
        leitura.Resumo.Should().HaveLength(43);
    }

    [Fact(DisplayName = "As mesmas condições emitem sempre a mesma chave")]
    public void EmitirChave_EDeterministica()
    {
        CondicoesDoFiltro primeira = CondicoesDoFiltro.De(ParteDoFiltro.De("a"), ParteDoFiltro.De(Guid.Parse("11111111-1111-1111-1111-111111111111")));
        CondicoesDoFiltro segunda = CondicoesDoFiltro.De(ParteDoFiltro.De("a"), ParteDoFiltro.De(Guid.Parse("11111111-1111-1111-1111-111111111111")));

        primeira.EmitirChave().Should().Be(segunda.EmitirChave());
    }

    [Fact(DisplayName = "O instante congelado volta da chave, no mesmo instante")]
    public void InstanteCongelado_IdaEVolta()
    {
        string chave = CondicoesDoFiltro.Nenhuma.CongeladasEm(Instante).EmitirChave();

        ChaveDeContinuacao.InstanteCongelado(chave, colunas: 0).Should().Be(Instante);
    }

    [Fact(DisplayName = "Sem instante congelado, a chave não devolve instante")]
    public void InstanteCongelado_ChaveSemInstante_Nulo()
    {
        string chave = CondicoesDoFiltro.Nenhuma.EmitirChave();

        ChaveDeContinuacao.InstanteCongelado(chave, colunas: 0).Should().BeNull();
    }

    [Fact(DisplayName = "Continuar com outro instante congelado recusa")]
    public void ConferirChave_InstanteDiferente_Recusa()
    {
        string chave = CondicoesDoFiltro.Nenhuma.CongeladasEm(Instante).EmitirChave();

        RecusaDaChave(CondicoesDoFiltro.Nenhuma.CongeladasEm(Instante.AddSeconds(1)), chave);
        RecusaDaChave(CondicoesDoFiltro.Nenhuma, chave);
    }

    [Fact(DisplayName = "O instante do cursor escreve em UTC e lê de volta")]
    public void InstanteDoCursor_IdaEVolta()
    {
        string texto = InstanteDoCursor.Escrever(Instante);

        texto.Should().Be("2026-03-10T17:30:00.0000000+00:00");
        InstanteDoCursor.Ler(texto).Should().Be(Instante);
    }

    [Theory(DisplayName = "Texto que não é instante do cursor recusa a leitura")]
    [InlineData("")]
    [InlineData("2026-03-10")]
    [InlineData("ontem")]
    public void InstanteDoCursor_TextoInvalido_Recusa(string texto) =>
        FluentActions.Invoking(() => InstanteDoCursor.Ler(texto)).Should().Throw<CursorAnchorMismatchException>();

    private static void RecusaDaChave(CondicoesDoFiltro condicoes, string? chave) =>
        FluentActions.Invoking(() => condicoes.ConferirChave(chave)).Should().Throw<CursorAnchorMismatchException>();
}
