namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class RascunhoDeProcessoSeletivoTests
{
    private static readonly Guid Unidade = Guid.CreateVersion7();

    private static DocumentoDoRascunho Documento(string conteudo = """{"identificacao":{}}""", string? nome = "Primeiro nome") =>
        DocumentoDoRascunho.Criar(conteudo, nome).Value!;

    private static RascunhoDeProcessoSeletivo Novo() => RascunhoDeProcessoSeletivo.Criar(Unidade, Documento()).Value!;

    private static PrecondicaoIfMatch Com(string etag) => PrecondicaoIfMatch.DeTags([etag]);

    [Fact(DisplayName = "O rascunho nasce na revisão 1, com o documento, o nome e a unidade dona informados")]
    public void Criar_NasceNaPrimeiraRevisao()
    {
        Result<RascunhoDeProcessoSeletivo> resultado = RascunhoDeProcessoSeletivo.Criar(Unidade, Documento("""{"a":1}""", "Nome do certame"));

        resultado.IsSuccess.Should().BeTrue();
        RascunhoDeProcessoSeletivo rascunho = resultado.Value!;
        rascunho.Revisao.Should().Be(1);
        rascunho.Documento.Should().Be("""{"a":1}""");
        rascunho.Nome.Should().Be("Nome do certame");
        rascunho.UnidadeAdministradoraOrigemId.Should().Be(Unidade);
        rascunho.Id.Should().NotBeEmpty();
    }

    [Fact(DisplayName = "O ETag é forte e identifica a revisão")]
    public void ETag_IdentificaARevisao()
    {
        Novo().ETag.Should().Be("\"1\"");
    }

    [Fact(DisplayName = "Sem unidade dona o rascunho não nasce")]
    public void Criar_SemUnidade_Recusa()
    {
        Result<RascunhoDeProcessoSeletivo> resultado = RascunhoDeProcessoSeletivo.Criar(Guid.Empty, Documento());

        resultado.Error!.Code.Should().Be(RascunhoDeProcessoSeletivoErrorCodes.UnidadeAdministradoraAusente);
    }

    [Fact(DisplayName = "Substituir com o ETag da revisão atual troca o documento, o nome e a unidade e sobe a revisão")]
    public void Substituir_PrecondicaoQueCasa_TrocaESobeARevisao()
    {
        RascunhoDeProcessoSeletivo rascunho = Novo();
        Guid outraUnidade = Guid.CreateVersion7();

        Result resultado = rascunho.Substituir(Documento("""{"b":2}""", "Segundo nome"), outraUnidade, Com(rascunho.ETag));

        resultado.IsSuccess.Should().BeTrue();
        rascunho.Revisao.Should().Be(2);
        rascunho.ETag.Should().Be("\"2\"");
        rascunho.Documento.Should().Be("""{"b":2}""");
        rascunho.Nome.Should().Be("Segundo nome");
        rascunho.UnidadeAdministradoraOrigemId.Should().Be(outraUnidade);
    }

    [Fact(DisplayName = "Substituir por um documento sem nome apaga o nome da listagem")]
    public void Substituir_DocumentoSemNome_ApagaONome()
    {
        RascunhoDeProcessoSeletivo rascunho = Novo();

        rascunho.Substituir(Documento(nome: null), Unidade, Com(rascunho.ETag));

        rascunho.Nome.Should().BeNull();
    }

    [Fact(DisplayName = "Sem If-Match a substituição é exigida com precondição e nada muda")]
    public void Substituir_SemPrecondicao_ExigePrecondicao()
    {
        RascunhoDeProcessoSeletivo rascunho = Novo();

        Result resultado = rascunho.Substituir(Documento("""{"b":2}"""), Unidade, PrecondicaoIfMatch.Ausente);

        resultado.Error!.Code.Should().Be(PrecondicaoErrorCodes.Requerida);
        NadaMudou(rascunho);
    }

    [Fact(DisplayName = "ETag de outra revisão não casa e nada muda")]
    public void Substituir_ETagDeOutraRevisao_Falha()
    {
        RascunhoDeProcessoSeletivo rascunho = Novo();

        Result resultado = rascunho.Substituir(Documento("""{"b":2}"""), Unidade, Com("\"7\""));

        resultado.Error!.Code.Should().Be(PrecondicaoErrorCodes.Falhou);
        NadaMudou(rascunho);
    }

    [Fact(DisplayName = "O ETag lido antes de outra substituição fica obsoleto")]
    public void Substituir_ETagObsoleto_Falha()
    {
        RascunhoDeProcessoSeletivo rascunho = Novo();
        string lidoPelosDoisEditores = rascunho.ETag;
        rascunho.Substituir(Documento("""{"b":2}"""), Unidade, Com(lidoPelosDoisEditores)).IsSuccess.Should().BeTrue();

        Result segundo = rascunho.Substituir(Documento("""{"c":3}"""), Unidade, Com(lidoPelosDoisEditores));

        segundo.Error!.Code.Should().Be(PrecondicaoErrorCodes.Falhou);
        rascunho.Revisao.Should().Be(2);
        rascunho.Documento.Should().Be("""{"b":2}""");
    }

    [Fact(DisplayName = "O curinga casa com o rascunho existente")]
    public void Substituir_Curinga_Casa()
    {
        RascunhoDeProcessoSeletivo rascunho = Novo();

        rascunho.Substituir(Documento("""{"b":2}"""), Unidade, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        rascunho.Revisao.Should().Be(2);
    }

    [Fact(DisplayName = "Numa lista de ETags basta um casar")]
    public void Substituir_ListaComUmETagQueCasa_Casa()
    {
        RascunhoDeProcessoSeletivo rascunho = Novo();
        PrecondicaoIfMatch lista = PrecondicaoIfMatch.DeTags(["\"9\"", rascunho.ETag]);

        rascunho.Substituir(Documento("""{"b":2}"""), Unidade, lista).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Lista sem nenhum ETag da revisão atual não casa")]
    public void Substituir_ListaSemETagQueCasa_Falha()
    {
        RascunhoDeProcessoSeletivo rascunho = Novo();

        Result resultado = rascunho.Substituir(Documento("""{"b":2}"""), Unidade, PrecondicaoIfMatch.DeTags(["\"8\"", "\"9\""]));

        resultado.Error!.Code.Should().Be(PrecondicaoErrorCodes.Falhou);
    }

    [Fact(DisplayName = "O If-Match só com ETags fracas chega como lista vazia e não casa")]
    public void Substituir_FracasDescartadas_NaoCasa()
    {
        RascunhoDeProcessoSeletivo rascunho = Novo();

        Result resultado = rascunho.Substituir(Documento("""{"b":2}"""), Unidade, PrecondicaoIfMatch.DeTags([]));

        resultado.Error!.Code.Should().Be(PrecondicaoErrorCodes.Falhou);
        NadaMudou(rascunho);
    }

    [Fact(DisplayName = "Substituir sem unidade dona é recusado e a revisão não sobe")]
    public void Substituir_SemUnidade_Recusa()
    {
        RascunhoDeProcessoSeletivo rascunho = Novo();

        Result resultado = rascunho.Substituir(Documento("""{"b":2}"""), Guid.Empty, Com(rascunho.ETag));

        resultado.Error!.Code.Should().Be(RascunhoDeProcessoSeletivoErrorCodes.UnidadeAdministradoraAusente);
        NadaMudou(rascunho);
    }

    [Fact(DisplayName = "A precondição é conferida antes da unidade: a pessoa com a revisão velha recebe a recusa de concorrência")]
    public void Substituir_PrecondicaoFalhaESemUnidade_RecusaAPrecondicao()
    {
        RascunhoDeProcessoSeletivo rascunho = Novo();

        Result resultado = rascunho.Substituir(Documento("""{"b":2}"""), Guid.Empty, Com("\"7\""));

        resultado.Error!.Code.Should().Be(PrecondicaoErrorCodes.Falhou);
    }

    [Fact(DisplayName = "Os argumentos nulos são recusados")]
    public void Substituir_ArgumentosNulos_Lancam()
    {
        RascunhoDeProcessoSeletivo rascunho = Novo();

        Action semDocumento = () => rascunho.Substituir(null!, Unidade, PrecondicaoIfMatch.Curinga);
        Action semPrecondicao = () => rascunho.Substituir(Documento(), Unidade, null!);
        Action criarSemDocumento = () => RascunhoDeProcessoSeletivo.Criar(Unidade, null!);

        semDocumento.Should().Throw<ArgumentNullException>();
        semPrecondicao.Should().Throw<ArgumentNullException>();
        criarSemDocumento.Should().Throw<ArgumentNullException>();
    }

    private static void NadaMudou(RascunhoDeProcessoSeletivo rascunho)
    {
        rascunho.Revisao.Should().Be(1);
        rascunho.Documento.Should().Be("""{"identificacao":{}}""");
        rascunho.Nome.Should().Be("Primeiro nome");
        rascunho.UnidadeAdministradoraOrigemId.Should().Be(Unidade);
    }
}
