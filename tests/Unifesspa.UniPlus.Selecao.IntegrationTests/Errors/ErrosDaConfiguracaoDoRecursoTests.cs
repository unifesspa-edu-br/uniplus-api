namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Errors;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>
/// Cada recusa dos parâmetros do recurso, na fase e na etapa, chega ao cliente com o status 422 e o código estável do módulo,
/// que o <c>type</c> do problema resolve na página do catálogo.
/// </summary>
public sealed class ErrosDaConfiguracaoDoRecursoTests
{
    private static readonly IDomainErrorMapper Mapper = MapperDoModuloDeTeste.Criar();

    private static readonly Dictionary<MotivoDaRecusaDosParametros, string> SufixosNoFio = new()
    {
        [MotivoDaRecusaDosParametros.PrazoNaoPositivo] = "prazo_nao_positivo",
        [MotivoDaRecusaDosParametros.PrazoSemUnidadeDeclaravel] = "prazo_sem_unidade_declaravel",
        [MotivoDaRecusaDosParametros.PrazoEmDiasCorridos] = "prazo_em_dias_corridos",
        [MotivoDaRecusaDosParametros.PrazoEmFracaoDeDiaUtil] = "prazo_em_fracao_de_dia_util",
        [MotivoDaRecusaDosParametros.EfeitoSuspensivoNaoDeclarado] = "efeito_suspensivo_nao_declarado",
        [MotivoDaRecusaDosParametros.PrazoSegundaInstanciaNaoDeclarado] = "prazo_segunda_instancia_nao_declarado",
        [MotivoDaRecusaDosParametros.PrazoSegundaInstanciaSemInstanciaSuperior] = "prazo_segunda_instancia_sem_instancia_superior",
        [MotivoDaRecusaDosParametros.NaoGuardaComInstanciaSuperior] = "nao_guarda_com_instancia_superior",
        [MotivoDaRecusaDosParametros.GuardaDaVagaNaoDeclaravel] = "guarda_da_vaga_nao_declaravel",
    };

    public static TheoryData<MotivoDaRecusaDosParametros> Motivos => [.. Enum.GetValues<MotivoDaRecusaDosParametros>()];

    [Fact(DisplayName = "Todo motivo de recusa dos parâmetros do recurso tem o código estável declarado neste teste")]
    public void TodoMotivo_TemCodigoNoFio() =>
        SufixosNoFio.Keys.Should().BeEquivalentTo(Enum.GetValues<MotivoDaRecusaDosParametros>());

    [Theory(DisplayName = "A recusa dos parâmetros do recurso da fase é mapeada para 422 com o código estável do módulo")]
    [MemberData(nameof(Motivos))]
    public void RecusaDaFase_MapeadaPara422ComOCodigoEstavel(MotivoDaRecusaDosParametros motivo) =>
        ConferirMapeamento($"RegraRecursoFase.{motivo}", $"uniplus.selecao.configuracao_do_recurso_da_fase.{SufixosNoFio[motivo]}");

    [Theory(DisplayName = "A recusa dos parâmetros do recurso da etapa é mapeada para 422 com o código estável do módulo")]
    [MemberData(nameof(Motivos))]
    public void RecusaDaEtapa_MapeadaPara422ComOCodigoEstavel(MotivoDaRecusaDosParametros motivo) =>
        ConferirMapeamento($"RecursoDaEtapa.{motivo}", $"uniplus.selecao.configuracao_do_recurso_da_etapa.{SufixosNoFio[motivo]}");

    private static void ConferirMapeamento(string codigoDoDominio, string codigoNoFio)
    {
        bool mapeada = Mapper.TryGetMapping(codigoDoDominio, out DomainErrorMapping? mapeamento);

        mapeada.Should().BeTrue();
        mapeamento!.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        mapeamento.Code.Should().Be(codigoNoFio);
        mapeamento.Title.Should().NotBeNullOrWhiteSpace();
        Mapper.GetProblemTypeUri(codigoNoFio).Should().Be(MapperDoModuloDeTeste.BaseUriDeErros + codigoNoFio);
    }
}
