namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O acréscimo cita o fato booleano que decide quem o recebe, e o motor o resolve para todo candidato
/// (ADR-0142): com inscrição própria a citação é obrigatória, e o fato é campo do formulário de
/// inscrição ou derivado por regra do processo, conhecido até a fase da inscrição.
/// </summary>
public sealed class FatoDeElegibilidadeDoAcrescimoTests
{
    private static readonly Dictionary<string, string> SemCatalogo = new(StringComparer.Ordinal);

    private static FaseCronograma Fase(int ordem, string codigo, bool coletaInscricao = false, bool coletaIsencao = false) =>
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: coletaInscricao, coletaSolicitacaoIsencao: coletaIsencao,
            inicio: new DateTimeOffset(2026, 1, ordem, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, ordem + 1, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar(codigo, PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    private static FatoColetado Item(string codigo) =>
        FatoColetado.Criar(codigo, 0, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, etapaCodigo: FormularioDeTeste.Secao, classificacaoProtecao: "PESSOAL").Value!;

    private static FatoColetado Numero(string codigo, int ordem) =>
        FatoColetado.Criar(codigo, ordem, codigo, TipoRenderizacao.Numero, Obrigatoriedade.Sempre, null, etapaCodigo: FormularioDeTeste.Secao, classificacaoProtecao: "PESSOAL").Value!;

    /// <summary>
    /// Inscrição, isenção e habilitação em fases sucessivas, cada uma com o fato booleano que só ela
    /// coleta; a inscrição tem também um grupo repetível com um campo booleano de membro.
    /// </summary>
    private static ProcessoSeletivo Processo(bool isencaoNaFaseDaInscricao = false)
    {
        FaseCronograma inscricao = Fase(1, "INSCRICAO", coletaInscricao: true, coletaIsencao: isencaoNaFaseDaInscricao);
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: inscricao);
        FaseCronograma isencao = isencaoNaFaseDaInscricao ? inscricao : Fase(2, "SOLICITACAO_ISENCAO", coletaIsencao: true);
        FaseCronograma habilitacao = Fase(3, FormularioProcesso.CodigoFaseHabilitacao);
        FaseCronograma[] fases = isencaoNaFaseDaInscricao ? [inscricao, habilitacao] : [inscricao, isencao, habilitacao];
        processo.DefinirCronogramaFases(fases, [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        foreach ((FinalidadeFormulario finalidade, FaseCronograma fase, FatoColetado[] fatos) in new[]
        {
            (FinalidadeFormulario.Inscricao, inscricao, new[] { Item("PEDE_O_ACRESCIMO"), Numero("RENDA_FAMILIAR", 1) }),
            (FinalidadeFormulario.IsencaoTaxa, isencao, [Item("BOLSISTA")]),
            (FinalidadeFormulario.Habilitacao, habilitacao, [Item("COMPROVANTE_RENDA")]),
        })
        {
            processo.DefinirFormulario(finalidade, fase.Id, null, FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
            Result itens = processo.DefinirFatosColetados(
                finalidade, fatos, PrecondicaoIfMatch.Ausente, finalidade == FinalidadeFormulario.Inscricao ? [GrupoDaInscricao()] : null);
            itens.IsSuccess.Should().BeTrue(itens.Error?.Message);
        }

        return processo;
    }

    private static GrupoColetado GrupoDaInscricao() =>
        GrupoColetado.Criar("COMPOSICAO_FAMILIAR", 2, FormularioDeTeste.Secao, "Composição familiar", 0, 5, null, Obrigatoriedade.Sempre,
            [FatoColetado.Criar("MEMBRO_PEDE_O_ACRESCIMO", 0, "Membro pede o acréscimo", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL").Value!],
            FinalidadeFormulario.Inscricao).Value!;

    private static void Aplicar(ProcessoSeletivo processo, string? fatoElegibilidade) =>
        processo.DefinirBonusRegional(
            aplica: true,
            ConfiguracaoBonusRegional.Criar(
                ReferenciaRegra.Criar(RegraBonusCodigo.Multiplicativo, "v1", new string('a', 64)).Value!,
                1.20m, null, fatoElegibilidade, Guid.NewGuid(), "EDITAL", "Edital nº 1/2026", "Fator de Atenuação das Desigualdades de Acesso",
                [("1504208", "Marabá", "PA")]).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

    /// <summary>Derivado booleano: verdadeiro quando a dependência é verdadeira.</summary>
    private static void Derivar(ProcessoSeletivo processo, string derivado, string dependencia, string? contribui = null) =>
        processo.DefinirRegrasDerivacao(
        [
            ConfiguracaoDerivacaoFato.Criar(derivado,
                [RegraDerivacaoConfigurada.Criar(0, contribui, [CondicaoRegraDerivacao.Criar(0, dependencia, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!]).Value!])
                .Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

    private static bool ItemVermelho(ProcessoSeletivo processo) =>
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum)
            .Single(static i => i.Codigo == "bonus_regional_fato_de_elegibilidade").Ok is false;

    [Fact(DisplayName = "Com inscrição própria, o acréscimo sem o fato de elegibilidade fica pendente no checklist")]
    public void InscricaoPropria_SemFato_Pendente()
    {
        ProcessoSeletivo processo = Processo();
        Aplicar(processo, null);

        processo.RecusaDoFatoDeElegibilidade(SemCatalogo, SemCatalogo)!.Code.Should().Be(ProcessoSeletivoErrorCodes.FatoElegibilidadeAusente);
        ItemVermelho(processo).Should().BeTrue();
    }

    [Fact(DisplayName = "Com resultado importado, o acréscimo dispensa o fato de elegibilidade: a classificação já vem pronta")]
    public void ResultadoImportado_DispensaOFato()
    {
        ProcessoSeletivo processo = Processo();
        Aplicar(processo, null);
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.OrigemCandidatos))!.SetValue(processo, OrigemCandidatos.ImportacaoExterna);

        processo.RecusaDoFatoDeElegibilidade(SemCatalogo, SemCatalogo).Should().BeNull();
        ItemVermelho(processo).Should().BeFalse();
    }

    [Theory(DisplayName = "O fato de elegibilidade é campo do formulário de inscrição ou derivado pelo processo")]
    [InlineData("NAO_COLETADO")]
    [InlineData("COMPROVANTE_RENDA")]
    public void FatoForaDaInscricao_Recusa(string fato)
    {
        ProcessoSeletivo processo = Processo();
        Aplicar(processo, fato);

        processo.RecusaDoFatoDeElegibilidade(SemCatalogo, SemCatalogo)!.Code.Should().Be(ProcessoSeletivoErrorCodes.FatoElegibilidadeForaDoProcesso);
    }

    [Fact(DisplayName = "O campo de membro de grupo não decide o acréscimo, que é decidido por um fato do candidato")]
    public void CampoDeMembroDeGrupo_Recusa()
    {
        ProcessoSeletivo processo = Processo();
        Aplicar(processo, "MEMBRO_PEDE_O_ACRESCIMO");

        processo.RecusaDoFatoDeElegibilidade(SemCatalogo, SemCatalogo)!.Code.Should().Be(ProcessoSeletivoErrorCodes.FatoElegibilidadeDeMembroDeGrupo);
    }

    [Fact(DisplayName = "O campo da inscrição citado como fato de elegibilidade é booleano")]
    public void CampoNaoBooleano_Recusa()
    {
        ProcessoSeletivo processo = Processo();
        Aplicar(processo, "RENDA_FAMILIAR");

        processo.RecusaDoFatoDeElegibilidade(SemCatalogo, SemCatalogo)!.Code.Should().Be(ProcessoSeletivoErrorCodes.FatoElegibilidadeNaoBooleano);
    }

    [Fact(DisplayName = "O derivado citado como fato de elegibilidade é booleano")]
    public void DerivadoCategorico_Recusa()
    {
        ProcessoSeletivo processo = Processo();
        Derivar(processo, "ELEGIVEL_AO_ACRESCIMO", "PEDE_O_ACRESCIMO", contribui: "SIM");
        Aplicar(processo, "ELEGIVEL_AO_ACRESCIMO");

        processo.RecusaDoFatoDeElegibilidade(SemCatalogo, SemCatalogo)!.Code.Should().Be(ProcessoSeletivoErrorCodes.FatoElegibilidadeNaoBooleano);
    }

    [Fact(DisplayName = "O derivado de fato da habilitação só é conhecido depois da inscrição")]
    public void DerivadoDaHabilitacao_Recusa()
    {
        ProcessoSeletivo processo = Processo();
        Derivar(processo, "ELEGIVEL_AO_ACRESCIMO", "COMPROVANTE_RENDA");
        Aplicar(processo, "ELEGIVEL_AO_ACRESCIMO");

        processo.RecusaDoFatoDeElegibilidade(SemCatalogo, SemCatalogo)!.Code.Should().Be(ProcessoSeletivoErrorCodes.FatoElegibilidadeConhecidoDepoisDaInscricao);
    }

    [Fact(DisplayName = "O ponto de resolução que o catálogo dá ao fato conta: fato da inscrição que o catálogo resolve na habilitação é conhecido depois")]
    public void PontoDeResolucaoDoCatalogo_PosteriorAInscricao_Recusa()
    {
        ProcessoSeletivo processo = Processo();
        Aplicar(processo, "PEDE_O_ACRESCIMO");
        Dictionary<string, string> catalogo = new(StringComparer.Ordinal) { ["PEDE_O_ACRESCIMO"] = FormularioProcesso.CodigoFaseHabilitacao };

        processo.RecusaDoFatoDeElegibilidade(SemCatalogo, SemCatalogo).Should().BeNull();
        processo.RecusaDoFatoDeElegibilidade(catalogo, SemCatalogo)!.Code.Should().Be(ProcessoSeletivoErrorCodes.FatoElegibilidadeConhecidoDepoisDaInscricao);
    }

    [Fact(DisplayName = "O derivado de fato que só a isenção coleta não decide o acréscimo, mesmo na fase da inscrição")]
    public void DerivadoDaIsencao_Recusa()
    {
        ProcessoSeletivo processo = Processo(isencaoNaFaseDaInscricao: true);
        Derivar(processo, "ELEGIVEL_AO_ACRESCIMO", "BOLSISTA");
        Aplicar(processo, "ELEGIVEL_AO_ACRESCIMO");

        processo.RecusaDoFatoDeElegibilidade(SemCatalogo, SemCatalogo)!.Code.Should().Be(ProcessoSeletivoErrorCodes.FatoElegibilidadeDaIsencao);
    }

    [Theory(DisplayName = "Campo booleano da inscrição, ou derivado booleano dele, decide o acréscimo")]
    [InlineData("PEDE_O_ACRESCIMO", false)]
    [InlineData("ELEGIVEL_AO_ACRESCIMO", true)]
    public void FatoBooleanoDaInscricao_Aceito(string fato, bool derivado)
    {
        ProcessoSeletivo processo = Processo();
        if (derivado)
        {
            Derivar(processo, fato, "PEDE_O_ACRESCIMO");
        }

        Aplicar(processo, fato);

        processo.RecusaDoFatoDeElegibilidade(SemCatalogo, SemCatalogo).Should().BeNull();
        ItemVermelho(processo).Should().BeFalse();
    }
}
