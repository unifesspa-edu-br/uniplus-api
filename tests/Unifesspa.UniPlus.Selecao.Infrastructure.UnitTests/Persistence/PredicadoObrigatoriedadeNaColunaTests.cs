namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

public sealed class PredicadoObrigatoriedadeNaColunaTests
{
    [Theory]
    [InlineData("""{"$tipo":"etapaObrigatoria","TipoEtapaCodigo":"PROVA"}""")]
    [InlineData("""{"$tipo":"etapaObrigatoria","tipoEtapaCodigo":"PROVA","extra":true}""")]
    [InlineData("""{"$tipo":"etapaObrigatoria"}""")]
    [InlineData("""{"$tipo":"etapaObrigatoria","tipoEtapaCodigo":null}""")]
    public void Ler_DocumentoInvalido_FalhaExplicitamente(string documento)
    {
        Action leitura = () => ObrigatoriedadeLegalConfiguration.PredicadoConverter.ConvertFromProvider(documento);

        InvalidOperationException erro = leitura.Should().Throw<InvalidOperationException>().Which;
        erro.Message.Should().Be("Dado inválido no banco ao reidratar PredicadoObrigatoriedade: documento JSON inválido. Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        erro.InnerException.Should().BeNull();
    }

    public static TheoryData<PredicadoObrigatoriedade> Predicados => new()
    {
        new EtapaObrigatoria("PROVA"),
        new ModalidadesMinimas(["AMPLA", "RESERVADA"]),
        new DesempateDeveIncluir("IDADE"),
        new DocumentoObrigatorioParaModalidade("RESERVADA", "DOCUMENTO"),
        new AtendimentoDisponivel(["ACESSIBILIDADE"]),
        new ConcorrenciaDuplaObrigatoria(),
        new Customizado(JsonSerializer.SerializeToElement(new { chave = "valor" })),
    };

    [Theory]
    [MemberData(nameof(Predicados))]
    public void Conversor_PredicadoConhecido_PreservaConteudo(PredicadoObrigatoriedade predicado)
    {
        ArgumentNullException.ThrowIfNull(predicado);
        ValueConverter<PredicadoObrigatoriedade, string> conversor = ObrigatoriedadeLegalConfiguration.PredicadoConverter;
        object? documento = conversor.ConvertToProvider(predicado);
        object? lido = conversor.ConvertFromProvider(documento);

        lido.Should().BeOfType(predicado.GetType());
        conversor.ConvertToProvider(lido).Should().Be(documento);
        PredicadoObrigatoriedade copia = ObrigatoriedadeLegalConfiguration.PredicadoComparer.Snapshot(predicado);
        conversor.ConvertToProvider(copia).Should().Be(documento);
    }
}
