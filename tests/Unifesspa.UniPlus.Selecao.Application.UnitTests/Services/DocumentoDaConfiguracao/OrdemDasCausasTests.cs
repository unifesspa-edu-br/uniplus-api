namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Kernel.Results;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

public sealed class OrdemDasCausasTests
{
    private static readonly JsonNode Documento = JsonNode.Parse("""
        { "zeta": 1, "alfa": [ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, { "k": 1 } ], "meio": { "b": 1, "a": 2 } }
        """)!;

    private static FieldError Causa(string ponteiro, string codigo = "Codigo") => new(ponteiro, new DomainError(codigo, "Mensagem."));

    private static string?[] Ordenar(params FieldError[] causas) => [.. OrdemDasCausas.Ordenar(causas, Documento).Select(static causa => causa.Field)];

    [Fact(DisplayName = "A causa vem na ordem em que a propriedade está escrita, não na ordem alfabética")]
    public void PropriedadeNaOrdemEscrita() =>
        Ordenar(Causa("/meio"), Causa("/alfa"), Causa("/zeta")).Should().Equal("/zeta", "/alfa", "/meio");

    [Fact(DisplayName = "O índice da lista ordena como número: 10 vem depois de 2")]
    public void IndiceDaLista_OrdenaComoNumero() =>
        Ordenar(Causa("/alfa/10"), Causa("/alfa/2")).Should().Equal("/alfa/2", "/alfa/10");

    [Fact(DisplayName = "O ponto que contém vem antes dos que ele contém")]
    public void QuemContemVemAntes() =>
        Ordenar(Causa("/alfa/10/k"), Causa("/alfa/10"), Causa(string.Empty), Causa("/alfa")).Should().Equal(string.Empty, "/alfa", "/alfa/10", "/alfa/10/k");

    [Fact(DisplayName = "O ponto que o documento não tem, como um campo obrigatório ausente, vem depois do que o objeto tem")]
    public void PontoAusente_VemDepoisDoQueExiste() =>
        Ordenar(Causa("/meio/novo"), Causa("/meio/a"), Causa("/meio/b")).Should().Equal("/meio/b", "/meio/a", "/meio/novo");

    [Fact(DisplayName = "Os pontos ausentes do mesmo objeto seguem a ordem em que foram recebidos")]
    public void PontosAusentes_SeguemAOrdemRecebida() =>
        Ordenar(Causa("/meio/y"), Causa("/meio/x")).Should().Equal("/meio/y", "/meio/x");

    [Fact(DisplayName = "Dois erros no mesmo ponteiro viram um só, o primeiro recebido")]
    public void MesmoPonteiro_FicaOPrimeiro()
    {
        IReadOnlyList<FieldError> causas = OrdemDasCausas.Ordenar([Causa("/zeta", "Primeiro"), Causa("/zeta", "Segundo")], Documento);

        causas.Should().ContainSingle().Which.Error.Code.Should().Be("Primeiro");
    }

    [Fact(DisplayName = "O ponteiro com escape é desfeito para achar a posição")]
    public void PonteiroComEscape_EDesfeito()
    {
        JsonNode documento = JsonNode.Parse("""{ "b": 1, "a/b~c": 2 }""")!;

        OrdemDasCausas.Ordenar([Causa("/a~1b~0c"), Causa("/b")], documento).Select(static causa => causa.Field).Should().Equal("/b", "/a~1b~0c");
    }

    [Fact(DisplayName = "As causas e o documento são obrigatórios")]
    public void ArgumentoNulo_Lanca()
    {
        Assert.Throws<ArgumentNullException>(() => OrdemDasCausas.Ordenar(null!, Documento));
        Assert.Throws<ArgumentNullException>(() => OrdemDasCausas.Ordenar([], null!));
    }
}
