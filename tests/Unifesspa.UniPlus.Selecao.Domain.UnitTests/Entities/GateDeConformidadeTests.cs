namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>
/// Contraprovas do gate de conformidade (ADR-0109 D5).
/// </summary>
/// <remarks>
/// O checklist vale para as <b>duas</b> transições que congelam. A retificação
/// também abre uma <c>VersaoConfiguracao</c> append-only e vinculante: congelar
/// configuração incompleta ali produz um documento irreparável, exatamente como
/// na publicação. Antes desta story, só <c>Publicar</c> avaliava.
/// </remarks>
public sealed class GateDeConformidadeTests
{
    private static readonly string HashFixo = new('a', 64);

    private static ReferenciaRegra Regra(string codigo, string hashSeed) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(hashSeed[0], 64)).Value!;

    private static ProcessoSeletivo ProcessoConforme(bool declararTaxa = true) =>
        ProcessoConformeFactory.Criar(declararTaxa);

    private static DadosEdital Dados() => ProcessoConformeFactory.Dados();

    private static VersaoConfiguracao Publicar(ProcessoSeletivo processo)
    {
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            Dados(),
            configuracaoCongeladaCanonica: "{}"u8.ToArray(),
            schemaVersion: "1.1",
            algoritmoHash: "canonical-json/sha256@v1",
            hashDocumento: HashFixo,
            atorUsuarioSub: "teste",
            TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
        return publicar.Value!;
    }

    [Fact(DisplayName = "PendenciaDeConformidade_ProcessoIncompleto — nomeia as dimensões faltantes")]
    public void PendenciaDeConformidade_ProcessoIncompleto()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Vazio", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        DomainError? pendencia = processo.PendenciaDeConformidade();

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        // Story #851 §3.5: "Etapas" deixou de ser item incondicional do checklist — um
        // processo sem prova (SiSU, CLASSIFICACAO-IMPORTADA) publica sem etapa. O que
        // permanece obrigatório incondicionalmente inclui "Cronograma de fases" (1..*).
        pendencia.Message.Should().Contain("Cronograma de fases").And.Contain("Classificação");
    }

    [Fact(DisplayName = "PendenciaDeConformidade_ProcessoConforme — não há pendência")]
    public void PendenciaDeConformidade_ProcessoConforme() =>
        ProcessoConforme().PendenciaDeConformidade().Should().BeNull();

    /// <summary>
    /// <b>Contraprova do CA-09.</b> É o furo que esta story fecha: antes,
    /// <c>Retificar</c> não avaliava conformidade nenhuma.
    /// </summary>
    [Fact(DisplayName = "Retificar_ProcessoNaoConforme_Recusa — a retificação avalia o MESMO gate da publicação")]
    public void Retificar_ProcessoNaoConforme_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        VersaoConfiguracao versaoAtual = Publicar(processo);

        // A configuração fica incompleta com o processo já publicado. Hoje não há
        // caminho de API para chegar aqui (todo Definir* é barrado pós-publicação),
        // mas o estado é alcançável por correção de dados no banco — e passará a ser
        // alcançável pela API quando a retificação puder alterar a configuração. O
        // gate tem de pegá-lo independentemente de como ele surgiu, e é o gate que
        // este teste exercita, não o caminho.
        //
        // A contraprova pelo caminho real — apagar a classificação no Postgres,
        // recarregar o agregado e retificar — está em RetificacaoConformidadeTests
        // (integração).
        typeof(ProcessoSeletivo)
            .GetProperty(nameof(ProcessoSeletivo.Classificacao))!
            .SetValue(processo, null);

        processo.Classificacao.Should().BeNull("pré-condição do teste");

        Result<VersaoConfiguracao> retificar = processo.Retificar(
            Dados(),
            versaoAtual,
            configuracaoCongeladaCanonica: "{}"u8.ToArray(),
            schemaVersion: "1.1",
            algoritmoHash: "canonical-json/sha256@v1",
            hashDocumento: HashFixo,
            atorUsuarioSub: "teste",
            motivo: "Correção do prazo",
            TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        retificar.IsFailure.Should().BeTrue(
            "retificar também congela uma versão append-only e vinculante — congelar configuração incompleta " +
            "ali produz um documento irreparável, exatamente como na publicação");

        retificar.Error!.Code.Should().Be(
            "ProcessoSeletivo.ConformidadeInsuficiente",
            "as duas transições recusam com o MESMO DomainError — fonte única (ADR-0109 D5)");
    }

    [Fact(DisplayName = "Publicar_ProcessoNaoConforme_Recusa — o gate da publicação continua valendo")]
    public void Publicar_ProcessoNaoConforme_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Vazio", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        Result<VersaoConfiguracao> publicar = processo.Publicar(
            Dados(),
            configuracaoCongeladaCanonica: "{}"u8.ToArray(),
            schemaVersion: "1.1",
            algoritmoHash: "canonical-json/sha256@v1",
            hashDocumento: HashFixo,
            atorUsuarioSub: "teste",
            TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        publicar.IsFailure.Should().BeTrue();
        publicar.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
    }

    [Fact(DisplayName =
        "PendenciaDeConformidade_SemTaxaDeclarada — processo conforme nas demais cinco dimensões ainda pendencia a taxa de inscrição (CA-01)")]
    public void PendenciaDeConformidade_SemTaxaDeclarada()
    {
        ProcessoSeletivo processo = ProcessoConforme(declararTaxa: false);

        DomainError? pendencia = processo.PendenciaDeConformidade();

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        pendencia.Message.Should().Contain("Taxa de inscrição e isenção");
    }

    [Fact(DisplayName =
        "Publicar_SemTaxaDeclarada_Recusa — processo conforme nas demais cinco dimensões é recusado por faltar declarar taxa (CA-01)")]
    public void Publicar_SemTaxaDeclarada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConforme(declararTaxa: false);

        Result<VersaoConfiguracao> publicar = processo.Publicar(
            Dados(),
            configuracaoCongeladaCanonica: "{}"u8.ToArray(),
            schemaVersion: "1.1",
            algoritmoHash: "canonical-json/sha256@v1",
            hashDocumento: HashFixo,
            atorUsuarioSub: "teste",
            TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        publicar.IsFailure.Should().BeTrue(
            "as outras cinco dimensões estão conformes — só a ausência de declaração de taxa pode estar bloqueando");
        publicar.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        publicar.Error!.Message.Should().Contain("Taxa de inscrição e isenção");
    }
}
