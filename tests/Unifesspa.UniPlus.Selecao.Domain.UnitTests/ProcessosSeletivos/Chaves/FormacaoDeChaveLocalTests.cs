namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Chaves;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Chaves;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class FormacaoDeChaveLocalTests
{
    [Theory]
    [InlineData("fase", "INSCRICAO", "", "fase_inscricao")]
    [InlineData("etapa", "PROVA_OBJETIVA", "", "etapa_prova_objetiva")]
    [InlineData("etapa", "PROVA_OBJETIVA", "etapa_prova_objetiva", "etapa_prova_objetiva_2")]
    [InlineData("etapa", "etapa_prova_objetiva", "", "etapa_prova_objetiva")]
    [InlineData("banca", "BANCA_HETEROIDENTIFICACAO", "", "banca_heteroidentificacao")]
    [InlineData("banca", "BANCA", "", "banca")]
    [InlineData("banca", "BANCARIA", "", "banca_bancaria")]
    [InlineData("exigencia", "CERTIDAO_NASCIMENTO", "", "exigencia_certidao_nascimento")]
    [InlineData("exigencia", "FOTO_3X4", "exigencia_foto_3x4", "exigencia_foto_3x4_2")]
    [InlineData("exigencia", "FOTO_3X4_2", "exigencia_foto_3x4,exigencia_foto_3x4_2", "exigencia_foto_3x4_2_2")]
    [InlineData("item", "Redação", "", "item_redacao")]
    [InlineData("item", "Redação", "item_redacao", "item_redacao_2")]
    [InlineData("nivel", "Atende parcialmente (50%)", "", "nivel_atende_parcialmente_50")]
    [InlineData("item", "Domínio da modalidade escrita formal da língua portuguesa e adequação ao tema", "", "item_dominio_da_modalidade_escrita_formal_da_lingua_portuguesa_e")]
    [InlineData("item", "—", "", "item")]
    [InlineData("nivel", "(*)", "nivel", "nivel_2")]
    [InlineData("item", "Domínio da modalidade escrita formal da língua portuguesa e adequação ao tema", "item_dominio_da_modalidade_escrita_formal_da_lingua_portuguesa_e", "item_dominio_da_modalidade_escrita_formal_da_lingua_portuguesa_2")]
    [InlineData("item", "á", "", "item_a")]
    [InlineData("item", "\u0301", "", "item")]
    [InlineData("item", "a", "item_a,item_a_2", "item_a_3")]
    [InlineData("fase", "X", "", "fase_x")]
    [InlineData("item", "a", "item_a,item_a_3", "item_a_2")]
    public void Formar_ComTextoEChavesEmUso_DevolveChaveEsperada(string prefixo, string texto, string existentes, string esperada)
    {
        ArgumentNullException.ThrowIfNull(existentes);
        var chaves = new ChavesEmUso();
        foreach (string existente in existentes.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            chaves.Adicionar(ChaveLocal.Criar(existente).Value).Should().BeTrue();
        }

        FormacaoDeChaveLocal.Formar(prefixo, texto, chaves).Valor.Should().Be(esperada);
    }

    [Fact]
    public void Formar_ComComprimentoExato_PreservaTexto()
    {
        string texto = new('a', 59);
        FormacaoDeChaveLocal.Formar("item", texto, new ChavesEmUso()).Valor.Should().Be("item_" + texto);
    }

    [Fact]
    public void Formar_ComPalavraLonga_ConservaPalavraQueCabe()
    {
        string texto = new('a', 70);
        FormacaoDeChaveLocal.Formar("item", texto, new ChavesEmUso()).Valor.Should().Be("item");
    }
}
