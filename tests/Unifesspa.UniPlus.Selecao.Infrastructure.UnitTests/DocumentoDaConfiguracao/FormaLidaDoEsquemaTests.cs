namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

/// <summary>A normalização se guia pelo que o esquema declara: os campos só de leitura e as partes com chave saem dele.</summary>
public sealed class FormaLidaDoEsquemaTests
{
    private static readonly FormaDoNo Raiz = new EsquemaDoDocumentoDaConfiguracao().Forma;

    // O caminho nomeia as propriedades separadas por barra, e "[]" desce para os elementos de uma lista.
    private static FormaDoNo Em(string caminho)
    {
        ArgumentNullException.ThrowIfNull(caminho);

        return caminho.Split('/').Aggregate(Raiz, static (no, passo) => passo == "[]" ? no.Itens! : no.Propriedades[passo]);
    }

    [Theory(DisplayName = "Os campos só de leitura do esquema são marcados, na raiz e nos blocos")]
    [InlineData("id")]
    [InlineData("versao")]
    [InlineData("publicadoEm")]
    [InlineData("tiposDeAtoCongelados")]
    [InlineData("identificacao/unidadeAdministradoraNome")]
    [InlineData("distribuicaoVagas/[]/quadro")]
    public void CampoSomenteLeitura_EMarcado(string caminho) => Em(caminho).SomenteLeitura.Should().BeTrue();

    [Theory(DisplayName = "O campo editável não é marcado como só de leitura")]
    [InlineData("identificacao")]
    [InlineData("identificacao/nome")]
    [InlineData("identificacao/unidadeAdministradoraCodigo")]
    [InlineData("etapas/[]/chave")]
    public void CampoEditavel_NaoEMarcado(string caminho) => Em(caminho).SomenteLeitura.Should().BeFalse();

    [Fact(DisplayName = "O nó referido traz as localizações do ponto e do destino na ordem de expansão")]
    public void NoComReferencia_TemLocalizacoesDoPontoEDoDestino() =>
        Em("etapas/[]").Localizacoes.Should().Equal("documento.schema.json#/properties/etapas/items", "etapas.schema.json#");

    [Theory]
    [InlineData("identificacao/nome", "string", false, false)]
    [InlineData("taxaDeInscricao/valor", "number", true, false)]
    [InlineData("versao", "integer", true, false)]
    [InlineData("publicadoEm", "string", false, false)]
    public void TipoDeclarado_EReconhecido(string caminho, string tipo, bool numerico, bool aceitaNulo)
    {
        FormaDoNo forma = Em(caminho);
        forma.TiposDeclarados.Should().Contain(tipo);
        forma.Numerico.Should().Be(numerico);
        forma.AceitaNulo.Should().Be(aceitaNulo);
    }

    [Fact]
    public void TiposEmLista_SaoReconhecidos()
    {
        FormaDoNo forma = Em("distribuicaoVagas/[]/referenciaDemograficaCodigo");
        forma.TiposDeclarados.Should().Equal("string", "null");
        forma.Numerico.Should().BeFalse();
        forma.AceitaNulo.Should().BeTrue();
    }

    [Theory(DisplayName = "A parte que exige chave sai do esquema com o tipo e o escopo da chave")]
    [InlineData("etapas/[]", "etapa", EscopoDaChave.Documento)]
    [InlineData("cronograma/fases/[]", "fase", EscopoDaChave.Documento)]
    [InlineData("documentosExigidos/exigencias/[]", "exigencia", EscopoDaChave.Documento)]
    [InlineData("etapas/[]/bancas/[]", "banca", EscopoDaChave.Documento)]
    [InlineData("cronograma/fases/[]/bancasRequeridas/[]", "banca", EscopoDaChave.Documento)]
    [InlineData("etapas/[]/itensDeAvaliacao/[]", "item", EscopoDaChave.ParteAncestral)]
    [InlineData("etapas/[]/escalaDeAtendimento/[]", "nivel", EscopoDaChave.ParteAncestral)]
    public void ParteComChave_TemTipoEEscopo(string caminho, string tipo, EscopoDaChave escopo) =>
        Em(caminho).Parte.Should().Be(new ParteComChave(tipo, escopo));

    [Fact(DisplayName = "Só as partes com chave do esquema têm tipo de parte, e o tipo nasce do título da chave")]
    public void PartesComChave_SaoAsDoEsquema()
    {
        string[] tipos = [.. Descendentes(Raiz, 6).Select(static no => no.Parte).OfType<ParteComChave>().Select(static parte => parte.Tipo).Distinct().Order(StringComparer.Ordinal)];

        tipos.Should().Equal("banca", "etapa", "exigencia", "fase", "item", "nivel");
    }

    // O esquema é recursivo (o predicado contém predicados): limita a profundidade para descer só o que importa.
    private static IEnumerable<FormaDoNo> Descendentes(FormaDoNo no, int profundidade)
    {
        if (profundidade == 0)
        {
            return [];
        }

        IEnumerable<FormaDoNo> filhos = no.Propriedades.Values.Concat(no.Itens is null ? [] : [no.Itens]);
        return filhos.SelectMany(filho => Descendentes(filho, profundidade - 1)).Prepend(no);
    }
}
