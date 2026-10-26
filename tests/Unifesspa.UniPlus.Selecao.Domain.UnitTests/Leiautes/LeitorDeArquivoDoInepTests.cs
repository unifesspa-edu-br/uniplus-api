namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Leiautes;

using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Leiautes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Testes.Compartilhado.ArquivoDoInepSintetico;

/// <summary>
/// A leitura do retorno do INEP pelo leiaute: cada nota e cada situação na coluna certa, a recusa
/// do arquivo de outra estrutura sem nunca citar o conteúdo, e a tolerância a fim de linha CRLF e
/// à marca BOM.
/// </summary>
public sealed class LeitorDeArquivoDoInepTests
{
    private static readonly PersonasDeTeste.PessoaFicticia Fernanda = PersonasDeTeste.Candidatas[0];
    private static readonly LeiauteDeRetornoDoInep Leiaute = CatalogoDeLeiautesDoInep.EnemRetornoV1;

    private static Result<ArquivoDeRetornoLido> Ler(byte[] bytes) => LeitorDeArquivoDoInep.Ler(bytes, Leiaute);

    private static byte[] Texto(string texto) => Encoding.UTF8.GetBytes(texto);

    [Fact(DisplayName = "cada nota vai para a sua área, na ordem do arquivo e não na ordem canônica")]
    public void Ler_CadaNotaVaiParaASuaArea()
    {
        Result<ArquivoDeRetornoLido> resultado = Ler(Bytes([Linha(Fernanda, 1)]));

        resultado.IsSuccess.Should().BeTrue();
        LinhaDeRetorno linha = resultado.Value!.Linhas.Single();
        linha.Cpf.Should().Be(Fernanda.Cpf);
        linha.NumeroDeInscricaoNoEnem.Should().Be(NumeroDeInscricao(1));
        linha.Areas.Select(a => a.Area).Should().Equal(AreaDoEnem.Todas);
        NotaDe(linha, AreaDoEnem.CienciasDaNatureza).Should().Be(611.1m);
        NotaDe(linha, AreaDoEnem.CienciasHumanas).Should().Be(722.2m);
        NotaDe(linha, AreaDoEnem.Linguagens).Should().Be(833.3m);
        NotaDe(linha, AreaDoEnem.Matematica).Should().Be(544.4m);
        NotaDe(linha, AreaDoEnem.Redacao).Should().Be(960m);
    }

    [Fact(DisplayName = "cada situação vai para a sua área: presente nas demais e ausente só na escolhida")]
    public void Ler_CadaSituacaoVaiParaASuaArea()
    {
        string[] situacoes = ["7", "7", "7", "6", "7"];
        string[] notas = ["611.1", "722.2", "833.3", "0", "960"];

        LinhaDeRetorno linha = Ler(Bytes([Linha(Fernanda, 1, notas, situacoes)])).Value!.Linhas.Single();

        SituacaoDe(linha, AreaDoEnem.Matematica).Should().Be(SituacaoDaProvaNoEnem.Ausente);
        linha.Areas.Where(a => a.Area != AreaDoEnem.Matematica)
            .Should().OnlyContain(a => a.Situacao == SituacaoDaProvaNoEnem.Presente);
    }

    [Fact(DisplayName = "linha de quem faltou (nota 0 sem casa e código 6) é estrutura válida")]
    public void Ler_LinhaDeAusente_EValida()
    {
        LinhaDeRetorno linha = Ler(Bytes([LinhaDeAusente(Fernanda, 1)])).Value!.Linhas.Single();

        linha.Areas.Should().OnlyContain(a => a.Nota == 0m && a.Situacao == SituacaoDaProvaNoEnem.Ausente);
    }

