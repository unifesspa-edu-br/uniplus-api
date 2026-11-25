namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CausasDasOfertasNoEnvelopeTests
{
    [Fact]
    public void LerOfertas_CodigoInvalido_ApontaOferta()
    {
        const string valor = "VALOR_FICTICIO_X!";
        JsonObject documento = JsonNode.Parse("""{"ofertas":[{"ofertaCursoOrigemId":"11111111-1111-1111-1111-111111111111","ofertaCursoCodigo":"VALOR_FICTICIO_X!","cursoNome":"Curso","cursoGrau":"Grau","unidadeOfertanteNome":"Unidade","turnos":["MATUTINO"]}]}""")!.AsObject();
        LeitorEnvelope leitor = new();
        LeituraDasOfertas.LerOfertas(leitor, documento);
        FieldError causa = leitor.Falha<object>().Errors.Single();
        DomainError erro = OfertaCursoSnapshot.Criar(valor, "Curso", "Grau", "Unidade", ["MATUTINO"]).Error!;
        ConferirCausaNoDocumento.Conferir(documento, causa, "ofertas[0]", erro.Code, erro.Message);
        causa.Field.Should().NotContain(valor);
        causa.Error.Message.Should().NotContain(valor);
    }

    [Fact]
    public void LerReferenciaDemografica_PercentualInvalido_ApontaReferencia()
    {
        JsonObject documento = JsonNode.Parse("""{"distribuicao":[{"referenciaDemografica":{"origemId":"11111111-1111-1111-1111-111111111111","censoReferencia":"Censo fictício","ppiPercentual":"101.00","quilombolaPercentual":"0.00","pcdPercentual":"0.00","baseLegal":"Norma fictícia"}}]}""")!.AsObject();
        LeitorEnvelope leitor = new();
        LeituraDaReferenciaDemografica.Ler(leitor, documento["distribuicao"]![0]!.AsObject(), "distribuicao[0]");
        FieldError causa = leitor.Falha<object>().Errors.Single();
        ConferirCausaNoDocumento.Conferir(documento, causa, "distribuicao[0].referenciaDemografica", "ReferenciaReservaDemograficaSnapshot.PercentualInvalido", "Os percentuais demográficos devem estar entre 0 e 100.");
        causa.Field.Should().NotContain("101.00");
        causa.Error.Message.Should().NotContain("101.00");
    }
}
