namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Xunit;

/// <summary>
/// O campo da causa entregue ao cliente do documento é o ponteiro JSON (RFC 6901): o nome é o que o documento dá ao campo, cada
/// colchete é um segmento numérico, a tabela de nomes é por posição e o índice de cada colchete atravessa a conversão.
/// </summary>
public sealed class PonteiroNoDocumentoTests
{
    [Theory(DisplayName = "O campo do corpo do pedido que o documento nomeia diferente vira o ponteiro com o nome do documento e o índice de cada colchete")]
    [InlineData("fases[3].faseCanonicaId", "/cronograma/fases/3/faseCanonicaCodigo")]
    [InlineData("fases[0].faseConcluinteCodigo", "/cronograma/fases/0/faseConcluinteChave")]
    [InlineData("fases[12].unidadeResponsavel", "/cronograma/fases/12/unidadeResponsavelCodigo")]
    [InlineData("fases[1].bancasRequeridas[4].categoriasDocumentoIds", "/cronograma/fases/1/bancasRequeridas/4/categoriasDocumentoCodigos")]
    [InlineData("etapas[2].faseCodigo", "/etapas/2/faseChave")]
    [InlineData("etapas[7].id", "/etapas/7/chave")]
    public void Converter_CampoComOutroNomeNoDocumento_UsaONomeDoDocumento(string campo, string esperado) =>
        PonteiroNoDocumento.Converter(campo, OrigemNoDocumento.Identidade).Should().Be(esperado);

    [Theory(DisplayName = "O campo que o documento nomeia igual só ganha o bloco do cronograma quando é de fase")]
    [InlineData("fases[0].ordem", "/cronograma/fases/0/ordem")]
    [InlineData("fases[0].produtos[1].atoCodigo", "/cronograma/fases/0/produtos/1/atoCodigo")]
    [InlineData("etapas[0].produtos[1].atoCodigo", "/etapas/0/produtos/1/atoCodigo")]
    [InlineData("identificacao.nome", "/identificacao/nome")]
    [InlineData("cronograma.fases", "/cronograma/fases")]
    [InlineData("formularios[1].itens[2].fatoCodigo", "/formularios/1/itens/2/fatoCodigo")]
    [InlineData("taxaDeInscricao", "/taxaDeInscricao")]
    public void Converter_CampoComONomeDoDocumento_FicaComoEstaOuGanhaOBloco(string campo, string esperado) =>
        PonteiroNoDocumento.Converter(campo, OrigemNoDocumento.Identidade).Should().Be(esperado);

    [Theory(DisplayName = "O conceito da etapa, que o documento não tem, fica no trecho da etapa que o declara")]
    [InlineData("etapas[4].conceito", "/etapas/4")]
    [InlineData("etapas[4].conceito.faixas[1].nome", "/etapas/4")]
    [InlineData("etapas[4].conceitoDaNota", "/etapas/4/conceitoDaNota")]
    public void Converter_ConceitoDaEtapa_ApontaAEtapa(string campo, string esperado) =>
        PonteiroNoDocumento.Converter(campo, OrigemNoDocumento.Identidade).Should().Be(esperado);

    [Theory(DisplayName = "A barra e o til do nome se escapam em ~1 e ~0, e o índice fica como número")]
    [InlineData("a/b.c", "/a~1b/c")]
    [InlineData("a~b[3]", "/a~0b/3")]
    [InlineData("a~/b", "/a~0~1b")]
    public void Converter_NomeComBarraOuTil_EscapaOsDois(string campo, string esperado) =>
        PonteiroNoDocumento.Converter(campo, OrigemNoDocumento.Identidade).Should().Be(esperado);

