namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

public sealed class ProjecaoDaVersaoPublicadaTests
{
    [Fact]
    public void Projetar_VersaoPublicada_DevolveSomenteRaizDoFormato()
    {
        Guid id = Guid.CreateVersion7();
        DateTimeOffset instante = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);
        IEsquemaDoDocumentoDaConfiguracao esquema = Substitute.For<IEsquemaDoDocumentoDaConfiguracao>();
        esquema.Forma.Returns(FormaDaRaiz());
        ProjecaoDaVersaoPublicada projecao = new(esquema);
        JsonObject documento = projecao.Projetar(new(id, 2, instante, "{}"));
        documento.Select(propriedade => propriedade.Key).Should().Equal("formato", "versaoFormato", "id", "versao", "publicadoEm");
        documento["formato"]!.GetValue<string>().Should().Be("uniplus.selecao.configuracao");
        documento["versaoFormato"]!.GetValue<int>().Should().Be(1);
        documento["id"]!.GetValue<string>().Should().Be(id.ToString());
        documento["versao"]!.GetValue<int>().Should().Be(2);
        documento["publicadoEm"]!.GetValue<string>().Should().Be("2026-03-13T19:00:00.000000Z");
    }
    private static FormaDoNo FormaDaRaiz()
    {
        string[] partes = ["cronograma", "etapas", "documentosExigidos", "formularios", "motivosDeDecisao", "identificacao", "coleta", "opcoesDeCurso", "distribuicaoVagas", "ordemDeRemanejamento",
            "taxaDeInscricao", "divulgacao", "tiposDeAto", "classificacao", "atendimento", "fada", "provaPresencial"];
        FormaDoNo vazia = new([], [], false, null, () => new Dictionary<string, FormaDoNo>(), () => null);
        return new([], [], false, null, () => partes.ToDictionary(nome => nome, _ => vazia, StringComparer.Ordinal), () => null);
    }
}

