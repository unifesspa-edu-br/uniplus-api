namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using System.Globalization;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ObjetoDeValorGravadoTests
{
    private const string Marcador = "valor-gravado-ficticio-7f3a";

    private const string OrientacaoDaMensagem = "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.";

    [Fact(DisplayName = "A leitura devolve a mesma instância de um valor válido")]
    public void Reidratar_ClasseValida_DevolveAMesmaInstancia()
    {
        Result<Percentual> criacao = Percentual.Criar(12.34m);

        Percentual valor = ObjetoDeValorGravado.Reidratar(criacao, nameof(Percentual));

        valor.Should().BeSameAs(criacao.Value);
    }

    [Fact(DisplayName = "A leitura devolve um tipo por valor válido")]
    public void Reidratar_TipoPorValorValido_DevolveOValor()
    {
        int valor = ObjetoDeValorGravado.Reidratar(Result<int>.Success(7), "Número");

        valor.Should().Be(7);
    }

    [Fact(DisplayName = "A leitura inválida informa o nome e o código sem a descrição do erro")]
    public void Reidratar_Falha_LancaComNomeECodigo()
    {
        Result<int> criacao = Result<int>.Failure(new DomainError("Teste.Invalido", "Descrição privada"));

        FluentActions.Invoking(() => ObjetoDeValorGravado.Reidratar(criacao, "Número"))
            .Should().Throw<InvalidOperationException>().Which.Message.Should().Be(
                "Dado inválido no banco ao reidratar Número: Teste.Invalido. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
    }

    [Fact(DisplayName = "Recusas repetidas informam códigos distintos na ordem sem descrição nem campo")]
    public void Reidratar_VariasRecusas_InformaCodigosDistintos()
    {
        Result<int> resultado = Result<int>.ValidationFailure([
            new("campo-ficticio", new DomainError("Teste.Um", "texto-ficticio")),
            new("campo-ficticio", new DomainError("Teste.Dois", "texto-ficticio")),
            new("campo-ficticio", new DomainError("Teste.Um", "texto-ficticio"))]);
        string mensagem = FluentActions.Invoking(() => ObjetoDeValorGravado.Reidratar(resultado, "Valor"))
            .Should().Throw<InvalidOperationException>().Which.Message;
        mensagem.Should().Be("Dado inválido no banco ao reidratar Valor: Teste.Um, Teste.Dois. Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        mensagem.Should().NotContain("campo-ficticio").And.NotContain("texto-ficticio");
    }

    [Fact(DisplayName = "A ausência válida continua ausente")]
    public void Reidratar_SucessoNulo_DevolveAusencia() =>
        ObjetoDeValorGravado.Reidratar(Result<string?>.Success(null), "Valor").Should().BeNull();

    [Fact(DisplayName = "JSON inválido não expõe o documento nem carrega a causa")]
    public void DesserializarDocumento_JsonInvalido_OcultaConteudoECausa()
    {
        const string json = "{\"valor-gravado-ficticio-7f3a\":";
        InvalidOperationException erro = FluentActions.Invoking(() => ObjetoDeValorGravado.DesserializarDocumento(
            () => JsonDocument.Parse(json), "Valor")).Should().Throw<InvalidOperationException>().Which;
        erro.Message.Should().Be("Dado inválido no banco ao reidratar Valor: documento JSON inválido. Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        erro.Message.Should().NotContain("valor-gravado-ficticio-7f3a");
        erro.InnerException.Should().BeNull();
    }

    [Fact(DisplayName = "Texto fora do formato não expõe o valor lido nem carrega a causa")]
    public void DesserializarDocumento_FormatoInvalido_OcultaConteudoECausa()
    {
        Func<DateOnly> conversao = () => DateOnly.ParseExact(Marcador, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        InvalidOperationException erro = FluentActions.Invoking(() => ObjetoDeValorGravado.DesserializarDocumento(conversao, "Valor"))
            .Should().Throw<InvalidOperationException>().Which;

        erro.Message.Should().Be($"Dado inválido no banco ao reidratar Valor: valor fora do formato esperado. {OrientacaoDaMensagem}");
        erro.Message.Should().NotContain(Marcador);
        erro.InnerException.Should().BeNull();
    }

    [Fact(DisplayName = "Bytes de JSON malformado não expõem o documento nem carregam a causa")]
    public void DesserializarDocumento_BytesDeJsonMalformado_OcultaConteudoECausa()
    {
        byte[] bytes = Encoding.UTF8.GetBytes($"{{\"{Marcador}\":");

        RecusarBytes(bytes);
    }

    [Fact(DisplayName = "Bytes que não são UTF-8 válido não expõem o documento nem carregam a causa")]
    public void DesserializarDocumento_BytesUtf8Invalidos_OcultaConteudoECausa()
    {
        byte[] bytes = [.. Encoding.UTF8.GetBytes($"\"{Marcador}"), 0xFF, 0xFE, (byte)'"'];

        RecusarBytes(bytes);
    }

    private static void RecusarBytes(byte[] bytes)
    {
        InvalidOperationException erro = FluentActions.Invoking(() => ObjetoDeValorGravado.DesserializarDocumento(
            () => JsonSerializer.Deserialize<string>(bytes), "Valor")).Should().Throw<InvalidOperationException>().Which;

        erro.Message.Should().Be($"Dado inválido no banco ao reidratar Valor: documento JSON inválido. {OrientacaoDaMensagem}");
        erro.Message.Should().NotContain(Marcador);
        erro.InnerException.Should().BeNull();
    }

    [Fact(DisplayName = "Documento válido devolve o resultado")]
    public void DesserializarDocumento_Valido_DevolveResultado() =>
        ObjetoDeValorGravado.DesserializarDocumento(() => JsonSerializer.Deserialize<int>("7"), "Valor").Should().Be(7);

    [Fact(DisplayName = "Outra exceção passa sem alteração")]
    public void DesserializarDocumento_OutraExcecao_Propaga()
    {
        InvalidOperationException original = new("falha fictícia");
        FluentActions.Invoking(() => ObjetoDeValorGravado.DesserializarDocumento<int>(() => throw original, "Valor"))
            .Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(original);
    }
}
