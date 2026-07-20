namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura dos gates de publicação/escrita ESTENDIDOS ao nó de grupo <c>OU</c>/<c>N-de</c>
/// com consequência própria (Story #920, tasks §1.5a): base legal ≥1 <c>RESOLVIDO</c>,
/// <c>REMOVE_VANTAGEM</c> exige vantagem viva, e <c>PENDENCIA_REENVIO</c>×<c>PermiteComplementacao</c>
/// forward (na escrita da árvore) e reverso (ao redefinir o cronograma) — os mesmos quatro
/// gates que já existiam para <see cref="DocumentoExigido"/> (folha), agora também para
/// <see cref="NoExigencia"/> do tipo <see cref="TipoNo.GrupoOu"/>.
/// </summary>
public sealed class ProcessoSeletivoNoExigenciaGatesTests
{
    private static readonly FormatosPermitidos Qualquer = FormatosPermitidos.Criar(true, null).Value!;

    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PS Gates de Grupo", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);

    private static FaseCronograma NovaFase(Guid faseCanonicaOrigemId, bool permiteComplementacao) => FaseCronograma.Criar(
        1, faseCanonicaOrigemId, "INSCRICAO", "CEPS", OrigemDataFase.Delegada,
        agrupaEtapas: false, permiteComplementacao: permiteComplementacao, produzResultado: false,
        resultadoDefinitivo: false, coletaInscricao: false, inicio: null, fim: null,
        atoProduzidoCodigo: null, atoProduzidoEfeitoIrreversivel: false,
        bancasRequeridas: [], regraRecurso: null).Value!;

    private static DocumentoExigido DocumentoQualquer(Guid faseId) => DocumentoExigido.Criar(
        faseId, Guid.CreateVersion7(), "COD", "Nome", "CAT", Aplicabilidade.Geral,
        obrigatorio: false, consequenciaIndeferimento: null, [], [], null, Qualquer, null).Value!;

