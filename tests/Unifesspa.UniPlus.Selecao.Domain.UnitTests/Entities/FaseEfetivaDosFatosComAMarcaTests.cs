namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O documento exigido na isenção, que vem antes da inscrição, citando fato que a inscrição coleta: sem a marca "no mesmo momento
/// da anterior" o fato só é conhecido depois da isenção e a exigência é recusada; com a marca na inscrição as duas acontecem
/// juntas e não há recusa. Não há regra automática para a isenção: só a marca junta as duas fases.
/// </summary>
public sealed class FaseEfetivaDosFatosComAMarcaTests
{
    private static readonly Dictionary<string, string> FatoDaInscricaoNoCatalogo = new(StringComparer.Ordinal) { ["TEM_RENDA"] = "INSCRICAO" };

    private static readonly Dictionary<string, string> SemAgregados = new(StringComparer.Ordinal);

    private static FaseCronograma Fase(int ordem, string codigo, bool coletaInscricao = false, bool coletaIsencao = false, bool marcada = false) =>
        FaseCronograma.Criar(
            new OrigemCanonicaDaFase(
                Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: coletaInscricao, ColetaSolicitacaoIsencao: coletaIsencao),
            new DecisoesDeCronogramaDaFase(
                ordem, Inicio: new DateTimeOffset(2026, 1, ordem, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, ordem + 1, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps)
            { NoMesmoMomentoDaAnterior = marcada },
            new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar(codigo, PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    private static FatoColetado Item(string codigo) =>
        FatoColetado.Criar(codigo, 0, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, etapaCodigo: FormularioDeTeste.Secao, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    private static (ProcessoSeletivo Processo, FaseCronograma Isencao) ProcessoComIsencaoAntesDaInscricao(bool inscricaoMarcada)
    {
        FaseCronograma isencao = Fase(1, "SOLICITACAO_ISENCAO", coletaIsencao: true);
        FaseCronograma inscricao = Fase(2, "INSCRICAO", coletaInscricao: true, marcada: inscricaoMarcada);
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: isencao);
        Result cronograma = processo.DefinirCronogramaFases([isencao, inscricao], []);
        cronograma.IsSuccess.Should().BeTrue(cronograma.Error?.Message);
        DefinirFormularioComUmFato(processo, FinalidadeFormulario.Inscricao, inscricao, "TEM_RENDA");
        DefinirFormularioComUmFato(processo, FinalidadeFormulario.IsencaoTaxa, isencao, "BOLSISTA");

        return (processo, isencao);
    }

    private static void DefinirFormularioComUmFato(ProcessoSeletivo processo, FinalidadeFormulario finalidade, FaseCronograma fase, string fato)
    {
        processo.DefinirFormulario(finalidade, fase.Id, null, FormularioDeTeste.Etapas()).IsSuccess.Should().BeTrue();
        Result itens = processo.DefinirFatosColetados(finalidade, [Item(fato)]);
        itens.IsSuccess.Should().BeTrue(itens.Error?.Message);
    }

    [Fact(DisplayName = "Sem a marca, o fato da inscrição citado pelo documento exigido na isenção anterior é recusado como resolvido em fase posterior")]
    public void FatoDaInscricao_EmExigenciaDaIsencaoAnterior_SemMarca_Recusa()
    {
        (ProcessoSeletivo processo, FaseCronograma isencao) = ProcessoComIsencaoAntesDaInscricao(inscricaoMarcada: false);

        DomainError? recusa = FaseEfetivaDoFato.RecusaDeFaseDoGatilho(
            processo.Declaracao(), "TEM_RENDA", isencao.Id, FinalidadeFormulario.IsencaoTaxa, FatoDaInscricaoNoCatalogo, SemAgregados);

        recusa.Should().NotBeNull();
        recusa!.Code.Should().Be(DocumentoExigidoErrorCodes.FatoResolvidoEmFasePosterior);
        recusa.Message.Should().Contain("'INSCRICAO'").And.Contain("(ordem 2)").And.Contain("(ordem 1)");
    }

    [Fact(DisplayName = "Com a marca na inscrição, a isenção e a inscrição acontecem juntas e o documento exigido na isenção cita o fato da inscrição")]
    public void FatoDaInscricao_EmExigenciaDaIsencaoNoMesmoMomento_ComMarca_NaoRecusa()
    {
        (ProcessoSeletivo processo, FaseCronograma isencao) = ProcessoComIsencaoAntesDaInscricao(inscricaoMarcada: true);

        FaseEfetivaDoFato.RecusaDeFaseDoGatilho(
            processo.Declaracao(), "TEM_RENDA", isencao.Id, FinalidadeFormulario.IsencaoTaxa, FatoDaInscricaoNoCatalogo, SemAgregados).Should().BeNull();
    }
}
