namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A fase em que um fato citado por gatilho de exigência fica conhecido no processo (UNI-REQ-0144,
/// UNI-REQ-0077): a mais tardia entre a fase do catálogo e a do formulário que o produz, e a do
/// derivado não antes das suas dependências, nem o agregado antes do grupo que tem o fato de membro;
/// fato só da isenção é citado só por documento do formulário de isenção. O grupo que alimenta agregado citado é obrigatório
/// sempre, ele e o campo (UNI-REQ-0074).
/// </summary>
public sealed class FaseEfetivaDosFatosTests
{
    private static readonly Dictionary<string, string> TudoNaInscricao = new(StringComparer.Ordinal)
    {
        ["TEM_RENDA"] = "INSCRICAO",
        ["COMPROVANTE_RENDA"] = "INSCRICAO",
        ["BOLSISTA"] = "INSCRICAO",
        ["RURAL_NA_FAMILIA"] = "INSCRICAO",
        ["BOLSISTA_NA_FAMILIA"] = "INSCRICAO",
    };

    private static readonly Dictionary<string, string> SemAgregados = new(StringComparer.Ordinal);

    private static readonly Dictionary<string, string> Agregados = new(StringComparer.Ordinal)
    {
        ["RURAL_NA_FAMILIA"] = "MEMBRO_RURAL",
        ["BOLSISTA_NA_FAMILIA"] = "MEMBRO_BOLSISTA",
    };

    private static FaseCronograma Fase(int ordem, string codigo, bool coletaInscricao = false, bool coletaIsencao = false) =>
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, "CEPS", OrigemDataFase.Propria,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: coletaInscricao, coletaSolicitacaoIsencao: coletaIsencao,
            inicio: new DateTimeOffset(2026, 1, ordem, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, ordem + 1, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar(codigo, PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!;

    private static FatoColetado Item(string codigo) =>
        FatoColetado.Criar(codigo, 0, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, etapaCodigo: FormularioDeTeste.Secao).Value!;

    private static GrupoColetado Grupo(string membro) =>
        GrupoColetado.Criar($"GRUPO_{membro}", 1, FormularioDeTeste.Secao, "Composição familiar", 0, 5, null, Obrigatoriedade.Sempre,
            [FatoColetado.Criar(membro, 0, membro, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null).Value!]).Value!;

    private static void DefinirDerivado(ProcessoSeletivo processo, string derivado, string dependencia) =>
        processo.DefinirRegrasDerivacao(
        [
            ConfiguracaoDerivacaoFato.Criar(derivado,
                [RegraDerivacaoConfigurada.Criar(0, "SIM", [CondicaoRegraDerivacao.Criar(0, dependencia, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!]).Value!])
                .Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

    private static (ProcessoSeletivo Processo, FaseCronograma Inscricao, FaseCronograma Isencao, FaseCronograma Habilitacao) Processo()
    {
        FaseCronograma inscricao = Fase(1, "INSCRICAO", coletaInscricao: true);
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: inscricao);
        FaseCronograma isencao = Fase(2, "SOLICITACAO_ISENCAO", coletaIsencao: true);
        FaseCronograma habilitacao = Fase(3, FormularioProcesso.CodigoFaseHabilitacao);
        Result cronograma = processo.DefinirCronogramaFases([inscricao, isencao, habilitacao], [], PrecondicaoIfMatch.Ausente);
        cronograma.IsSuccess.Should().BeTrue(cronograma.Error?.Message);
        foreach ((FinalidadeFormulario finalidade, FaseCronograma fase, string fato, GrupoColetado[] grupos) in new[]
        {
            (FinalidadeFormulario.Inscricao, inscricao, "TEM_RENDA", Array.Empty<GrupoColetado>()),
            (FinalidadeFormulario.IsencaoTaxa, isencao, "BOLSISTA", [Grupo("MEMBRO_BOLSISTA")]),
            (FinalidadeFormulario.Habilitacao, habilitacao, "COMPROVANTE_RENDA", [Grupo("MEMBRO_RURAL")]),
        })
        {
            processo.DefinirFormulario(finalidade, fase.Id, null, FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
            Result itens = processo.DefinirFatosColetados(finalidade, [Item(fato)], PrecondicaoIfMatch.Ausente, grupos);
            itens.IsSuccess.Should().BeTrue(itens.Error?.Message);
        }

        return (processo, inscricao, isencao, habilitacao);
    }

    /// <summary>Inscrição e isenção respondidas na mesma fase, cada uma com o fato que só ela coleta.</summary>
    private static (ProcessoSeletivo Processo, FaseCronograma Compartilhada) ProcessoComInscricaoEIsencaoNaMesmaFase()
    {
        FaseCronograma compartilhada = Fase(1, "INSCRICAO", coletaInscricao: true, coletaIsencao: true);
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: compartilhada);
        processo.DefinirCronogramaFases([compartilhada], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        foreach ((FinalidadeFormulario finalidade, string fato) in new[]
        {
            (FinalidadeFormulario.Inscricao, "TEM_RENDA"),
            (FinalidadeFormulario.IsencaoTaxa, "BOLSISTA"),
        })
        {
            processo.DefinirFormulario(finalidade, compartilhada.Id, null, FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
            processo.DefinirFatosColetados(finalidade, [Item(fato)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        }

        return (processo, compartilhada);
    }

    [Fact(DisplayName = "Fato coletado só na habilitação não é citado por exigência da inscrição, mesmo que o catálogo o dê na inscrição")]
    public void FatoDaHabilitacao_EmExigenciaDaInscricao_Recusa()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, _, FaseCronograma habilitacao) = Processo();

        processo.RecusaDeFaseDoGatilho("COMPROVANTE_RENDA", inscricao.Id, FinalidadeFormulario.Inscricao, TudoNaInscricao, SemAgregados)!.Code
            .Should().Be(DocumentoExigidoErrorCodes.FatoResolvidoEmFasePosterior);
        processo.RecusaDeFaseDoGatilho("COMPROVANTE_RENDA", habilitacao.Id, FinalidadeFormulario.Habilitacao, TudoNaInscricao, SemAgregados).Should().BeNull();
    }

    [Fact(DisplayName = "O derivado fica conhecido na fase da sua dependência mais tardia")]
    public void Derivado_HerdaAFaseDaDependencia()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, _, _) = Processo();
        DefinirDerivado(processo, "RENDA_COMPROVADA", "COMPROVANTE_RENDA");

        processo.RecusaDeFaseDoGatilho("RENDA_COMPROVADA", inscricao.Id, FinalidadeFormulario.Inscricao, TudoNaInscricao, SemAgregados)!.Code
            .Should().Be(DocumentoExigidoErrorCodes.FatoResolvidoEmFasePosterior);
    }

    [Fact(DisplayName = "O derivado do sistema fica conhecido na fase do formulário que coleta a dependência declarada")]
    public void DerivadoDoSistema_HerdaAFaseDaDependencia()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, _, FaseCronograma habilitacao) = Processo();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("DATA_NASCIMENTO")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        Dictionary<string, string> catalogo = new(TudoNaInscricao, StringComparer.Ordinal) { ["FAIXA_ETARIA"] = "INSCRICAO" };

        processo.RecusaDeFaseDoGatilho("FAIXA_ETARIA", inscricao.Id, FinalidadeFormulario.Inscricao, catalogo, SemAgregados)!.Code
            .Should().Be(DocumentoExigidoErrorCodes.FatoResolvidoEmFasePosterior);
        processo.RecusaDeFaseDoGatilho("FAIXA_ETARIA", habilitacao.Id, FinalidadeFormulario.Habilitacao, catalogo, SemAgregados).Should().BeNull();
    }

    [Fact(DisplayName = "O agregado fica conhecido na fase do formulário do grupo que tem o fato de membro")]
    public void Agregado_HerdaAFaseDoGrupoDoFatoDeMembro()
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao, _, FaseCronograma habilitacao) = Processo();

        processo.RecusaDeFaseDoGatilho("RURAL_NA_FAMILIA", inscricao.Id, FinalidadeFormulario.Inscricao, TudoNaInscricao, Agregados)!.Code
            .Should().Be(DocumentoExigidoErrorCodes.FatoResolvidoEmFasePosterior);
        processo.RecusaDeFaseDoGatilho("RURAL_NA_FAMILIA", habilitacao.Id, FinalidadeFormulario.Habilitacao, TudoNaInscricao, Agregados).Should().BeNull();
    }

    [Fact(DisplayName = "O agregado sobre grupo do formulário de isenção só é citado por documento do formulário de isenção")]
    public void AgregadoDeGrupoDaIsencao_EmOutraFinalidade_Recusa()
    {
        (ProcessoSeletivo processo, _, _, FaseCronograma habilitacao) = Processo();

        processo.RecusaDeFaseDoGatilho("BOLSISTA_NA_FAMILIA", habilitacao.Id, FinalidadeFormulario.Habilitacao, TudoNaInscricao, Agregados)!.Code
            .Should().Be(DocumentoExigidoErrorCodes.FatoDaIsencaoEmOutraFinalidade);
    }

    [Theory(DisplayName = "Agregado citado por regra viva exige o grupo e o campo de membro obrigatórios sempre")]
    [InlineData(false, true, "grupo 'GRUPO_MEMBRO_RURAL'")]
    [InlineData(true, false, "campo 'MEMBRO_RURAL'")]
    public void PendenciaDeGrupoQueAlimentaAgregado_GrupoOuCampoOpcional_Recusa(bool grupoObrigatorio, bool campoObrigatorio, string recusado)
    {
        (ProcessoSeletivo processo, _, _, _) = Processo();
        FatoColetado campo = FatoColetado.Criar(
            "MEMBRO_RURAL", 0, "MEMBRO_RURAL", TipoRenderizacao.Booleano, campoObrigatorio ? Obrigatoriedade.Sempre : Obrigatoriedade.Nunca, null).Value!;
        GrupoColetado grupo = GrupoColetado.Criar(
            "GRUPO_MEMBRO_RURAL", 1, FormularioDeTeste.Secao, "Composição familiar", 0, 5, null,
            grupoObrigatorio ? Obrigatoriedade.Sempre : Obrigatoriedade.Nunca, [campo]).Value!;
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("COMPROVANTE_RENDA")], PrecondicaoIfMatch.Ausente, [grupo])
            .IsSuccess.Should().BeTrue();
        processo.PendenciaDeGrupoQueAlimentaAgregado(Agregados).Should().BeNull("nenhuma regra cita o agregado");

        DefinirDerivado(processo, "RENDA_RURAL", "RURAL_NA_FAMILIA");

        DomainError? pendencia = processo.PendenciaDeGrupoQueAlimentaAgregado(Agregados);
        pendencia!.Code.Should().Be(ItemFormularioErrorCodes.OpcionalQueAlimentaRegra);
        pendencia.Message.Should().Contain(recusado);
    }

    [Fact(DisplayName = "Fato só da isenção é citado só por documento do formulário de isenção, nem mesmo na habilitação, que vem depois")]
    public void FatoDaIsencao_SoNoFormularioDeIsencao()
    {
        (ProcessoSeletivo processo, _, FaseCronograma isencao, FaseCronograma habilitacao) = Processo();

        processo.RecusaDeFaseDoGatilho("BOLSISTA", habilitacao.Id, FinalidadeFormulario.Habilitacao, TudoNaInscricao, SemAgregados)!.Code
            .Should().Be(DocumentoExigidoErrorCodes.FatoDaIsencaoEmOutraFinalidade);
        processo.RecusaDeFaseDoGatilho("BOLSISTA", isencao.Id, FinalidadeFormulario.IsencaoTaxa, TudoNaInscricao, SemAgregados).Should().BeNull();
    }

    /// <summary>
    /// Inscrição e isenção na mesma fase: a fase é a mesma, e só a finalidade da exigência separa o
    /// documento da isenção, que cita o fato que só ela coleta, do documento da inscrição, que não cita.
    /// </summary>
    [Theory(DisplayName = "Na fase que divide inscrição e isenção, o fato só da isenção é citado pelo documento da isenção e recusado no da inscrição")]
    [InlineData(FinalidadeFormulario.IsencaoTaxa, true)]
    [InlineData(FinalidadeFormulario.Inscricao, false)]
    public void FatoDaIsencao_NaFaseCompartilhada_SegueAFinalidade(FinalidadeFormulario finalidade, bool aceito)
    {
        (ProcessoSeletivo processo, FaseCronograma compartilhada) = ProcessoComInscricaoEIsencaoNaMesmaFase();

        DomainError? recusa = processo.RecusaDeFaseDoGatilho("BOLSISTA", compartilhada.Id, finalidade, TudoNaInscricao, SemAgregados);

        (recusa is null).Should().Be(aceito);
        if (!aceito)
        {
            recusa!.Code.Should().Be(DocumentoExigidoErrorCodes.FatoDaIsencaoEmOutraFinalidade);
        }
    }

    /// <summary>
    /// A exigência sem formulário, na fase que divide inscrição e isenção, cita fato só da isenção: a
    /// recusa que orienta é a da finalidade que falta, conferida na definição das exigências, e não a
    /// do gatilho, que levaria a mexer na condição.
    /// </summary>
    [Fact(DisplayName = "Sem a finalidade que a fase pede, o gatilho não é recusado: a recusa é a da finalidade")]
    public void FinalidadeAusente_NaoRecusaOGatilho()
    {
        (ProcessoSeletivo processo, FaseCronograma compartilhada) = ProcessoComInscricaoEIsencaoNaMesmaFase();

        processo.RecusaDeFaseDoGatilho("BOLSISTA", compartilhada.Id, finalidadeDaExigencia: null, TudoNaInscricao, SemAgregados).Should().BeNull();
    }

    [Fact(DisplayName = "O derivado de fato da isenção também só é citado por documento do formulário de isenção")]
    public void DerivadoDeFatoDaIsencao_EmOutraFinalidade_Recusa()
    {
        (ProcessoSeletivo processo, _, _, FaseCronograma habilitacao) = Processo();
        DefinirDerivado(processo, "ISENTO_POR_BOLSA", "BOLSISTA");

        processo.RecusaDeFaseDoGatilho("ISENTO_POR_BOLSA", habilitacao.Id, FinalidadeFormulario.Habilitacao, TudoNaInscricao, SemAgregados)!.Code
            .Should().Be(DocumentoExigidoErrorCodes.FatoDaIsencaoEmOutraFinalidade);
    }

    [Fact(DisplayName = "Fato que o catálogo situa em fase fora do cronograma é recusado")]
    public void PontoDoCatalogoForaDoCronograma_Recusa()
    {
        (ProcessoSeletivo processo, _, _, FaseCronograma habilitacao) = Processo();

        processo.RecusaDeFaseDoGatilho(
                "MODALIDADE_CONVOCACAO", habilitacao.Id, FinalidadeFormulario.Habilitacao, new Dictionary<string, string>(StringComparer.Ordinal) { ["MODALIDADE_CONVOCACAO"] = "RESULTADO_FINAL" }, SemAgregados)!
            .Code.Should().Be(DocumentoExigidoErrorCodes.PontoResolucaoForaDoCronograma);
    }
}
