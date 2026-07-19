namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="ValidadorBaseLegalExigencias"/> (Story #554, PR #898, issue #549,
/// ADR-0074) — 5º item de <c>ProcessoSeletivo.AvaliarConformidade</c>.
/// </summary>
public sealed class ValidadorBaseLegalExigenciasTests
{
    private static DocumentoExigidoBaseLegal Base(TipoAbrangencia abrangencia, StatusBaseLegal status) =>
        DocumentoExigidoBaseLegal.Criar("Referência qualquer", abrangencia, status, null).Value!;

    private static DocumentoExigido Exigencia(
        bool obrigatorio, string? consequenciaIndeferimento, params DocumentoExigidoBaseLegal[] basesLegais) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId: Guid.CreateVersion7(),
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "IDENTIDADE",
            tipoDocumentoNome: "Documento de identidade",
            tipoDocumentoCategoria: "PESSOAL",
            aplicabilidade: Aplicabilidade.Geral,
            obrigatorio: obrigatorio,
            consequenciaIndeferimento: consequenciaIndeferimento,
            grupoSatisfacaoId: null,
            condicoes: [],
            basesLegais: basesLegais,
            idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!,
            tamanhoMaximoBytes: null).Value!;

    [Fact(DisplayName = "CA-03: processo sem exigência que determina resultado é trivialmente satisfeito (semântica vazia)")]
    public void TodasResolvidas_SemExigenciaQueDeterminaResultado_RetornaTrue()
    {
        DocumentoExigido facultativa = Exigencia(obrigatorio: false, consequenciaIndeferimento: null);

        ValidadorBaseLegalExigencias.TodasResolvidas([facultativa]).Should().BeTrue();
    }

    [Fact(DisplayName = "Nenhuma exigência configurada é trivialmente satisfeita (contraprova)")]
    public void TodasResolvidas_ColecaoVazia_RetornaTrue() =>
        ValidadorBaseLegalExigencias.TodasResolvidas([]).Should().BeTrue();

    [Fact(DisplayName = "CA-02: exigência obrigatória com base RESOLVIDO satisfaz o gate")]
    public void TodasResolvidas_ObrigatoriaComBaseResolvida_RetornaTrue()
    {
        DocumentoExigido exigencia = Exigencia(
            obrigatorio: true, consequenciaIndeferimento: null, Base(TipoAbrangencia.Federal, StatusBaseLegal.Resolvido));

        ValidadorBaseLegalExigencias.TodasResolvidas([exigencia]).Should().BeTrue();
    }

    [Fact(DisplayName = "CA-04: InternaEdital RESOLVIDO conta sozinha")]
    public void TodasResolvidas_InternaEditalResolvida_RetornaTrue()
    {
        DocumentoExigido exigencia = Exigencia(
            obrigatorio: true, consequenciaIndeferimento: null, Base(TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido));

        ValidadorBaseLegalExigencias.TodasResolvidas([exigencia]).Should().BeTrue();
    }

    [Fact(DisplayName = "Exigência que determina resultado por consequência (não por Obrigatorio) também exige base resolvida")]
    public void TodasResolvidas_ComConsequenciaSemBase_RetornaFalse()
    {
        DocumentoExigido exigencia = Exigencia(obrigatorio: false, consequenciaIndeferimento: "ELIMINA");

        ValidadorBaseLegalExigencias.TodasResolvidas([exigencia]).Should().BeFalse();
    }

    [Fact(DisplayName = "Exigência que determina resultado sem nenhuma base legal reprova o gate")]
    public void TodasResolvidas_SemBaseLegal_RetornaFalse()
    {
        DocumentoExigido exigencia = Exigencia(obrigatorio: true, consequenciaIndeferimento: null);

        ValidadorBaseLegalExigencias.TodasResolvidas([exigencia]).Should().BeFalse();
    }

    [Fact(DisplayName = "Exigência que determina resultado só com base PENDENTE reprova o gate")]
    public void TodasResolvidas_SoBasePendente_RetornaFalse()
    {
        DocumentoExigido exigencia = Exigencia(
            obrigatorio: true, consequenciaIndeferimento: null, Base(TipoAbrangencia.Federal, StatusBaseLegal.Pendente));

        ValidadorBaseLegalExigencias.TodasResolvidas([exigencia]).Should().BeFalse();
    }

    [Fact(DisplayName = "Mistura de PENDENTE e RESOLVIDO na mesma exigência satisfaz o gate (contraprova)")]
    public void TodasResolvidas_PendenteEResolvidaJuntas_RetornaTrue()
    {
        DocumentoExigido exigencia = Exigencia(
            obrigatorio: true,
            consequenciaIndeferimento: null,
            Base(TipoAbrangencia.Federal, StatusBaseLegal.Pendente),
            Base(TipoAbrangencia.Estadual, StatusBaseLegal.Resolvido));

        ValidadorBaseLegalExigencias.TodasResolvidas([exigencia]).Should().BeTrue();
    }

    [Fact(DisplayName = "Uma exigência sem base entre várias reprova o gate mesmo com as outras satisfeitas")]
    public void TodasResolvidas_UmaExigenciaSemBaseEntreVarias_RetornaFalse()
    {
        DocumentoExigido comBase = Exigencia(
            obrigatorio: true, consequenciaIndeferimento: null, Base(TipoAbrangencia.Federal, StatusBaseLegal.Resolvido));
        DocumentoExigido semBase = Exigencia(obrigatorio: true, consequenciaIndeferimento: null);

        ValidadorBaseLegalExigencias.TodasResolvidas([comBase, semBase]).Should().BeFalse();
    }
}