    private static NoExigenciaBaseLegal BaseLegal(StatusBaseLegal status) =>
        NoExigenciaBaseLegal.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, status, null).Value!;

    [Fact(DisplayName = "Grupo com REMOVE_VANTAGEM sem vantagem viva bloqueia PendenciaPreCanonicalizacao")]
    public void Grupo_RemoveVantagemSemVantagemViva_Bloqueia()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = NovaFase(Guid.CreateVersion7(), permiteComplementacao: true);
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        NoExigencia folha = NoExigencia.CriarFolha(DocumentoQualquer(fase.Id), 0).Value!;
        NoExigencia grupo = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 1, "REMOVE_VANTAGEM", [BaseLegal(StatusBaseLegal.Resolvido)], [folha]).Value!;

        processo.DefinirDocumentosExigidos([grupo], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DomainError? pendencia = processo.PendenciaPreCanonicalizacao();

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("NoExigencia.RemoveVantagemSemVantagemViva");
    }

    [Fact(DisplayName = "Grupo com consequência e base legal só PENDENTE reprova o checklist de conformidade")]
    public void Grupo_ComConsequenciaEBaseLegalPendente_ReprovaConformidade()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = NovaFase(Guid.CreateVersion7(), permiteComplementacao: true);
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        NoExigencia folha = NoExigencia.CriarFolha(DocumentoQualquer(fase.Id), 0).Value!;
        NoExigencia grupo = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 1, "ELIMINA", [BaseLegal(StatusBaseLegal.Pendente)], [folha]).Value!;

        processo.DefinirDocumentosExigidos([grupo], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade()
            .Should().Contain(item => item.Item == "Base legal das exigências documentais" && !item.Ok);
    }

    [Fact(DisplayName = "Grupo com consequência e base legal RESOLVIDO aprova o checklist de conformidade")]
    public void Grupo_ComConsequenciaEBaseLegalResolvida_AprovaConformidade()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = NovaFase(Guid.CreateVersion7(), permiteComplementacao: true);
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        NoExigencia folha = NoExigencia.CriarFolha(DocumentoQualquer(fase.Id), 0).Value!;
        NoExigencia grupo = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 1, "ELIMINA", [BaseLegal(StatusBaseLegal.Resolvido)], [folha]).Value!;

        processo.DefinirDocumentosExigidos([grupo], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade()
            .Should().Contain(item => item.Item == "Base legal das exigências documentais" && item.Ok);
    }

    [Fact(DisplayName = "PENDENCIA_REENVIO em grupo numa fase sem PermiteComplementacao é recusado (forward)")]
    public void Grupo_PendenciaReenvioSemComplementacao_RecusaForward()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = NovaFase(Guid.CreateVersion7(), permiteComplementacao: false);
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        NoExigencia folha = NoExigencia.CriarFolha(DocumentoQualquer(fase.Id), 0).Value!;
        NoExigencia grupo = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, 1, "PENDENCIA_REENVIO", [], [folha]).Value!;

        Result resultado = processo.DefinirDocumentosExigidos([grupo], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.PendenciaReenvioExigeComplementacao");
    }

    [Fact(DisplayName = "Retirar PermiteComplementacao de fase referenciada por grupo PENDENCIA_REENVIO é recusado (reverso)")]
    public void Grupo_RetirarComplementacaoDeFaseComPendenciaReenvio_RecusaReverso()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid faseCanonicaOrigemId = Guid.CreateVersion7();
        FaseCronograma faseComComplementacao = NovaFase(faseCanonicaOrigemId, permiteComplementacao: true);
        processo.DefinirCronogramaFases([faseComComplementacao], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        NoExigencia folha = NoExigencia.CriarFolha(DocumentoQualquer(faseComComplementacao.Id), 0).Value!;
        NoExigencia grupo = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, 1, "PENDENCIA_REENVIO", [], [folha]).Value!;
        processo.DefinirDocumentosExigidos([grupo], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma faseSemComplementacao = NovaFase(faseCanonicaOrigemId, permiteComplementacao: false);
        Result resultado = processo.DefinirCronogramaFases([faseSemComplementacao], [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.PendenciaReenvioExigeComplementacao");
    }

    // ── Fail-closed de cardinalidade qualificada (Story #921, achado de revisão do PR #937):
    // o snapshot canônico ainda não congela quantidadeMinima/chaveDistincao de NoExigencia —
    // publicar uma folha solteira com cardinalidade não-padrão perderia a exigência
    // silenciosamente, mesmo raciocínio do fail-closed de grupo E/OU (Story #920, PR 1/4). ──

    [Fact(DisplayName = "Folha solteira com quantidadeMinima > 1 bloqueia PendenciaPreCanonicalizacao")]
    public void FolhaSolteira_QuantidadeMinimaMaiorQueUm_Bloqueia()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = NovaFase(Guid.CreateVersion7(), permiteComplementacao: true);
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        NoExigencia folha = NoExigencia.CriarFolha(DocumentoQualquer(fase.Id), 0, quantidadeMinima: 2).Value!;
        processo.DefinirDocumentosExigidos([folha], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DomainError? pendencia = processo.PendenciaPreCanonicalizacao();

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("NoExigencia.CardinalidadeQualificadaAindaNaoSuportada");
    }

    [Fact(DisplayName = "Folha solteira com chaveDistincao bloqueia PendenciaPreCanonicalizacao mesmo com quantidadeMinima padrão")]
    public void FolhaSolteira_ComChaveDistincao_Bloqueia()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = NovaFase(Guid.CreateVersion7(), permiteComplementacao: true);
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        NoExigencia folha = NoExigencia.CriarFolha(
            DocumentoQualquer(fase.Id), 0, quantidadeMinima: 1,
            chaveDistincao: ChaveDistincao.Ocorrencia).Value!;
        processo.DefinirDocumentosExigidos([folha], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DomainError? pendencia = processo.PendenciaPreCanonicalizacao();

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("NoExigencia.CardinalidadeQualificadaAindaNaoSuportada");
    }

    [Fact(DisplayName = "Folha solteira com cardinalidade padrão (1, sem chave) continua publicável")]
    public void FolhaSolteira_CardinalidadePadrao_NaoBloqueia()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = NovaFase(Guid.CreateVersion7(), permiteComplementacao: true);
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        NoExigencia folha = NoExigencia.CriarFolha(DocumentoQualquer(fase.Id), 0).Value!;
        processo.DefinirDocumentosExigidos([folha], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DomainError? pendencia = processo.PendenciaPreCanonicalizacao();

        pendencia.Should().BeNull();
    }
}
