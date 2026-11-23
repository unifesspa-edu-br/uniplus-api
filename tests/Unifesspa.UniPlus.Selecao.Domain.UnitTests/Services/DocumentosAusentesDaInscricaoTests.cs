namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os documentos obrigatórios que faltam no envio da inscrição (UNI-REQ-0028): a decisão parte do
/// situação da regra de comprovação.
/// </summary>
public sealed class DocumentosAusentesDaInscricaoTests
{
    private const string Familia = "FAMILIA";

    private static readonly FormatosPermitidos Qualquer = FormatosPermitidos.Criar(true, null).Value!;
    private static readonly Guid Fase = Guid.CreateVersion7();

    [Fact(DisplayName = "O documento obrigatório sem apresentação falta; com apresentação, não")]
    public void Obrigatorio_FaltaSoSemApresentacao()
    {
        DocumentoExigido laudo = Documento("Laudo");

        Ausentes([RegraDeComprovacao.CriarRegraDeDocumento(laudo, 0).Value!]).Should().ContainSingle()
            .Which.Alternativas.Should().ContainSingle().Which.Should().BeSameAs(laudo);
        Ausentes([RegraDeComprovacao.CriarRegraDeDocumento(laudo, 0).Value!], Apresentadas(laudo)).Should().BeEmpty();
    }

    [Fact(DisplayName = "O documento não obrigatório que falta não recusa")]
    public void NaoObrigatorio_NaoFalta() =>
        Ausentes([RegraDeComprovacao.CriarRegraDeDocumento(Documento("Laudo do atendimento", obrigatorio: false), 0).Value!]).Should().BeEmpty();

    [Fact(DisplayName = "A exigência cujos espaços o envio não registra (competência mensal) não recusa, e não torna a inscrição impossível de enviar")]
    public void ComChaveDeCompetencia_NaoRecusa() =>
        Ausentes([RegraDeComprovacao.CriarRegraDeDocumento(Documento("Contracheque"), 0, quantidadeMinima: 3, criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.CompetenciaMensal, dataReferencia: new DateOnly(2026, 10, 1)).Value!])
            .Should().BeEmpty();

    [Fact(DisplayName = "O documento que não é da inscrição não falta no envio dela")]
    public void DeOutraFinalidade_NaoFalta() =>
        Ausentes([RegraDeComprovacao.CriarRegraDeDocumento(Documento("Comprovante da habilitação", finalidade: FinalidadeFormulario.Habilitacao), 0).Value!]).Should().BeEmpty();

