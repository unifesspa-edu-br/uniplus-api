namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaPublicacaoDoProcessoSeletivo;

/// <summary>
/// O que a conferência da configuração devolve para o cronograma e as etapas quando as declarações têm mais de uma causa:
/// a primeira, na ordem de posição, e a etapa que declara uma fase que o cronograma não tem.
/// </summary>
public sealed class ConferenciaDoCronogramaNaPublicacaoTests
{
    // Sem formulário: com ele, a fase que o formulário cita não pode sair do cronograma.
    private static ProcessoSeletivo ProcessoComCronograma(params FaseCronograma[] fases)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        Aceitar(processo.DefinirCronogramaFases(fases, []));

        return processo;
    }

    private static ProcessoSeletivo ProcessoComEtapas(params EtapaProcesso[] etapas)
    {
        ProcessoSeletivo processo = ProcessoComCronograma(FaseConforme());
        Aceitar(processo.DefinirEtapas(etapas));

        return processo;
    }

    private static void Aceitar(Result resultado) =>
        resultado.IsSuccess.Should().BeTrue(string.Join(", ", resultado.Errors.Select(static e => e.Error.Code)));

    private static DomainError? Pendencia(ProcessoSeletivo processo) =>
        PonteiroNoDocumento.Converter(ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Padrao).Errors, OrigemNoDocumento.Identidade)
            .Where(static causa => causa.Error.Code != "ProcessoSeletivo.ConformidadeInsuficiente"
                && causa.Field is not null
                && (causa.Field.StartsWith("/cronograma", StringComparison.Ordinal) || causa.Field.StartsWith("/etapas", StringComparison.Ordinal)))
            .Select(static causa => causa.Error)
            .FirstOrDefault();

    [Fact(DisplayName = "A etapa carregada com uma fase fora do cronograma é recusada na publicação, citando a etapa e a fase")]
    public void Publicacao_EtapaComFaseForaDoCronograma_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComCronograma(new ModeloDeFase("OUTRA_FASE", 1).Criar());
        ProcessoCarregadoDoBanco.Etapa(processo, EtapaDaProvaObjetiva("Prova", CaraterEtapa.Classificatoria, 1m, faseCodigo: "RESULTADO_FINAL"));

        DomainError? pendencia = Pendencia(processo);

        pendencia!.Code.Should().Be("ProcessoSeletivo.EtapaSemFaseNoCronograma");
        pendencia.Message.Should().Contain("\"Prova\"").And.Contain("RESULTADO_FINAL");
    }

    [Fact(DisplayName = "Com várias etapas sem fase declarada, a recusa é a da etapa de menor posição")]
    public void Publicacao_EtapasSemFase_RecusaAPrimeira()
    {
        ProcessoSeletivo processo = ProcessoComEtapas(
            EtapaDaProvaObjetiva("Prova", CaraterEtapa.Classificatoria, 1m, ordem: 1, faseCodigo: "RESULTADO_FINAL"),
            EtapaDaProvaObjetiva("Redação", CaraterEtapa.Classificatoria, 1m, ordem: 2),
            EtapaDaProvaObjetiva("Entrevista", CaraterEtapa.Classificatoria, 1m, ordem: 3));

        DomainError? pendencia = Pendencia(processo);

        pendencia!.Code.Should().Be("ProcessoSeletivo.EtapaSemFaseDeclarada");
        pendencia.Message.Should().Contain("'Redação'").And.NotContain("Entrevista");
    }

    [Fact(DisplayName = "Com várias fases sem unidade responsável, a recusa é a da fase de menor posição")]
    public void Publicacao_FasesSemUnidade_RecusaAPrimeira()
    {
        ProcessoSeletivo processo = ProcessoComCronograma(
            new ModeloDeFase("RESULTADO", 1).Criar(),
            new ModeloDeFase("MATRICULA", 2) { ComUnidade = false }.Criar(),
            new ModeloDeFase("RECURSO", 3) { ComUnidade = false }.Criar());

        DomainError? pendencia = Pendencia(processo);

        pendencia!.Code.Should().Be("ProcessoSeletivo.FaseSemUnidadeResponsavel");
        pendencia.Message.Should().Contain("MATRICULA").And.NotContain("RECURSO");
    }

    [Fact(DisplayName = "A fase sem unidade é apontada pela posição na ordem do documento, e não pela ordem em que foi declarada")]
    public void Publicacao_FasesDeclaradasForaDeOrdem_ApontamAPosicaoNoDocumento()
    {
        ProcessoSeletivo processo = ProcessoComCronograma(
            new ModeloDeFase("RECURSO", 3) { ComUnidade = false }.Criar(),
            new ModeloDeFase("RESULTADO", 1).Criar(),
            new ModeloDeFase("MATRICULA", 2) { ComUnidade = false }.Criar());

        IReadOnlyList<FieldError> causas = PonteiroNoDocumento.Converter(
            ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Padrao).Errors, OrigemNoDocumento.Identidade);

        causas.Where(static causa => causa.Error.Code == "ProcessoSeletivo.FaseSemUnidadeResponsavel").Select(static causa => (causa.Field, causa.Error.Message))
            .Should().SatisfyRespectively(
                static primeira => (primeira.Field, primeira.Message.Contains("MATRICULA", StringComparison.Ordinal)).Should().Be(("/cronograma/fases/1/unidadeResponsavelCodigo", true)),
                static segunda => (segunda.Field, segunda.Message.Contains("RECURSO", StringComparison.Ordinal)).Should().Be(("/cronograma/fases/2/unidadeResponsavelCodigo", true)));
        processo.Declaracao().Cronograma.CronogramaFases.Select(static fase => fase.Codigo).Should().Equal("RESULTADO", "MATRICULA", "RECURSO");
    }

    [Fact(DisplayName = "Duas fases com o mesmo código e origens diferentes são recusadas na gravação do cronograma")]
    public void Gravacao_MesmoCodigoComOrigensDiferentes_RecusaFaseCanonicaDuplicada()
    {
        ProcessoSeletivo processo = ProcessoComCronograma(FaseConforme());
        FaseCronograma[] fases = [new ModeloDeFase("INSCRICAO", 1).Criar(), new ModeloDeFase("INSCRICAO", 2).Criar()];

        Result resultado = processo.DefinirCronogramaFases(fases, []);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.FaseCanonicaDuplicada");
    }
}