    [Fact(DisplayName = "arquivo de 23 campos é recusado pelo leiaute de 20, sem citar CPF, nota nem nome")]
    public void Ler_ArquivoDe23Campos_RecusadoSemConteudo()
    {
        Result<ArquivoDeRetornoLido> resultado = Ler(Bytes([LinhaDe23Campos(Fernanda, 1)]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(NotasDoEnemErrorCodes.ArquivoForaDoLeiaute);
        resultado.Error.Message.Should().Contain("Linha 1");
        SemConteudoDaLinha(resultado.Error.Message, Fernanda);
    }

    [Theory(DisplayName = "estrutura inválida recusa o arquivo inteiro, citando linha e coluna e nunca o conteúdo")]
    [MemberData(nameof(ArquivosForaDoLeiaute))]
    public void Ler_EstruturaInvalida_Recusa(string descricao, byte[] bytes)
    {
        Result<ArquivoDeRetornoLido> resultado = Ler(bytes);

        resultado.IsFailure.Should().BeTrue(descricao);
        resultado.Error!.Code.Should().Be(NotasDoEnemErrorCodes.ArquivoForaDoLeiaute);
        SemConteudoDaLinha(resultado.Error.Message, Fernanda);
    }

    public static TheoryData<string, byte[]> ArquivosForaDoLeiaute() => new()
    {
        { "nota com vírgula", Bytes([Linha(Fernanda, 1, notas: ["611,1", "722.2", "833.3", "544.4", "960"])]) },
        { "nota com duas casas", Bytes([Linha(Fernanda, 1, notas: ["611.12", "722.2", "833.3", "544.4", "960"])]) },
        { "nota acima de 1000", Bytes([Linha(Fernanda, 1, notas: ["1000.1", "722.2", "833.3", "544.4", "960"])]) },
        { "nota com sinal", Bytes([Linha(Fernanda, 1, notas: ["-1", "722.2", "833.3", "544.4", "960"])]) },
        { "nota com espaço", Bytes([Linha(Fernanda, 1, notas: [" 611.1", "722.2", "833.3", "544.4", "960"])]) },
        { "CPF com 10 dígitos", Bytes([Linha(Fernanda, 1, cpf: "6995983637")]) },
        { "CPF com 12 dígitos", Bytes([Linha(Fernanda, 1, cpf: "699598363712")]) },
        { "número de inscrição com 11 dígitos", Bytes([Linha(Fernanda, 1, numeroDeInscricao: "24000000001")]) },
        { "sem o separador final", Bytes([Linha(Fernanda, 1).TrimEnd(';')]) },
        { "linha em branco no meio", Bytes([Linha(Fernanda, 1), string.Empty, Linha(Fernanda, 2)]) },
        { "retorno de carro solto no meio da linha", Texto(Linha(Fernanda, 1).Replace("Laura", "La\ruRa", StringComparison.Ordinal) + "\n") },
        { "retorno de carro solto no fim, sem quebra de linha", Texto(Linha(Fernanda, 1) + "\r") },
        { "UTF-8 inválido", [.. Texto(Linha(Fernanda, 1)), 0xFF, 0xFE, (byte)'\n'] },
        { "arquivo sem nenhum byte", [] },
        { "arquivo só com a marca BOM", [0xEF, 0xBB, 0xBF] },
        { "arquivo só com quebra de linha", Texto("\n") },
        { "duas quebras de linha ao final", Texto(Linha(Fernanda, 1) + "\n\n") },
    };

    [Fact(DisplayName = "código de situação fora de 6 e 7 recusa o arquivo com erro dedicado, sem citar o conteúdo")]
    public void Ler_CodigoDeSituacaoDesconhecido_ErroDedicado()
    {
        string[] situacoes = ["7", "7", "9", "7", "7"];

        Result<ArquivoDeRetornoLido> resultado = Ler(Bytes([Linha(Fernanda, 1), Linha(Fernanda, 2, situacoes: situacoes)]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(NotasDoEnemErrorCodes.CodigoDeSituacaoDesconhecido);
        resultado.Error.Message.Should().Contain("Linha 2").And.Contain("coluna 11");
        SemConteudoDaLinha(resultado.Error.Message, Fernanda);
    }

    public static TheoryData<string, byte[]> ArquivosToleradosEquivalentes() => new()
    {
        { "CRLF em todas as linhas", Bytes([Linha(Fernanda, 1), Linha(PersonasDeTeste.Candidatas[1], 2)], fimDeLinha: "\r\n") },
        { "marca BOM no início", Bytes([Linha(Fernanda, 1), Linha(PersonasDeTeste.Candidatas[1], 2)], comBom: true) },
        { "BOM com CRLF", Bytes([Linha(Fernanda, 1), Linha(PersonasDeTeste.Candidatas[1], 2)], "\r\n", comBom: true) },
        { "sem a quebra final", Bytes([Linha(Fernanda, 1), Linha(PersonasDeTeste.Candidatas[1], 2)], quebraFinal: false) },
    };

    [Theory(DisplayName = "BOM, CRLF e quebra final única são tolerados e dão o mesmo resultado do arquivo LF")]
    [MemberData(nameof(ArquivosToleradosEquivalentes))]
    public void Ler_Tolerancias_MesmoResultadoDoArquivoLf(string descricao, byte[] bytes)
    {
        byte[] referencia = Bytes([Linha(Fernanda, 1), Linha(PersonasDeTeste.Candidatas[1], 2)]);

        Result<ArquivoDeRetornoLido> lido = Ler(bytes);
        Result<ArquivoDeRetornoLido> esperado = Ler(referencia);

        lido.IsSuccess.Should().BeTrue(descricao);
        lido.Value!.Linhas.Select(ChaveCompleta).Should().Equal(esperado.Value!.Linhas.Select(ChaveCompleta));
    }

    [Fact(DisplayName = "o mesmo conteúdo com LF e com CRLF dá linhas iguais e arquivos de bytes diferentes")]
    public void Ler_LfECrlf_BytesDiferentes()
    {
        byte[] lf = Bytes([Linha(Fernanda, 1)]);
        byte[] crlf = Bytes([Linha(Fernanda, 1)], "\r\n");

        lf.Should().NotEqual(crlf);
        ChaveCompleta(Ler(lf).Value!.Linhas.Single()).Should().Be(ChaveCompleta(Ler(crlf).Value!.Linhas.Single()));
    }

    [Fact(DisplayName = "colunas ignoradas diferentes não alteram a chave de igualdade da linha")]
    public void Ler_ColunasIgnoradasNaoEntramNaChave()
    {
        LinhaDeRetorno a = Ler(Bytes([Linha(Fernanda, 1)])).Value!.Linhas.Single();
        LinhaDeRetorno b = Ler(Bytes([Linha(Fernanda, 1, nome: "Outro Nome Qualquer")])).Value!.Linhas.Single();

        a.ChaveDeIgualdade.Should().Be(b.ChaveDeIgualdade);
    }

    private static decimal NotaDe(LinhaDeRetorno linha, string area) => linha.Areas.Single(a => a.Area == area).Nota;

    private static SituacaoDaProvaNoEnem SituacaoDe(LinhaDeRetorno linha, string area) =>
        linha.Areas.Single(a => a.Area == area).Situacao;

    private static string ChaveCompleta(LinhaDeRetorno linha) => $"{linha.NumeroDaLinha}|{linha.Cpf}|{linha.ChaveDeIgualdade}";

    private static void SemConteudoDaLinha(string mensagem, PersonasDeTeste.PessoaFicticia pessoa)
    {
        mensagem.Should().NotContain(pessoa.Cpf).And.NotContain(pessoa.Nome).And.NotContain("611").And.NotContain("960");
    }
}
