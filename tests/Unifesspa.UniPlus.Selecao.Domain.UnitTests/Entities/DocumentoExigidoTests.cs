namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="DocumentoExigido"/> (Story #554, PR #895): fábrica, guard de
/// coerência de aplicabilidade (CA-01) e <see cref="DocumentoExigido.DeterminaResultado"/>.
/// </summary>
public sealed class DocumentoExigidoTests
{
    private static Result<DocumentoExigido> Exigencia(
        Aplicabilidade aplicabilidade = Aplicabilidade.Geral,
        bool obrigatorio = false,
        string? consequenciaIndeferimento = null,
        IReadOnlyList<CondicaoGatilho>? condicoes = null,
        IReadOnlyList<DocumentoExigidoBaseLegal>? basesLegais = null,
        IdadeMaximaEmissao? idadeMaximaEmissao = null,
        FormatosPermitidos? formatosPermitidos = null,
        int? tamanhoMaximoBytes = null) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId: Guid.CreateVersion7(),
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "LAUDO_MEDICO",
            tipoDocumentoNome: "Laudo médico",
            tipoDocumentoCategoria: "SAUDE",
            aplicabilidade: aplicabilidade,
            obrigatorio: obrigatorio,
            consequenciaIndeferimento: consequenciaIndeferimento,
            condicoes: condicoes ?? [],
            basesLegais: basesLegais ?? [],
            idadeMaximaEmissao: idadeMaximaEmissao,
            formatosPermitidos: formatosPermitidos ?? FormatosPermitidos.Criar(true, null).Value!,
            tamanhoMaximoBytes: tamanhoMaximoBytes);

    private static CondicaoGatilho CondicaoQualquer() => CondicaoGatilho.Criar(
        0, "SEXO", Operador.Igual, JsonSerializer.SerializeToElement("MASCULINO")).Value!;

    [Fact(DisplayName = "CA-01: aplicabilidade Nenhuma é recusada")]
    public void Criar_AplicabilidadeNenhuma_Recusa()
    {
        Result<DocumentoExigido> resultado = Exigencia(Aplicabilidade.Nenhuma);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.AplicabilidadeObrigatoria");
    }

    [Fact(DisplayName = "GERAL/CONDICIONAL válidos são aceitos")]
    public void Criar_ValoresValidos_Aceita()
    {
        Exigencia(Aplicabilidade.Geral).IsSuccess.Should().BeTrue();
        Exigencia(Aplicabilidade.Condicional).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Consequência de indeferimento fora do domínio é recusada")]
    public void Criar_ConsequenciaInvalida_Recusa()
    {
        Result<DocumentoExigido> resultado = Exigencia(consequenciaIndeferimento: "REPROVA_TUDO");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.ConsequenciaIndeferimentoInvalida");
    }

    [Theory(DisplayName = "Consequências válidas são aceitas")]
    [InlineData("ELIMINA")]
    [InlineData("RECLASSIFICA_AC")]
    [InlineData("REMOVE_VANTAGEM")]
    [InlineData("PENDENCIA_REENVIO")]
    public void Criar_ConsequenciaValida_Aceita(string consequencia) =>
        Exigencia(consequenciaIndeferimento: consequencia).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "CA-01: GERAL com condição viva é recusada")]
    public void GarantirCoerenciaAplicabilidade_GeralComCondicaoViva_Recusa()
    {
        DocumentoExigido exigencia = Exigencia(Aplicabilidade.Geral).Value!;

        DomainError? erro = exigencia.GarantirCoerenciaAplicabilidade(possuiCondicaoViva: true);

        erro.Should().NotBeNull();
        erro!.Code.Should().Be("DocumentoExigido.GeralComCondicao");
    }

    [Fact(DisplayName = "Story #892 (PR #896): Criar recusa GERAL com condição real (não mais parâmetro sintético)")]
    public void Criar_GeralComCondicaoReal_Recusa()
    {
        Result<DocumentoExigido> resultado = Exigencia(Aplicabilidade.Geral, condicoes: [CondicaoQualquer()]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.GeralComCondicao");
    }

    [Fact(DisplayName = "Story #892 (PR #896): Criar aceita CONDICIONAL com condição real e a anexa à coleção")]
    public void Criar_CondicionalComCondicaoReal_Aceita()
    {
        CondicaoGatilho condicao = CondicaoQualquer();
        DocumentoExigido exigencia = Exigencia(Aplicabilidade.Condicional, condicoes: [condicao]).Value!;

        exigencia.Condicoes.Should().ContainSingle(c => c.Id == condicao.Id);
        condicao.DocumentoExigidoId.Should().Be(exigencia.Id);
    }

    [Fact(DisplayName = "CA-01: GERAL sem condição viva é coerente (contraprova)")]
    public void GarantirCoerenciaAplicabilidade_GeralSemCondicaoViva_Aceita()
    {
        DocumentoExigido exigencia = Exigencia(Aplicabilidade.Geral).Value!;

        exigencia.GarantirCoerenciaAplicabilidade(possuiCondicaoViva: false).Should().BeNull();
    }

    [Fact(DisplayName = "CA-01: CONDICIONAL com condição viva é coerente (contraprova)")]
    public void GarantirCoerenciaAplicabilidade_CondicionalComCondicaoViva_Aceita()
    {
        DocumentoExigido exigencia = Exigencia(Aplicabilidade.Condicional).Value!;

        exigencia.GarantirCoerenciaAplicabilidade(possuiCondicaoViva: true).Should().BeNull();
    }

    [Fact(DisplayName = "DeterminaResultado: obrigatória determina resultado")]
    public void DeterminaResultado_Obrigatoria_RetornaTrue() =>
        Exigencia(obrigatorio: true).Value!.DeterminaResultado().Should().BeTrue();

    [Fact(DisplayName = "DeterminaResultado: com consequência determina resultado")]
    public void DeterminaResultado_ComConsequencia_RetornaTrue() =>
        Exigencia(consequenciaIndeferimento: "ELIMINA").Value!.DeterminaResultado().Should().BeTrue();

    [Fact(DisplayName = "DeterminaResultado: facultativa e sem consequência não determina resultado (contraprova)")]
    public void DeterminaResultado_FacultativaSemConsequencia_RetornaFalse() =>
        Exigencia().Value!.DeterminaResultado().Should().BeFalse();

    [Fact(DisplayName = "Consequência composta só de espaços é normalizada para null (não determina resultado)")]
    public void Criar_ConsequenciaSoDeEspacos_NormalizaParaNull()
    {
        DocumentoExigido exigencia = Exigencia(consequenciaIndeferimento: "   ").Value!;

        exigencia.ConsequenciaIndeferimento.Should().BeNull();
        exigencia.DeterminaResultado().Should().BeFalse();
    }

    // ── Story #554/issue #549 (PR #898) — base legal 1:N ──

    [Fact(DisplayName = "CA-06: BasesLegaisResolvidas exclui toda base PENDENTE")]
    public void BasesLegaisResolvidas_ExcluiPendente()
    {
        DocumentoExigidoBaseLegal pendente = DocumentoExigidoBaseLegal.Criar(
            "Referência pendente", TipoAbrangencia.Federal, StatusBaseLegal.Pendente, null).Value!;
        DocumentoExigidoBaseLegal resolvida = DocumentoExigidoBaseLegal.Criar(
            "Referência resolvida", TipoAbrangencia.Estadual, StatusBaseLegal.Resolvido, null).Value!;
        DocumentoExigido exigencia = Exigencia(basesLegais: [pendente, resolvida]).Value!;

        exigencia.BasesLegaisResolvidas().Should().ContainSingle().Which.Should().Be(resolvida);
    }

    [Fact(DisplayName = "BasesLegaisResolvidas retorna vazio quando só há PENDENTE (contraprova)")]
    public void BasesLegaisResolvidas_SoPendente_RetornaVazio()
    {
        DocumentoExigidoBaseLegal pendente = DocumentoExigidoBaseLegal.Criar(
            "Referência pendente", TipoAbrangencia.Federal, StatusBaseLegal.Pendente, null).Value!;
        DocumentoExigido exigencia = Exigencia(basesLegais: [pendente]).Value!;

        exigencia.BasesLegaisResolvidas().Should().BeEmpty();
    }

    // ── Story #916 — AplicavelPara ternário ──

    private static CondicaoGatilho CondicaoDe(string fato, Operador operador, string valorJson)
    {
        using JsonDocument documento = JsonDocument.Parse(valorJson);
        return CondicaoGatilho.Criar(0, fato, operador, documento.RootElement.Clone()).Value!;
    }

    [Fact(DisplayName = "AplicavelPara: GERAL é sempre Verdadeiro, gatilho nunca avaliado")]
    public void AplicavelPara_Geral_SempreVerdadeiro()
    {
        DocumentoExigido exigencia = Exigencia(Aplicabilidade.Geral).Value!;

        exigencia.AplicavelPara(new Dictionary<string, FatoResolvido>()).Should().Be(Ternario.Verdadeiro);
    }

    [Fact(DisplayName = "AplicavelPara: CONDICIONAL sem condição viva (zero cláusulas) é Falso — estrutural, não ausência de dado")]
    public void AplicavelPara_CondicionalSemCondicoes_Falso()
    {
        DocumentoExigido exigencia = Exigencia(Aplicabilidade.Condicional).Value!;

        exigencia.AplicavelPara(new Dictionary<string, FatoResolvido>()).Should().Be(Ternario.Falso);
    }

    [Fact(DisplayName = "AplicavelPara: CONDICIONAL cujo fato não está resolvido é Indeterminado, nunca Falso")]
    public void AplicavelPara_CondicionalComFatoAusente_Indeterminado()
    {
        DocumentoExigido exigencia = Exigencia(
            Aplicabilidade.Condicional, condicoes: [CondicaoDe("SEXO", Operador.Igual, "\"MASCULINO\"")]).Value!;

        exigencia.AplicavelPara(new Dictionary<string, FatoResolvido>()).Should().Be(Ternario.Indeterminado);
    }

    [Fact(DisplayName = "AplicavelPara: CONDICIONAL com fato resolvido avalia normalmente (Verdadeiro/Falso)")]
    public void AplicavelPara_CondicionalComFatoResolvido_AvaliaNormalmente()
    {
        DocumentoExigido exigencia = Exigencia(
            Aplicabilidade.Condicional, condicoes: [CondicaoDe("SEXO", Operador.Igual, "\"MASCULINO\"")]).Value!;

        Dictionary<string, FatoResolvido> fatos = new() { ["SEXO"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement("MASCULINO")) };
        exigencia.AplicavelPara(fatos).Should().Be(Ternario.Verdadeiro);

        Dictionary<string, FatoResolvido> fatosDivergentes = new() { ["SEXO"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement("FEMININO")) };
        exigencia.AplicavelPara(fatosDivergentes).Should().Be(Ternario.Falso);
    }

    // ── Story #916 — PodeAlcancarModalidade com DIFERENTE/NAO_EM ──

    [Fact(DisplayName = "PodeAlcancarModalidade: MODALIDADE DIFERENTE X alcança qualquer modalidade que não seja X")]
    public void PodeAlcancarModalidade_Diferente_AlcancaModalidadeDivergente()
    {
        DocumentoExigido exigencia = Exigencia(
            Aplicabilidade.Condicional, condicoes: [CondicaoDe("MODALIDADE", Operador.Diferente, "\"LB_PPI\"")]).Value!;

        exigencia.PodeAlcancarModalidade("AC").Should().BeTrue();
        exigencia.PodeAlcancarModalidade("LB_PPI").Should().BeFalse();
    }

    [Fact(DisplayName = "PodeAlcancarModalidade: MODALIDADE NAO_EM [...] alcança qualquer modalidade fora da lista")]
    public void PodeAlcancarModalidade_NaoEm_AlcancaModalidadeForaDaLista()
    {
        DocumentoExigido exigencia = Exigencia(
            Aplicabilidade.Condicional,
            condicoes: [CondicaoDe("MODALIDADE", Operador.NaoEm, "[\"LB_PPI\",\"LB_Q\"]")]).Value!;

        exigencia.PodeAlcancarModalidade("AC").Should().BeTrue();
        exigencia.PodeAlcancarModalidade("LB_PPI").Should().BeFalse();
        exigencia.PodeAlcancarModalidade("LB_Q").Should().BeFalse();
    }

    // ── Story #918 §4 — não-expiração do laudo de deficiência permanente sai de GATILHO,
    // sem campo especial: TIPO_DEFICIENCIA EM/NAO_EM {valores permanentes} já é inteiramente
    // coberto pelos operadores/avaliação ternária da PR2 (Story #916) + o vocabulário da PR1
    // (Story #917) — os 3 cenários abaixo usam SÓ código pré-existente dessas duas PRs,
    // provando que nenhuma produção nova é necessária para a exceção de idade.

    private static readonly string[] ValoresPermanentes = ["TEA", "AMPUTACAO"];

    private static DocumentoExigido ExigenciaSemIdadeGatilhadaPorTipoDeficiencia(Operador operador, string valorJson) =>
        Exigencia(Aplicabilidade.Condicional, condicoes: [CondicaoDe("TIPO_DEFICIENCIA", operador, valorJson)]).Value!;

    [Fact(DisplayName = "Story #918 §4: laudo PERMANENTE — exigência SEM idade (EM permanentes) é Verdadeiro, exigência COM idade (NAO_EM permanentes) é Falso")]
    public void AplicavelPara_TipoDeficienciaPermanente_ExcecaoDeIdadeResolveCorretamente()
    {
        string listaPermanentes = JsonSerializer.Serialize(ValoresPermanentes);
        DocumentoExigido semIdadeExigidaEmPermanentes =
            ExigenciaSemIdadeGatilhadaPorTipoDeficiencia(Operador.Em, listaPermanentes);
        DocumentoExigido comIdadeExigidaNaoEmPermanentes =
            ExigenciaSemIdadeGatilhadaPorTipoDeficiencia(Operador.NaoEm, listaPermanentes);

        Dictionary<string, FatoResolvido> fatos = new() { ["TIPO_DEFICIENCIA"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement("TEA")) };

        semIdadeExigidaEmPermanentes.AplicavelPara(fatos).Should().Be(Ternario.Verdadeiro,
            "TEA é permanente — a exigência SEM idade máxima (gatilhada por EM permanentes) se aplica");
        comIdadeExigidaNaoEmPermanentes.AplicavelPara(fatos).Should().Be(Ternario.Falso,
            "TEA é permanente — a exigência COM idade máxima (gatilhada por NAO_EM permanentes) NÃO se aplica");
    }

    [Fact(DisplayName = "Story #918 §4: laudo NÃO-PERMANENTE — o oposto: exigência SEM idade é Falso, exigência COM idade é Verdadeiro")]
    public void AplicavelPara_TipoDeficienciaNaoPermanente_ExcecaoDeIdadeResolveCorretamente()
    {
        string listaPermanentes = JsonSerializer.Serialize(ValoresPermanentes);
        DocumentoExigido semIdadeExigidaEmPermanentes =
            ExigenciaSemIdadeGatilhadaPorTipoDeficiencia(Operador.Em, listaPermanentes);
        DocumentoExigido comIdadeExigidaNaoEmPermanentes =
            ExigenciaSemIdadeGatilhadaPorTipoDeficiencia(Operador.NaoEm, listaPermanentes);

        Dictionary<string, FatoResolvido> fatos = new() { ["TIPO_DEFICIENCIA"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement("DEPRESSAO")) };

        semIdadeExigidaEmPermanentes.AplicavelPara(fatos).Should().Be(Ternario.Falso,
            "DEPRESSAO não é permanente — a exigência SEM idade máxima (gatilhada por EM permanentes) NÃO se aplica");
        comIdadeExigidaNaoEmPermanentes.AplicavelPara(fatos).Should().Be(Ternario.Verdadeiro,
            "DEPRESSAO não é permanente — a exigência COM idade máxima (gatilhada por NAO_EM permanentes) se aplica");
    }

    [Fact(DisplayName = "Story #918 §4: TIPO_DEFICIENCIA ausente — AMBAS as exigências ficam Indeterminado (fail-closed, nunca 'nenhuma se aplica')")]
    public void AplicavelPara_TipoDeficienciaAusente_AmbasIndeterminado()
    {
        string listaPermanentes = JsonSerializer.Serialize(ValoresPermanentes);
        DocumentoExigido semIdadeExigidaEmPermanentes =
            ExigenciaSemIdadeGatilhadaPorTipoDeficiencia(Operador.Em, listaPermanentes);
        DocumentoExigido comIdadeExigidaNaoEmPermanentes =
            ExigenciaSemIdadeGatilhadaPorTipoDeficiencia(Operador.NaoEm, listaPermanentes);

        Dictionary<string, FatoResolvido> semFatos = [];

        semIdadeExigidaEmPermanentes.AplicavelPara(semFatos).Should().Be(Ternario.Indeterminado);
        comIdadeExigidaNaoEmPermanentes.AplicavelPara(semFatos).Should().Be(Ternario.Indeterminado);
    }
}
