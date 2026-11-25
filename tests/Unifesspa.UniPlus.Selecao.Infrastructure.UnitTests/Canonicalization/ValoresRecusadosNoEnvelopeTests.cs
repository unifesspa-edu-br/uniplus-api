namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class ValoresRecusadosNoEnvelopeTests
{
    [Theory]
    [InlineData("data", "data-ficticia-de-teste", "esperada uma data 'yyyy-MM-dd'.")]
    [InlineData("data", "0001-01-01", "a data não pode ser o valor padrão — é assim que uma data omitida se materializa.")]
    [InlineData("dataOpcional", "0001-01-01", "a data não pode ser o valor padrão — é assim que uma data omitida se materializa.")]
    [InlineData("dataOpcional", "data-ficticia-de-teste", "esperada uma data 'yyyy-MM-dd'.")]
    [InlineData("instante", "instante-ficticio-de-teste", "esperado um instante canônico")]
    [InlineData("decimal", "1234567.891", "esperado um decimal canônico com 2 casas.")]
    [InlineData("precisao", "1234567.89", "o decimal tem mais dígitos do que a coluna comporta (numeric(5,2)).")]
    [InlineData("identificador", "ID-FICTICIO-DE-TESTE", "esperado um Guid canônico (minúsculo, formato D).")]
    public void LerCampo_ValorForaDaRegra_NaoRepeteValor(string tipo, string valor, string regra)
    {
        JsonObject documento = new() { ["campo"] = valor };
        LeitorEnvelope leitor = new();
        LerCampo(leitor, documento, tipo);
        ConferirRecusa(leitor, valor, "documento.campo", regra);
    }

    [Fact]
    public void Textos_ItemComEspacos_NaoRepeteValor()
    {
        const string valor = "texto-ficticio-de-teste";
        JsonObject documento = new() { ["campo"] = new JsonArray($" {valor} ") };
        LeitorEnvelope leitor = new();
        leitor.Textos(documento, "campo", "documento");
        ConferirRecusa(leitor, valor, "documento.campo[0]", "esperado um texto já normalizado (sem espaços nas pontas).");
    }

    [Fact]
    public void LerValoresDominioDeclarados_ValorRepetido_NaoRepeteValor()
    {
        const string valor = "VALOR_FICTICIO_DE_TESTE";
        JsonObject documento = new() { ["opcoesDeclaradas"] = new JsonArray(Opcao(valor), Opcao(valor)) };
        LeitorEnvelope leitor = new();
        LeituraDosValoresDeDominio.LerValoresDominioDeclarados(leitor, documento, "documento");
        ConferirRecusa(leitor, valor, "documento.opcoesDeclaradas", "um valor aparece mais de uma vez.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConferirDerivados_CodigoJaColetadoOuDeGrupo_NaoRepeteValor(bool coletado)
    {
        const string valor = "VALOR_FICTICIO_DE_TESTE";
        ConfiguracaoDerivacaoFato derivacao = ConfiguracaoDerivacaoFato.Criar(valor, [RegraDerivacaoConfigurada.Criar(0, null, null).Value!]).Value!;
        HashSet<string> coletados = coletado ? [valor] : [];
        HashSet<string> grupos = coletado ? [] : [valor];
        DomainError? erro = ConferenciaDoVocabularioDeFatos.ConferirDerivados([derivacao], coletados, grupos, []);
        LeitorEnvelope leitor = new();
        leitor.Propagar<object>(erro!);
        ConferirRecusa(leitor, valor, "calculosDosDados", "o código já é de fato coletado ou de grupo.");
    }

    private static JsonObject Opcao(string valor) => new() { ["valorCodigo"] = valor, ["descricao"] = null, ["ordem"] = 0 };

    private static void LerCampo(LeitorEnvelope leitor, JsonObject documento, string tipo)
    {
        switch (tipo)
        {
            case "data": leitor.Data(documento, "campo", "documento"); break;
            case "dataOpcional": leitor.DataOpcional(documento, "campo", "documento"); break;
            case "instante": leitor.Instante(documento, "campo", "documento"); break;
            case "decimal": leitor.Decimal(documento, "campo", 2, "documento"); break;
            case "precisao": leitor.Decimal(documento, "campo", 2, "documento", 5); break;
            case "identificador": leitor.Identificador(documento, "campo", "documento"); break;
            default: throw new ArgumentException("Tipo de campo desconhecido.", nameof(tipo));
        }
    }

    private static void ConferirRecusa(LeitorEnvelope leitor, string valor, string caminho, string regra)
    {
        FieldError causa = leitor.Falha<object>().Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        causa.Error.Message.Should().Contain(caminho).And.Contain(regra).And.NotContain(valor);
        causa.Field.Should().BeNull();
    }
}
