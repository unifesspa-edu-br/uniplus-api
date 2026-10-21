namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os documentos obrigatórios que faltam no envio da inscrição (UNI-REQ-0028): a decisão parte do
/// estado do nó da árvore, e não da folha.
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

        Ausentes([NoExigencia.CriarFolha(laudo, 0).Value!]).Should().ContainSingle()
            .Which.Alternativas.Should().ContainSingle().Which.Should().BeSameAs(laudo);
        Ausentes([NoExigencia.CriarFolha(laudo, 0).Value!], Apresentadas(laudo)).Should().BeEmpty();
    }

    [Fact(DisplayName = "O documento não obrigatório que falta não recusa")]
    public void NaoObrigatorio_NaoFalta() =>
        Ausentes([NoExigencia.CriarFolha(Documento("Laudo do atendimento", obrigatorio: false), 0).Value!]).Should().BeEmpty();

    [Fact(DisplayName = "A exigência cujos espaços o envio não registra (competência mensal) não recusa, e não torna a inscrição impossível de enviar")]
    public void ComChaveDeCompetencia_NaoRecusa() =>
        Ausentes([NoExigencia.CriarFolha(Documento("Contracheque"), 0, quantidadeMinima: 3, chaveDistincao: ChaveDistincao.CompetenciaMensal, dataReferencia: new DateOnly(2026, 10, 1)).Value!])
            .Should().BeEmpty();

    [Fact(DisplayName = "O documento que não é da inscrição não falta no envio dela")]
    public void DeOutraFinalidade_NaoFalta() =>
        Ausentes([NoExigencia.CriarFolha(Documento("Comprovante da habilitação", finalidade: FinalidadeFormulario.Habilitacao), 0).Value!]).Should().BeEmpty();

    [Fact(DisplayName = "Num grupo um ou outro, uma alternativa atendida basta: a pendente ao lado dela não falta")]
    public void GrupoOu_AtendidoPorUmaAlternativa_NaoFalta()
    {
        DocumentoExigido laudo = Documento("Laudo"), atestado = Documento("Atestado");

        Ausentes([GrupoOu(0, laudo, atestado)], Apresentadas(atestado)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Num grupo um ou outro sem nenhuma alternativa atendida, falta uma recusa só, com as duas alternativas")]
    public void GrupoOu_SemNenhuma_FaltaUmaVez()
    {
        DocumentoExigido laudo = Documento("Laudo"), atestado = Documento("Atestado");

        Ausentes([GrupoOu(0, laudo, atestado)]).Should().ContainSingle()
            .Which.Alternativas.Should().BeEquivalentTo([laudo, atestado]);
    }

    [Fact(DisplayName = "Num grupo um ou outro cujas alternativas não são obrigatórias, a falta não recusa")]
    public void GrupoOu_SemObrigatoria_NaoFalta() =>
        Ausentes([GrupoOu(0, Documento("Laudo", obrigatorio: false), Documento("Atestado", obrigatorio: false))]).Should().BeEmpty();

    [Fact(DisplayName = "Num grupo E, falta cada documento que falta")]
    public void GrupoE_FaltaCadaUm()
    {
        DocumentoExigido rg = Documento("RG"), cpf = Documento("CPF");
        NoExigencia conjunto = NoExigencia.CriarGrupo(
            TipoNo.GrupoE, 0, null, null, [], [NoExigencia.CriarFolha(rg, 0).Value!, NoExigencia.CriarFolha(cpf, 1).Value!]).Value!;

        Ausentes([conjunto], Apresentadas(rg)).Should().ContainSingle().Which.Alternativas.Should().Equal(cpf);
    }

    [Fact(DisplayName = "No grupo repetível a falta é de cada ocorrência")]
    public void Repetivel_FaltaPorOcorrencia()
    {
        DocumentoExigido renda = Documento("Comprovante de renda");
        NoExigencia arvore = NoExigencia.CriarFolha(renda, 0, repetePorEntidade: Familia).Value!;

        IReadOnlyList<DocumentoAusente> ausentes = Ausentes(
            [arvore], Apresentadas((renda, "m1")), ocorrencias: ["m1", "m2"]);

        ausentes.Should().ContainSingle().Which.OcorrenciaId.Should().Be("m2");
    }

    [Fact(DisplayName = "No grupo um ou outro repetível, o atestado de uma ocorrência não atende a outra")]
    public void GrupoOuRepetivel_DecideACadaOcorrencia()
    {
        DocumentoExigido rg = Documento("RG"), cnh = Documento("CNH");
        NoExigencia grupo = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 1, null, [], [NoExigencia.CriarFolha(rg, 0).Value!, NoExigencia.CriarFolha(cnh, 1).Value!], repetePorEntidade: Familia).Value!;

        IReadOnlyList<DocumentoAusente> ausentes = Ausentes(
            [grupo], Apresentadas((cnh, "m1")), ocorrencias: ["m1", "m2"]);

        DocumentoAusente falta = ausentes.Should().ContainSingle().Subject;
        falta.OcorrenciaId.Should().Be("m2");
        falta.Alternativas.Should().BeEquivalentTo([rg, cnh]);
    }

    private static DocumentoExigido Documento(string nome, bool obrigatorio = true, FinalidadeFormulario finalidade = FinalidadeFormulario.Inscricao) =>
        DocumentoExigido.Criar(
            Fase, Guid.CreateVersion7(), nome.ToUpperInvariant().Replace(' ', '_'), nome, "CAT",
            Aplicabilidade.Geral, obrigatorio, consequenciaIndeferimento: null, [], [], null, Qualquer, null, finalidade: finalidade).Value!;

    private static NoExigencia GrupoOu(int ordem, params DocumentoExigido[] alternativas) =>
        NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, ordem, 1, null, [], [.. alternativas.Select((d, i) => NoExigencia.CriarFolha(d, i).Value!)]).Value!;

    private static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> Apresentadas(params DocumentoExigido[] documentos) =>
        Apresentadas([.. documentos.Select(static d => (d, (string?)null))]);

    private static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> Apresentadas(params (DocumentoExigido Documento, string? Ocorrencia)[] enviados) =>
        enviados.GroupBy(static e => e.Documento.Id).ToDictionary(
            static g => g.Key,
            static g => (IReadOnlyList<ApresentacaoDocumento>)[.. g.Select(static e => new ApresentacaoDocumento(Guid.CreateVersion7(), e.Ocorrencia, e.Ocorrencia))]);

    private static IReadOnlyList<DocumentoAusente> Ausentes(
        IReadOnlyList<NoExigencia> raizes,
        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>? apresentacoes = null,
        string[]? ocorrencias = null)
    {
        ArvoreExigenciasCongelada arvore = ArvoreExigenciasCongelada.DeGrafoReidratado(raizes);
        Dictionary<string, IReadOnlyList<InstanciaEntidade>> instancias = ocorrencias is null
            ? []
            : new() { [Familia] = [.. ocorrencias.Select(static o => new InstanciaEntidade(o, new Dictionary<string, FatoResolvido>()))] };
        Kernel.Results.Result<ResultadoResolucaoArvore> resultado = ResolvedorArvoreSatisfacao.Resolver(
            arvore, new Dictionary<string, FatoResolvido>(), apresentacoes ?? [], instancias);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        return DocumentosAusentesDaInscricao.Apurar(arvore, resultado.Value!);
    }
}