    [Fact(DisplayName = "Num grupo um ou outro, uma alternativa atendida basta: a pendente ao lado dela não falta")]
    public void QuantidadeMinimaDeItens_AtendidoPorUmaAlternativa_NaoFalta()
    {
        DocumentoExigido laudo = Documento("Laudo"), atestado = Documento("Atestado");

        Ausentes([QuantidadeMinimaDeItens(0, laudo, atestado)], Apresentadas(atestado)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Num grupo um ou outro sem nenhuma alternativa atendida, falta uma recusa só, com as duas alternativas")]
    public void QuantidadeMinimaDeItens_SemNenhuma_FaltaUmaVez()
    {
        DocumentoExigido laudo = Documento("Laudo"), atestado = Documento("Atestado");

        Ausentes([QuantidadeMinimaDeItens(0, laudo, atestado)]).Should().ContainSingle()
            .Which.Alternativas.Should().BeEquivalentTo([laudo, atestado]);
    }

    [Fact(DisplayName = "Num grupo um ou outro cujas alternativas não são obrigatórias, a falta não recusa")]
    public void QuantidadeMinimaDeItens_SemObrigatoria_NaoFalta() =>
        Ausentes([QuantidadeMinimaDeItens(0, Documento("Laudo", obrigatorio: false), Documento("Atestado", obrigatorio: false))]).Should().BeEmpty();

    [Fact(DisplayName = "Em um grupo de todos os itens, cada documento ausente é exigido")]
    public void TodosOsItens_FaltaCadaUm()
    {
        DocumentoExigido rg = Documento("RG"), cpf = Documento("CPF");
        RegraDeComprovacao conjunto = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.TodosOsItens, 0, null, null, [], [RegraDeComprovacao.CriarRegraDeDocumento(rg, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(cpf, 1).Value!]).Value!;

        Ausentes([conjunto], Apresentadas(rg)).Should().ContainSingle().Which.Alternativas.Should().Equal(cpf);
    }

    [Fact(DisplayName = "Um grupo impossível não esconde o documento obrigatório dos outros itens")]
    public void TodosOsItensImpossivel_NaoEscondeIrmao()
    {
        DocumentoExigido laudo = Documento("Laudo"), rg = Documento("RG");
        // Só o RG se aplica; o grupo exige dois itens e o laudo continua faltando.
        DocumentoExigido cnhNaoAplica = Documento("CNH condicional", gatilhoFalso: true);
        RegraDeComprovacao ouImpossivel = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.QuantidadeMinimaDeItens, 1, 2, null, [], [RegraDeComprovacao.CriarRegraDeDocumento(rg, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(cnhNaoAplica, 1).Value!]).Value!;
        RegraDeComprovacao regra = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.TodosOsItens, 0, null, null, [], [RegraDeComprovacao.CriarRegraDeDocumento(laudo, 0).Value!, ouImpossivel]).Value!;

        Ausentes([regra]).Select(static a => a.Alternativas[0].TipoDocumentoNome).Should().Contain("Laudo");
    }

    [Fact(DisplayName = "No grupo repetível a falta é de cada ocorrência")]
    public void Repetivel_FaltaPorOcorrencia()
    {
        DocumentoExigido renda = Documento("Comprovante de renda");
        RegraDeComprovacao regra = RegraDeComprovacao.CriarRegraDeDocumento(renda, 0, grupoDoFormularioParaRepeticao: Familia).Value!;

        IReadOnlyList<DocumentoAusente> ausentes = Ausentes(
            [regra], Apresentadas((renda, "m1")), ocorrencias: ["m1", "m2"]);

        ausentes.Should().ContainSingle().Which.OcorrenciaId.Should().Be("m2");
    }

    [Fact(DisplayName = "No grupo um ou outro repetível, o atestado de uma ocorrência não atende a outra")]
    public void QuantidadeMinimaDeItensRepetivel_DecideACadaOcorrencia()
    {
        DocumentoExigido rg = Documento("RG"), cnh = Documento("CNH");
        RegraDeComprovacao grupo = RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.QuantidadeMinimaDeItens, 0, 1, null, [], [RegraDeComprovacao.CriarRegraDeDocumento(rg, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(cnh, 1).Value!], grupoDoFormularioParaRepeticao: Familia).Value!;

        IReadOnlyList<DocumentoAusente> ausentes = Ausentes(
            [grupo], Apresentadas((cnh, "m1")), ocorrencias: ["m1", "m2"]);

        DocumentoAusente falta = ausentes.Should().ContainSingle().Subject;
        falta.OcorrenciaId.Should().Be("m2");
        falta.Alternativas.Should().BeEquivalentTo([rg, cnh]);
    }

    private static DocumentoExigido Documento(string nome, bool obrigatorio = true, FinalidadeFormulario finalidade = FinalidadeFormulario.Inscricao, bool gatilhoFalso = false) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), nome.ToUpperInvariant().Replace(' ', '_'), nome, "CAT", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(Fase, null, finalidade))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(gatilhoFalso ? Aplicabilidade.Condicional : Aplicabilidade.Geral, gatilhoFalso ? [CondicaoGatilho.Criar(0, "NUNCA", Operador.Igual, System.Text.Json.JsonSerializer.SerializeToElement(true)).Value!] : []))
            .ComIndeferimento(new IndeferimentoDoDocumento(obrigatorio, null, []))
            .ComRequisitos(new RequisitosDoDocumento(Qualquer, null, null, null, null))
            .ComFatos(null)
            .Criar().Value!;

    private static RegraDeComprovacao QuantidadeMinimaDeItens(int ordem, params DocumentoExigido[] alternativas) =>
        RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.QuantidadeMinimaDeItens, ordem, 1, null, [], [.. alternativas.Select((d, i) => RegraDeComprovacao.CriarRegraDeDocumento(d, i).Value!)]).Value!;

    private static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> Apresentadas(params DocumentoExigido[] documentos) =>
        Apresentadas([.. documentos.Select(static d => (d, (string?)null))]);

    private static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> Apresentadas(params (DocumentoExigido Documento, string? Ocorrencia)[] enviados) =>
        enviados.GroupBy(static e => e.Documento.Id).ToDictionary(
            static g => g.Key,
            static g => (IReadOnlyList<ApresentacaoDocumento>)[.. g.Select(static e => new ApresentacaoDocumento(Guid.CreateVersion7(), e.Ocorrencia, e.Ocorrencia))]);

    private static IReadOnlyList<DocumentoAusente> Ausentes(
        IReadOnlyList<RegraDeComprovacao> regrasSemGrupo,
        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>? apresentacoes = null,
        string[]? ocorrencias = null)
    {
        RegrasDeComprovacaoDaVersao regrasDaVersao = RegrasDeComprovacaoDaVersao.DaConfiguracaoPublicada(regrasSemGrupo);
        Dictionary<string, IReadOnlyList<OcorrenciaDoGrupo>> instancias = ocorrencias is null
            ? []
            : new() { [Familia] = [.. ocorrencias.Select(static o => new OcorrenciaDoGrupo(o, new Dictionary<string, FatoResolvido>()))] };
        Kernel.Results.Result<ResultadoDaComprovacao> resultado = AvaliadorDaComprovacao.Resolver(
            regrasDaVersao, new Dictionary<string, FatoResolvido> { ["NUNCA"] = FatoResolvido.Resolvido(System.Text.Json.JsonSerializer.SerializeToElement(false)) }, apresentacoes ?? [], instancias);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        return DocumentosAusentesDaInscricao.Apurar(regrasDaVersao, resultado.Value!);
    }
}
