namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConferenciaDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ConferenciaDasReferenciasDeRegraTests
{
    [Fact]
    public void Conferir_ReferenciasIntercaladas_DevolveUmaRecusaPorOcorrenciaNaOrdem()
    {
        ReferenciaRegra atual = Regra("A", "v1", 'a');
        Dictionary<(string, string), RegraDoCatalogoCitada> catalogo = new()
        {
            [("A", "v1")] = new(TipoRegra.RegraCalculo, atual, default),
        };
        (string, ReferenciaRegra)[] referencias = Referencias(atual);

        IReadOnlyList<FieldError> recusas = ConferenciaDasReferenciasDeRegra.Conferir(referencias, catalogo);

        recusas.Select(r => r.Field).Should().Equal("primeiro", "segundo", "quarto", "quinto");
        recusas.Should().OnlyContain(r => r.Error.Code == ProcessoSeletivoErrorCodes.RegraCitadaNaoCorrespondeAoCatalogo
            && r.Error.Message == "A regra citada não corresponde à regra atual do catálogo. Cite a regra novamente.");
    }

    [Fact]
    public void Conferir_SemReferencias_NaoRecusa()
    {
        ConferenciaDasReferenciasDeRegra.Conferir([], new Dictionary<(string, string), RegraDoCatalogoCitada>()).Should().BeEmpty();
    }

    [Fact]
    public void Conferir_ArgumentoNulo_RecusaArgumento()
    {
        Action semReferencias = () => ConferenciaDasReferenciasDeRegra.Conferir(null!, new Dictionary<(string, string), RegraDoCatalogoCitada>());
        Action semCatalogo = () => ConferenciaDasReferenciasDeRegra.Conferir([], null!);

        semReferencias.Should().Throw<ArgumentNullException>().WithParameterName("referencias");
        semCatalogo.Should().Throw<ArgumentNullException>().WithParameterName("catalogo");
    }

    private static (string, ReferenciaRegra)[] Referencias(ReferenciaRegra atual)
    {
        ReferenciaRegra antiga = Regra("A", "v1", 'b');
        return
        [
            ("primeiro", Regra("A", "v2", 'a')), ("segundo", antiga), ("integra", atual),
            ("quarto", Regra("B", "v1", 'a')), ("quinto", antiga),
        ];
    }

    private static ReferenciaRegra Regra(string codigo, string versao, char hash) => ReferenciaRegra.De(codigo, versao, new string(hash, 64));
}
