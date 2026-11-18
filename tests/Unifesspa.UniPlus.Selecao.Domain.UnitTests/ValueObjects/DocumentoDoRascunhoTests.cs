namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DocumentoDoRascunhoTests
{
    private const string Conteudo = """{"identificacao":{"nome":"Processo Seletivo Modelo"}}""";

    [Fact(DisplayName = "O documento guarda o conteúdo como veio e o nome sem os espaços das pontas")]
    public void Criar_GuardaOConteudoEONomeAparado()
    {
        Result<DocumentoDoRascunho> resultado = DocumentoDoRascunho.Criar(Conteudo, "  Processo Seletivo Modelo  ");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Conteudo.Should().Be(Conteudo);
        resultado.Value.Nome.Should().Be("Processo Seletivo Modelo");
    }

    [Theory(DisplayName = "O nome ausente, vazio ou só de espaços fica nulo e não impede o rascunho")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeSemTexto_FicaNulo(string? nome)
    {
        Result<DocumentoDoRascunho> resultado = DocumentoDoRascunho.Criar(Conteudo, nome);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Nome.Should().BeNull();
    }

    [Fact(DisplayName = "O nome com o tamanho máximo é aceito")]
    public void Criar_NomeNoLimite_EAceito()
    {
        string nome = new('a', DocumentoDoRascunho.NomeMaxLength);

        DocumentoDoRascunho.Criar(Conteudo, nome).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "O tamanho do nome é medido depois de aparado")]
    public void Criar_NomeNoLimiteComEspacosNasPontas_EAceito()
    {
        string nome = $" {new string('a', DocumentoDoRascunho.NomeMaxLength)} ";

        DocumentoDoRascunho.Criar(Conteudo, nome).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "O nome acima do tamanho máximo é recusado")]
    public void Criar_NomeAcimaDoLimite_Recusa()
    {
        string nome = new('a', DocumentoDoRascunho.NomeMaxLength + 1);

        Result<DocumentoDoRascunho> resultado = DocumentoDoRascunho.Criar(Conteudo, nome);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(RascunhoDeProcessoSeletivoErrorCodes.NomeAcimaDoLimite);
    }

    [Theory(DisplayName = "O conteúdo ausente, vazio ou só de espaços é recusado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n ")]
    public void Criar_ConteudoSemTexto_Recusa(string? conteudo)
    {
        Result<DocumentoDoRascunho> resultado = DocumentoDoRascunho.Criar(conteudo, "Nome");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(RascunhoDeProcessoSeletivoErrorCodes.ConteudoDoDocumentoAusente);
    }

    [Fact(DisplayName = "As recusas do conteúdo e do nome chegam juntas, sem parar na primeira")]
    public void Criar_ConteudoENomeInvalidos_AcumulaAsDuasRecusas()
    {
        Result<DocumentoDoRascunho> resultado = DocumentoDoRascunho.Criar(null, new string('a', DocumentoDoRascunho.NomeMaxLength + 1));

        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            RascunhoDeProcessoSeletivoErrorCodes.ConteudoDoDocumentoAusente,
            RascunhoDeProcessoSeletivoErrorCodes.NomeAcimaDoLimite,
        ]);
    }

    [Fact(DisplayName = "O domínio não interpreta o conteúdo: a forma do documento é conferida antes, pela aplicação")]
    public void Criar_ConteudoQueNaoEJson_NaoEConferidoAqui()
    {
        DocumentoDoRascunho.Criar("isto não é JSON", nome: null).IsSuccess.Should().BeTrue();
    }
}
