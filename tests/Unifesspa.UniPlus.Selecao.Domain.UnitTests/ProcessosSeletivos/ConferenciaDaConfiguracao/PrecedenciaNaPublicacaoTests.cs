namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConferenciaDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A precedência do cadastro na publicação: lida pela operação, é conferida de novo contra o cronograma gravado, e o painel mostra
/// a mesma pendência. Sem a leitura (a retificação que não mexe no cronograma), nada se confere e o item fica conforme.
/// </summary>
public sealed class PrecedenciaNaPublicacaoTests
{
    private const string PrecedenciaViolada = "ProcessoSeletivo.PrecedenciaFaseViolada";
    private const string Sobreposicao = "ProcessoSeletivo.SobreposicaoDeJanelasNaoPermitida";
    private const string ItemDaPrecedencia = "cronograma_precedencia_entre_fases";

    private static ProcessoSeletivo ProcessoComSegundaFase(int diaDeInicio = 40)
    {
        ProcessoSeletivo processo = ApoioDaPublicacaoDoProcessoSeletivo.NovoProcessoConforme();
        FaseCronograma habilitacao = FaseCronograma.Criar(
            new OrigemCanonicaDaFase(Guid.CreateVersion7(), "HABILITACAO", OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(2, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(diaDeInicio), new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(diaDeInicio + 10), UnidadesDeTeste.Ceps),
            new PublicacoesDaFase([], EmiteParecerIndividual: false, RegraRecurso: null),
            []).Value!;
        processo.DefinirCronogramaFases([.. processo.CronogramaFases, habilitacao], []).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static string[] Codigos(ProcessoSeletivo processo, PrecedenciasDosCadastros precedencias) =>
        [.. CausasQueImpedemCongelar.De(processo.Declaracao(), CadastrosDeTeste.Para(processo).ComPrecedencias(precedencias)).Select(static causa => causa.Error.Code)];

    private static PrecedenciasDosCadastros Lidas(string antecessora, string sucessora, bool permiteSobreposicao = true) =>
        PrecedenciasDosCadastros.Lidas([new(antecessora, sucessora, permiteSobreposicao)]);

    [Fact(DisplayName = "A precedência cadastrada depois da gravação recusa a publicação das fases em ordem invertida")]
    public void PrecedenciaCadastradaDepois_RecusaAPublicacao()
    {
        ProcessoSeletivo processo = ProcessoComSegundaFase();

        Codigos(processo, Lidas("HABILITACAO", "RESULTADO_FINAL")).Should().Contain(PrecedenciaViolada);
    }

    [Fact(DisplayName = "Sem a leitura da precedência, ou com o cadastro sem relação alguma, a publicação não recusa por precedência")]
    public void SemLeituraOuSemRelacao_NaoRecusa()
    {
        ProcessoSeletivo processo = ProcessoComSegundaFase();

        Codigos(processo, PrecedenciasDosCadastros.NaoLidas).Should().NotContain(PrecedenciaViolada);
        Codigos(processo, PrecedenciasDosCadastros.Lidas([])).Should().NotContain(PrecedenciaViolada);
    }

    [Fact(DisplayName = "A publicação também confere a sobreposição que o cadastro não permite")]
    public void SobreposicaoNaoPermitida_RecusaAPublicacao()
    {
        ProcessoSeletivo processo = ProcessoComSegundaFase(diaDeInicio: 10);

        Codigos(processo, Lidas("RESULTADO_FINAL", "HABILITACAO", permiteSobreposicao: false)).Should().Contain(Sobreposicao);
        Codigos(processo, Lidas("RESULTADO_FINAL", "HABILITACAO", permiteSobreposicao: true)).Should().NotContain(Sobreposicao);
    }

    [Theory(DisplayName = "O painel mostra pendente o item da precedência exatamente quando a conferência recusa")]
    [MemberData(nameof(PrecedenciasEResultado))]
    public void Painel_ItemDaPrecedencia_SegueAConferencia(PrecedenciasDosCadastros precedencias, bool conforme)
    {
        ProcessoSeletivo processo = ProcessoComSegundaFase();
        ConfiguracaoDeclarada configuracao = processo.Declaracao();
        CadastrosDaConfiguracao cadastros = CadastrosDeTeste.Para(processo).ComPrecedencias(precedencias);

        ItemConformidade item = ChecklistDeConformidade.AvaliarConformidade(configuracao, cadastros).Single(static item => item.Codigo == ItemDaPrecedencia);
        Result<ConfiguracaoAptaACongelar> conferencia = ConfiguracaoAptaACongelar.Conferir(processo.Id, configuracao, cadastros);

        item.Ok.Should().Be(conforme);
        conferencia.IsSuccess.Should().Be(conforme);
    }

    public static TheoryData<PrecedenciasDosCadastros, bool> PrecedenciasEResultado => new()
    {
        { Lidas("HABILITACAO", "RESULTADO_FINAL"), false },
        { PrecedenciasDosCadastros.Lidas([]), true },
        { PrecedenciasDosCadastros.NaoLidas, true },
    };
}
