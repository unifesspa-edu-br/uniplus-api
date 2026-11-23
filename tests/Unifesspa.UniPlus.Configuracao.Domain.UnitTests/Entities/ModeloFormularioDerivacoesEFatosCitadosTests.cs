namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.ApoioDoModeloDeFormulario;

/// <summary>
/// Os fatos que o modelo pode citar: pressupostos da inscrição, derivados do catálogo e fatos que o próprio modelo coleta.
/// </summary>
public sealed class ModeloFormularioDerivacoesEFatosCitadosTests
{
    [Fact(DisplayName = "Modelo de habilitação cita fato pressuposto da inscrição")]
    public void Habilitacao_CitaPressuposto_Aceita()
    {
        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO_EMITIDO", 0, exibicao: Quando("CONCLUSAO_REGULAR"))], "CONCLUSAO_REGULAR"));

        modelo.IsSuccess.Should().BeTrue(modelo.Error?.Message);
    }

    [Fact(DisplayName = "Sem o pressuposto, a regra que cita fato da inscrição é recusada")]
    public void Habilitacao_CitaFatoSemPressuposto_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO_EMITIDO", 0, exibicao: Quando("CONCLUSAO_REGULAR"))]));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
    }

    [Fact(DisplayName = "Derivado do catálogo cujas dependências são pressupostas pode ser citado")]
    public void DerivadoDosPressupostos_Aceita()
    {
        Dictionary<string, IReadOnlyCollection<string>> derivacoes = new(StringComparer.Ordinal) { ["EGRESSO_ESCOLA_PUBLICA"] = ["TIPO_ESCOLA"] };

        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao,
            Conteudo([Item("DECLARACAO_ESCOLAR", 0, exibicao: Quando("EGRESSO_ESCOLA_PUBLICA"))], "TIPO_ESCOLA"),
            derivacoes);

        modelo.IsSuccess.Should().BeTrue(modelo.Error?.Message);
    }

    [Fact(DisplayName = "Campo opcional que alimenta um derivado citado pelo modelo é recusado")]
    public void CampoOpcionalQueAlimentaDerivadoCitado_Recusa()
    {
        Dictionary<string, IReadOnlyCollection<string>> derivacoes = new(StringComparer.Ordinal)
        {
            ["EGRESSO_ESCOLA_PUBLICA"] = ["TIPO_ESCOLA"],
            ["OUTRO_DERIVADO"] = ["RENDA_DECLARADA"],
        };

        Result<ModeloFormulario> citado = CriarInscricao(
            Conteudo([Item("TIPO_ESCOLA", 0, Obrigatoriedade.Nunca), Item("COTA", 1, exibicao: Quando("EGRESSO_ESCOLA_PUBLICA"))]),
            derivacoes);

        citado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.OpcionalQueAlimentaRegra);
    }

    [Fact(DisplayName = "Campo opcional que alimenta um derivado não citado pelo modelo é aceito")]
    public void CampoOpcionalQueAlimentaDerivadoNaoCitado_Aceita()
    {
        Dictionary<string, IReadOnlyCollection<string>> derivacoes = new(StringComparer.Ordinal)
        {
            ["EGRESSO_ESCOLA_PUBLICA"] = [],
            ["OUTRO_DERIVADO"] = ["RENDA_DECLARADA"],
        };

        Result<ModeloFormulario> naoCitado = CriarInscricao(
            Conteudo([Item("RENDA_DECLARADA", 0, Obrigatoriedade.Nunca), Item("COTA", 1, exibicao: Quando("EGRESSO_ESCOLA_PUBLICA"))]),
            derivacoes);

        naoCitado.IsSuccess.Should().BeTrue(naoCitado.Error?.Message);
    }

    [Fact(DisplayName = "Pressuposto que só difere do item por espaço é o próprio item")]
    public void PressupostoQueEItemPorEspaco_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CONCLUSAO_REGULAR", 0)], "CONCLUSAO_REGULAR "));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.PressupostoTambemItem);
    }

    [Theory(DisplayName = "Documento do conjunto padrão que cita fato que o modelo não coleta é recusado, e o que cita fato coletado passa")]
    [InlineData("CERTIFICADO", true)]
    [InlineData("OUTRO_FATO", false)]
    public void DocumentoCitaFatoQueOModeloNaoColeta_Recusa(string fatoCitado, bool aceito)
    {
        PredicadoDnf quando = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar(fatoCitado, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;
        ConteudoDoModelo conteudo = Conteudo([Item("CERTIFICADO", 0)]) with
        {
            ConjuntoDeDocumentos = ConteudosDoModeloDeTeste.Conjunto(new DocumentoExigidoDoModelo("INSCRICAO", "LAUDO_MEDICO", quando, new IndeferimentoDoDocumentoDoModelo(false, "REMOVE_VANTAGEM", []), RequisitosDoDocumentoDoModelo.AindaNaoEscolhidos)),
        };

        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, conteudo);

        modelo.IsSuccess.Should().Be(aceito, string.Join("; ", modelo.Errors.Select(static e => e.Error.Message)));
    }

    [Fact(DisplayName = "Pressuposto em branco é recusado")]
    public void PressupostoEmBranco_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(FinalidadeFormulario.Habilitacao, Conteudo([Item("CERTIFICADO", 0)], "  "));

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.PressupostoEmBranco);
    }
}