    [Theory(DisplayName = "O segmento de ponteiro escreve o til como ~0 e a barra como ~1, e o escape se desfaz na ordem inversa")]
    [InlineData("etapas", "etapas")]
    [InlineData("a/b", "a~1b")]
    [InlineData("a~b", "a~0b")]
    [InlineData("~1", "~01")]
    [InlineData("~/", "~0~1")]
    public void Escapado_EDesescapado_SaoInversos(string nome, string segmento)
    {
        PonteiroNoDocumento.Escapado(nome).Should().Be(segmento);
        PonteiroNoDocumento.Desescapado(segmento).Should().Be(nome);
    }

    [Fact(DisplayName = "O segmento é obrigatório")]
    public void Segmento_Nulo_Lanca()
    {
        Assert.Throws<ArgumentNullException>(() => PonteiroNoDocumento.Escapado(null!));
        Assert.Throws<ArgumentNullException>(() => PonteiroNoDocumento.Desescapado(null!));
    }

    [Theory(DisplayName = "O índice que não é a posição numérica do item é defeito de quem emitiu o campo, e a conversão lança")]
    [InlineData("formularios[INSCRICAO].itens[2].fatoCodigo")]
    [InlineData("cronograma.etapas[Prova].inicio")]
    [InlineData("distribuicaoVagas[3f2a].modalidades")]
    [InlineData("etapas[-1].nome")]
    [InlineData("etapas[].nome")]
    public void Converter_IndiceNaoNumerico_Lanca(string campo)
    {
        Func<string?> converter = () => PonteiroNoDocumento.Converter(campo, OrigemNoDocumento.Identidade);

        converter.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "O campo ausente continua ausente: a causa que não aponta o documento não ganha ponteiro")]
    [InlineData(null)]
    [InlineData("")]
    public void Converter_CampoAusente_ContinuaAusente(string? campo) =>
        PonteiroNoDocumento.Converter(campo, OrigemNoDocumento.Identidade).Should().Be(campo);

    [Fact(DisplayName = "A conversão da lista mantém o código, a ordem e a causa sem campo")]
    public void Converter_Lista_MantemCodigoOrdemECausaSemCampo()
    {
        FieldError[] causas =
        [
            new("etapas[0].id", new DomainError("A.Um", "um")),
            new(null, new DomainError("A.Dois", "dois")),
            new("fases[1].unidadeResponsavel", new DomainError("A.Tres", "tres")),
        ];

        IReadOnlyList<FieldError> convertidas = PonteiroNoDocumento.Converter(causas, OrigemNoDocumento.Identidade);

        convertidas.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("/etapas/0/chave", "A.Um"), (null, "A.Dois"), ("/cronograma/fases/1/unidadeResponsavelCodigo", "A.Tres"));
    }

    // O documento recebido traz as fases e as etapas fora da ordem canônica: a canônica 0 está na posição 2 do documento, a 1 na
    // 0 e a 2 na 1. A fase canônica 1 traz as bancas requeridas trocadas entre si.
    private static OrigemNoDocumento DocumentoForaDaOrdemCanonica() => new(
        new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal)
        {
            ["fases"] = [2, 0, 1],
            ["fases[1].bancasRequeridas"] = [1, 0],
            ["etapas"] = [1, 0],
            ["etapas[0].produtos"] = [3, 2, 1, 0],
        },
        []);

    [Theory(DisplayName = "O índice canônico do campo vira a posição de origem da lista em que o item está no documento recebido")]
    [InlineData("fases[0].unidadeResponsavel", "/cronograma/fases/2/unidadeResponsavelCodigo")]
    [InlineData("fases[1].faseCanonicaId", "/cronograma/fases/0/faseCanonicaCodigo")]
    [InlineData("fases[2].ordem", "/cronograma/fases/1/ordem")]
    [InlineData("etapas[0].id", "/etapas/1/chave")]
    [InlineData("etapas[1].faseCodigo", "/etapas/0/faseChave")]
    [InlineData("etapas[1].conceito.faixas[1].nome", "/etapas/0")]
    public void Converter_ComOrigemDistinta_ApontaAPosicaoDeOrigem(string campo, string esperado) =>
        PonteiroNoDocumento.Converter(campo, DocumentoForaDaOrdemCanonica()).Should().Be(esperado);

    [Theory(DisplayName = "A lista aninhada se traduz pela lista do item canônico que a guarda, e o item do pai traduz pelo pai")]
    [InlineData("fases[1].bancasRequeridas[0].categoriasDocumentoIds", "/cronograma/fases/0/bancasRequeridas/1/categoriasDocumentoCodigos")]
    [InlineData("fases[1].bancasRequeridas[1]", "/cronograma/fases/0/bancasRequeridas/0")]
    [InlineData("etapas[0].produtos[3].atoCodigo", "/etapas/1/produtos/0/atoCodigo")]
    public void Converter_ListaAninhada_TraduzOsDoisNiveis(string campo, string esperado) =>
        PonteiroNoDocumento.Converter(campo, DocumentoForaDaOrdemCanonica()).Should().Be(esperado);

    [Theory(DisplayName = "A lista que a origem não guarda está na ordem do documento, e o índice fica como a regra o emitiu")]
    [InlineData("fases[0].bancasRequeridas[2]", "/cronograma/fases/2/bancasRequeridas/2")]
    [InlineData("etapas[1].bancas[5].regraDeDeliberacao", "/etapas/0/bancas/5/regraDeDeliberacao")]
    [InlineData("formularios[4].itens[1].fatoCodigo", "/formularios/4/itens/1/fatoCodigo")]
    public void Converter_ListaSemLigacaoNaOrigem_MantemAPosicaoCanonica(string campo, string esperado) =>
        PonteiroNoDocumento.Converter(campo, DocumentoForaDaOrdemCanonica()).Should().Be(esperado);

    [Fact(DisplayName = "A posição canônica que a lista guardada na origem não tem é defeito de quem a emitiu, e a conversão lança")]
    public void Converter_PosicaoForaDaListaDaOrigem_Lanca()
    {
        Func<string?> converter = () => PonteiroNoDocumento.Converter("etapas[2].id", DocumentoForaDaOrdemCanonica());

        converter.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "A causa da parte que a leitura recusou já nasce como ponteiro e a conversão a deixa como está")]
    public void Converter_CausaQueJaEPonteiro_NaoSeReconverte()
    {
        FieldError daLeitura = new("/cronograma/fases/2/bancasRequeridas/1/chave", new DomainError("A.Leitura", "leitura"));
        FieldError daRegra = new("fases[0].unidadeResponsavel", new DomainError("A.Regra", "regra"));

        IReadOnlyList<FieldError> convertidas = PonteiroNoDocumento.Converter([daLeitura, daRegra], DocumentoForaDaOrdemCanonica());

        convertidas.Select(static causa => causa.Field).Should().Equal(
            "/cronograma/fases/2/bancasRequeridas/1/chave", "/cronograma/fases/2/unidadeResponsavelCodigo");
    }

    [Fact(DisplayName = "A conversão da lista traduz cada causa pela mesma origem e mantém o código e a ordem")]
    public void Converter_ListaComOrigemDistinta_TraduzCadaCausa()
    {
        FieldError[] causas =
        [
            new("etapas[0].id", new DomainError("A.Um", "um")),
            new(null, new DomainError("A.Dois", "dois")),
            new("etapas[1].id", new DomainError("A.Tres", "tres")),
        ];

        IReadOnlyList<FieldError> convertidas = PonteiroNoDocumento.Converter(causas, DocumentoForaDaOrdemCanonica());

        convertidas.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("/etapas/1/chave", "A.Um"), (null, "A.Dois"), ("/etapas/0/chave", "A.Tres"));
    }

    [Fact(DisplayName = "A origem é obrigatória")]
    public void Converter_SemOrigem_Lanca()
    {
        Assert.Throws<ArgumentNullException>(() => PonteiroNoDocumento.Converter("etapas[0].id", null!));
        Assert.Throws<ArgumentNullException>(() => PonteiroNoDocumento.Converter([], null!));
    }
}
