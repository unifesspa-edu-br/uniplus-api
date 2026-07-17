namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="ProcessoSeletivo.DefinirDocumentosExigidos"/> (Story #554,
/// PR-a): substituição integral e pertencimento da fase ao cronograma do MESMO
/// processo (§2 da issue #547).
/// </summary>
public sealed class ProcessoSeletivoDocumentosExigidosTests
{
    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PS Documentos Exigidos", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);

    private static FaseCronograma Fase(int ordem, string codigo) => FaseCronograma.Criar(
        ordem,
        Guid.CreateVersion7(),
        codigo,
        "CEPS",
        OrigemDataFase.Delegada,
        agrupaEtapas: false,
        permiteComplementacao: false,
        produzResultado: false,
        resultadoDefinitivo: false,
        coletaInscricao: false,
        inicio: null,
        fim: null,
        atoProduzidoCodigo: null,
        atoProduzidoEfeitoIrreversivel: false,
        bancasRequeridas: [],
        regraRecurso: null).Value!;

    private static DocumentoExigido Exigencia(Guid exigidoNaFaseId, Aplicabilidade aplicabilidade = Aplicabilidade.Geral) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "IDENTIDADE",
            tipoDocumentoNome: "Documento de identidade",
            tipoDocumentoCategoria: "PESSOAL",
            aplicabilidade,
            obrigatorio: false,
            consequenciaIndeferimento: null,
            grupoSatisfacaoId: null,
            condicoes: []).Value!;

    [Fact(DisplayName = "Fase que não pertence ao cronograma do processo é recusada")]
    public void DefinirDocumentosExigidos_FaseDeOutroProcesso_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DocumentoExigido exigencia = Exigencia(Guid.CreateVersion7());

        Result resultado = processo.DefinirDocumentosExigidos([exigencia], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.FaseNaoPertenceAoProcesso");
    }

    [Fact(DisplayName = "Fase viva do cronograma do próprio processo é aceita")]
    public void DefinirDocumentosExigidos_FaseDoProprioProcesso_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(1, "INSCRICAO");
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DocumentoExigido exigencia = Exigencia(fase.Id);
        Result resultado = processo.DefinirDocumentosExigidos([exigencia], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().ContainSingle(d => d.Id == exigencia.Id);
        exigencia.ProcessoSeletivoId.Should().Be(processo.Id);
    }

    [Fact(DisplayName = "Coleção vazia é um estado válido — nenhuma exigência configurada")]
    public void DefinirDocumentosExigidos_ListaVazia_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirDocumentosExigidos([], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "PUT idempotente: reenviar o mesmo payload substitui a coleção por inteiro, sem duplicar")]
    public void DefinirDocumentosExigidos_ReenviarMesmoPayload_NaoDuplica()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(1, "INSCRICAO");
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DocumentoExigido primeiraChamada = Exigencia(fase.Id);
        processo.DefinirDocumentosExigidos([primeiraChamada], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DocumentoExigido segundaChamada = Exigencia(fase.Id);
        Result resultado = processo.DefinirDocumentosExigidos([segundaChamada], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().HaveCount(1);
        processo.DocumentosExigidos.Should().ContainSingle(d => d.Id == segundaChamada.Id);
    }

    [Fact(DisplayName = "CA-04 (guard parcial): redefinir o cronograma com exigência viva configurada é recusado")]
    public void DefinirCronogramaFases_ComExigenciaViva_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(1, "INSCRICAO");
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirDocumentosExigidos([Exigencia(fase.Id)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirCronogramaFases([Fase(1, "INSCRICAO")], [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue(
            "toda chamada aqui recria as fases com Id novo — sem a guarda, a FK Restrict de " +
            "documentos_exigidos.exigido_na_fase_id estouraria DbUpdateException não tratada");
        resultado.Error!.Code.Should().Be("FaseCronograma.ReferenciadaPorExigenciaViva");
    }

    [Fact(DisplayName = "Redefinir o cronograma sem exigência viva é aceito (contraprova)")]
    public void DefinirCronogramaFases_SemExigenciaViva_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCronogramaFases([Fase(1, "INSCRICAO")], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirCronogramaFases([Fase(1, "INSCRICAO")], [], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }
}
