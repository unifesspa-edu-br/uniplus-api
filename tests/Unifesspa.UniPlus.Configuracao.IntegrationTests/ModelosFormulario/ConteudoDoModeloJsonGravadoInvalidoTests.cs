namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

/// <summary>
/// O conteúdo do modelo gravado que a plataforma não consegue converter falha na leitura com mensagem fixa: nem o valor
/// lido nem a exceção da biblioteca saem na mensagem, porque a biblioteca pode repetir o valor gravado.
/// </summary>
public sealed class ConteudoDoModeloJsonGravadoInvalidoTests
{
    private const string DataValida = "2027-02-01";
    private const string DataInvalida = "data-invalida-ficticia-7f3a";
    private const string IdentificadorValido = "11111111-1111-1111-1111-111111111111";
    private const string IdentificadorInvalido = "identificador-invalido-ficticio-7f3a";
    private const string OrdemValida = "\"ordem\":0,\"tipo\":\"SECAO\"";
    private const string OrdemForaDoInteiro = "\"ordem\":99999999999,\"tipo\":\"SECAO\"";

    private const string Pagina =
        """{"codigo":"DADOS","ordem":0,"tipo":"SECAO","bloco":null,"titulo":"Dados","descricao":null,"aviso":null,"exibicao":null}""";

    private const string Termo =
        """{"codigo":"TERMO_USO","ordem":0,"termoId":"11111111-1111-1111-1111-111111111111","versaoId":"22222222-2222-2222-2222-222222222222","exibicao":null,"obrigatoriedade":{"tipo":"SEMPRE","predicado":null}}""";

    private const string Validade =
        """{"escolha":"EXIGE_DOCUMENTO_VALIDO","referencia":{"tipo":"DATA_ESPECIFICA","data":"2027-02-01","faseCodigo":null},"efeitoDoVencimento":"ACEITAR_COM_AVISO"}""";

    private const string Documento =
        $$"""{"faseCodigo":"INSCRICAO","tipoDocumentoCodigo":"RG","obrigatorio":true,"quando":null,"consequenciaDoDescumprimento":"INDEFERIMENTO","abrePrazoParaReenvio":false,"basesLegais":[],"prazoDeEmissao":null,"validade":{{Validade}},"porOpcao":null}""";

    private const string Conteudo =
        $$"""{"titulo":null,"paginas":[{{Pagina}}],"itens":[],"termos":[{{Termo}}],"pressupostos":[],"grupos":[],"documentosExigidos":[{{Documento}}],"gruposDeDocumentos":[]}""";

    [Fact(DisplayName = "O conteúdo válido continua remontando e volta idêntico ao ser gravado de novo")]
    public void Desserializar_ConteudoValido_VoltaIdenticoAoSerGravado()
    {
        ConteudoDoModelo conteudo = ConteudoDoModeloJson.Desserializar(Conteudo);

        ConteudoDoModeloJson.Serializar(conteudo).Should().Be(Conteudo);
    }

    [Theory(DisplayName = "O valor fora do formato esperado é recusado com mensagem fixa, sem o valor gravado")]
    [InlineData(DataValida, DataInvalida)]
    [InlineData(IdentificadorValido, IdentificadorInvalido)]
    [InlineData(OrdemValida, OrdemForaDoInteiro)]
    public void Desserializar_ValorForaDoFormato_RecusaComMensagemFixa(string valido, string gravado)
    {
        string json = Conteudo.Replace(valido, gravado, StringComparison.Ordinal);

        Action ler = () => ConteudoDoModeloJson.Desserializar(json);

        InvalidOperationException erro = ler.Should().Throw<InvalidOperationException>().Which;
        erro.Message.Should().Be(
            "Dado inválido no banco ao reidratar ConteudoDoModelo: valor fora do formato esperado. " +
            "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        erro.Message.Should().NotContain(gravado);
        erro.InnerException.Should().BeNull();
    }

    [Fact(DisplayName = "O documento que não é JSON é recusado com mensagem fixa, sem o texto gravado")]
    public void Desserializar_DocumentoMalformado_RecusaComMensagemFixa()
    {
        Action ler = () => ConteudoDoModeloJson.Desserializar("{\"titulo\":\"documento-malformado-ficticio-7f3a");

        InvalidOperationException erro = ler.Should().Throw<InvalidOperationException>().Which;
        erro.Message.Should().Be(
            "Dado inválido no banco ao reidratar ConteudoDoModelo: documento JSON inválido. " +
            "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        erro.Message.Should().NotContain("documento-malformado-ficticio-7f3a");
        erro.InnerException.Should().BeNull();
    }

    [Theory(DisplayName = "O objeto ou o texto gravado com tipo errado falha na leitura, sem virar exigência ausente")]
    [InlineData("\"validade\":" + Validade, "\"validade\":42")]
    [InlineData("\"validade\":" + Validade, "\"validade\":\"texto\"")]
    [InlineData("\"porOpcao\":null", "\"porOpcao\":42")]
    [InlineData("\"efeitoDoVencimento\":\"ACEITAR_COM_AVISO\"", "\"efeitoDoVencimento\":7")]
    [InlineData("\"referencia\":{\"tipo\":\"DATA_ESPECIFICA\",\"data\":\"2027-02-01\",\"faseCodigo\":null}", "\"referencia\":42")]
    [InlineData("\"faseCodigo\":null}", "\"faseCodigo\":5}")]
    public void Desserializar_TipoErradoNoDocumento_Falha(string valido, string gravado)
    {
        string json = Conteudo.Replace(valido, gravado, StringComparison.Ordinal);

        Action ler = () => ConteudoDoModeloJson.Desserializar(json);

        ler.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "O prazo de emissão gravado com o número 42 falha na leitura")]
    public void Desserializar_PrazoDeEmissaoNumero_Falha()
    {
        string json = Conteudo.Replace("\"prazoDeEmissao\":null", "\"prazoDeEmissao\":42", StringComparison.Ordinal);

        Action ler = () => ConteudoDoModeloJson.Desserializar(json);

        ler.Should().Throw<InvalidOperationException>();
    }
}
