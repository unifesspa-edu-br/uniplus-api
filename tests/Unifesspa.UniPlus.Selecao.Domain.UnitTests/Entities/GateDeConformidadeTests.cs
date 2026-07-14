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

    private static ProcessoSeletivo ProcessoConforme()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Gate", TipoProcesso.SiSU);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirDistribuicaoVagas([
            ConfiguracaoDistribuicaoVagas.Criar(
                ofertaCursoOrigemId: Guid.CreateVersion7(),
                voBase: 40,
                pr: 1m,
                regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, "a"),
                referenciaDemografica: null,
                modalidades: [
                    ModalidadeSelecionada.Criar(
                        modalidadeOrigemId: Guid.CreateVersion7(),
                        codigo: "AC",
                        descricao: null,
                        naturezaLegal: NaturezaLegalModalidade.Ampla,
                        composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
                        composicaoOrigemCodigo: null,
                        regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
                        remanejamentoDestino: null,
                        remanejamentoPar: null,
                        remanejamentoFallback: null,
                        criteriosCumulativos: [],
                        acaoQuandoIndeferido: null,
                        baseLegal: "Res. Unifesspa 532/2021").Value!,
                ]).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, "b"),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "c"),
            nOpcoesAlocacao: 1,
            regrasEliminacao: []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static DadosEdital Dados() => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateOnly(2026, 1, 1),
        periodoInscricaoFim: new DateOnly(2026, 1, 31),
        documentoEditalId: Guid.CreateVersion7()).Value!;

    private static VersaoConfiguracao Publicar(ProcessoSeletivo processo)
    {
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            Dados(),
            configuracaoCongeladaCanonica: "{}"u8.ToArray(),
            schemaVersion: "1.1",
            algoritmoHash: "canonical-json/sha256@v1",
            hashDocumento: HashFixo,
            atorUsuarioSub: "teste",
            TimeProvider.System);

        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
        return publicar.Value!;
    }

    [Fact(DisplayName = "PendenciaDeConformidade_ProcessoIncompleto — nomeia as dimensões faltantes")]
    public void PendenciaDeConformidade_ProcessoIncompleto()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Vazio", TipoProcesso.SiSU);

        DomainError? pendencia = processo.PendenciaDeConformidade();

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        pendencia.Message.Should().Contain("Etapas").And.Contain("Classificação");
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
            TimeProvider.System);

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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Vazio", TipoProcesso.SiSU);

        Result<VersaoConfiguracao> publicar = processo.Publicar(
            Dados(),
            configuracaoCongeladaCanonica: "{}"u8.ToArray(),
            schemaVersion: "1.1",
            algoritmoHash: "canonical-json/sha256@v1",
            hashDocumento: HashFixo,
            atorUsuarioSub: "teste",
            TimeProvider.System);

        publicar.IsFailure.Should().BeTrue();
        publicar.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
    }
}
