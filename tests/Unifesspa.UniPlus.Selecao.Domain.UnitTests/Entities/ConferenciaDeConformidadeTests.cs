namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// Contraprovas da conferência de conformidade (ADR-0109 D5).
/// </summary>
/// <remarks>
/// O checklist vale para as <b>duas</b> transições que congelam. A retificação
/// também abre uma <c>VersaoConfiguracao</c> append-only e vinculante: congelar
/// configuração incompleta ali produz um documento irreparável, exatamente como
/// na publicação.
/// </remarks>
public sealed class ConferenciaDeConformidadeTests
{
    private static readonly string HashFixo = new('a', 64);

    private static ProcessoSeletivo ProcessoConforme(bool declararTaxa = true) =>
        ProcessoConformeFactory.Criar(declararTaxa);

    private static DadosEdital Dados() => ProcessoConformeFactory.Dados();

    private static Result<VersaoConfiguracao> TentarPublicar(ProcessoSeletivo processo) => processo.Publicar(
        new CenarioDeCongelamento { Dados = Dados(), Documento = new DocumentoCongelado("{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo), AtorUsuarioContaId = "teste" });

    [Fact(DisplayName = "Processo incompleto: cada item estrutural pendente é uma causa no bloco do documento, e o cronograma vazio é recusado pela estrutura")]
    public void ProcessoIncompleto_NomeiaCadaItemPendente()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Vazio", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        IReadOnlyList<FieldError> causas = CausasDeConformidadeDeTeste.De(processo);

        // Um processo sem prova (SiSU, classificação importada) publica sem etapa: "Etapas" não é item incondicional.
        causas.Should().OnlyContain(static causa => causa.Error.Code == "ProcessoSeletivo.ConformidadeInsuficiente");
        causas.Should().Contain(static causa => causa.Field == "classificacao" && causa.Error.Message.Contains("Classificação", StringComparison.Ordinal));
        ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Para(processo)).Errors
            .Select(static causa => causa.Error.Code).Should().Contain("ProcessoSeletivo.CronogramaFasesVazio");
    }

    [Fact(DisplayName = "PendenciaDeConformidade_ProcessoConforme — não há pendência")]
    public void PendenciaDeConformidade_ProcessoConforme() =>
        CausasDeConformidadeDeTeste.De(ProcessoConforme()).Should().BeEmpty();

    [Fact(DisplayName = "Publicar com fase sem unidade responsável recusa e cita a fase")]
    public void Publicar_FaseSemUnidadeResponsavel_RecusaCitandoAFase()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(
            fase: ProcessoConformeFactory.FaseConforme(semUnidadeResponsavel: true));

        Result<VersaoConfiguracao> publicar = TentarPublicar(processo);

        publicar.IsFailure.Should().BeTrue();
        publicar.Error!.Code.Should().Be("ProcessoSeletivo.FaseSemUnidadeResponsavel");
        publicar.Error.Message.Should().Contain("'Resultado final'").And.NotContain("RESULTADO_FINAL");
        processo.Situacao.Should().Be(SituacaoDoProcessoSeletivo.Rascunho, "a recusa não congela nada");
    }

    [Fact(DisplayName = "Publicar_ProcessoNaoConforme_Recusa — a conferência da publicação continua valendo")]
    public void Publicar_ProcessoNaoConforme_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Vazio", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        Result<VersaoConfiguracao> publicar = TentarPublicar(processo);

        publicar.IsFailure.Should().BeTrue();
        publicar.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
    }

    [Fact(DisplayName =
        "PendenciaDeConformidade_SemTaxaDeclarada — processo conforme nas demais cinco dimensões ainda pendencia a taxa de inscrição (CA-01)")]
    public void PendenciaDeConformidade_SemTaxaDeclarada()
    {
        ProcessoSeletivo processo = ProcessoConforme(declararTaxa: false);

        IReadOnlyList<FieldError> causas = CausasDeConformidadeDeTeste.De(processo);

        FieldError causa = causas.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        causa.Error.Message.Should().Contain("Taxa de inscrição e isenção");
        causa.Field.Should().Be("taxaDeInscricao");
    }

    [Fact(DisplayName =
        "Publicar_SemTaxaDeclarada_Recusa — processo conforme nas demais cinco dimensões é recusado por faltar declarar taxa (CA-01)")]
    public void Publicar_SemTaxaDeclarada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConforme(declararTaxa: false);

        Result<VersaoConfiguracao> publicar = TentarPublicar(processo);

        publicar.IsFailure.Should().BeTrue(
            "as outras cinco dimensões estão conformes — só a ausência de declaração de taxa pode estar bloqueando");
        publicar.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        publicar.Error!.Message.Should().Contain("Taxa de inscrição e isenção");
    }
}
