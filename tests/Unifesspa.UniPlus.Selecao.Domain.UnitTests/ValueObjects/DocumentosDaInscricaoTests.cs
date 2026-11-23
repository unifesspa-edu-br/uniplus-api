namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class DocumentosDaInscricaoTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("membro-1")]
    public void RecusasDosAusentes_AlternativasNaoEnviadas_IndicaPrimeiraExigenciaEOcorrencia(string? ocorrencia)
    {
        DocumentoExigido primeira = Documento("DECLARACAO", "Declaração");
        DocumentoExigido segunda = Documento("CERTIDAO", "Certidão");
        FieldError recusa = Documentos(ocorrencia, primeira, segunda).RecusasDosAusentes().Single();

        recusa.Field.Should().Be($"documentos.{primeira.Id}" + (ocorrencia is null ? string.Empty : $".{ocorrencia}"));
        recusa.Error.Code.Should().Be(InscricaoErrorCodes.DocumentoObrigatorioAusente);
        recusa.Error.Message.Should().Be("Envie os documentos obrigatórios conforme o edital: Declaração ou Certidão.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RecusasDosAusentes_AlternativaEmVerificacao_PedeEsperaSomenteNaMesmaOcorrencia(bool mesmaOcorrencia)
    {
        DocumentoExigido primeira = Documento("DECLARACAO", "Declaração");
        DocumentoExigido segunda = Documento("CERTIDAO", "Certidão");
        HashSet<(Guid, string?)> emVerificacao = [(segunda.Id, mesmaOcorrencia ? "membro-1" : "membro-2")];
        FieldError recusa = Documentos("membro-1", primeira, segunda).RecusasDosAusentes(emVerificacao).Single();

        recusa.Error.Code.Should().Be(mesmaOcorrencia ? InscricaoErrorCodes.DocumentoObrigatorioEmVerificacao : InscricaoErrorCodes.DocumentoObrigatorioAusente);
    }

    [Fact]
    public void RecusasDosAusentes_ExigenciaUnica_OrientaEnvioDoDocumento()
    {
        FieldError recusa = Documentos(null, Documento("CERTIDAO", "Certidão")).RecusasDosAusentes().Single();
        recusa.Error.Message.Should().Be("Envie o documento obrigatório: Certidão.");
    }

    [Fact]
    public void RecusasDosAusentes_SemAusencias_NaoRecusa() =>
        new DocumentosDaInscricao([], new Dictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>>(), []).RecusasDosAusentes().Should().BeEmpty();

    private static DocumentosDaInscricao Documentos(string? ocorrencia, params DocumentoExigido[] alternativas) =>
        new([], new Dictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>>(), [new DocumentoAusente(alternativas, ocorrencia)]);

    private static DocumentoExigido Documento(string codigo, string nome) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.NewGuid(), codigo, nome, "CAT", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(Guid.NewGuid(), null, null))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []))
            .ComIndeferimento(new IndeferimentoDoDocumento(true, null, []))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, null, null))
            .ComFatos(null)
            .Criar().Value!;
}
