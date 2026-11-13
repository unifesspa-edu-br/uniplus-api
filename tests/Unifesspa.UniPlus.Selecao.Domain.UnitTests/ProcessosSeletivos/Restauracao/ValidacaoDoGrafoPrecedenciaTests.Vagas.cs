namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ProcessoSeletivoRestaurarConfiguracaoTests;

public sealed partial class ValidacaoDoGrafoPrecedenciaTests
{
    private const int VagasDaOferta = 10;
    private const int VagasDaModalidadeAdicional = 2;

    private static ModalidadeSelecionada AmplaConcorrencia(int vagas, PredicadoDnf? regra = null) =>
        ModalidadeSelecionada.Criar(
            Id(900), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, null,
            RegraRemanejamentoModalidade.Nenhuma, null, null, null,
            regra ?? RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "base legal", quantidadeDeclarada: vagas).Value!;

    private static PredicadoDnf RegraDeAplicabilidadeDivergenteDaAmpla() =>
        RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.CotaReservada);

    private static ModalidadeSelecionada AcaoAfirmativaComDestino(string acaoQuandoIndeferido) =>
        ModalidadeSelecionada.Criar(
            Id(901), "V", null, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, null,
            RegraRemanejamentoModalidade.DestinoUnico, "AC", null, null,
            RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.AcaoAfirmativa), acaoQuandoIndeferido, "base legal",
            quantidadeDeclarada: VagasDaModalidadeAdicional).Value!;

    private static ModalidadeSelecionada CotaDaLeiSemAOpcaoPelasCotas() =>
        ModalidadeSelecionada.Criar(
            Id(902), "LI_EP", null, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.SuplementarAoTotal, null,
            RegraRemanejamentoModalidade.SegueCascata, null, null, null, PredicadoDnf.SemClausula, null, "base legal",
            quantidadeDeclarada: VagasDaModalidadeAdicional).Value!;

    private static ModalidadeSelecionada AcaoAfirmativaSemRemanejamento() =>
        ModalidadeSelecionada.Criar(
            Id(903), "AC_PCD", null, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiraDe, "AC",
            RegraRemanejamentoModalidade.Nenhuma, null, null, null,
            RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.AcaoAfirmativa), null, "base legal",
            quantidadeDeclarada: VagasDaModalidadeAdicional).Value!;

    private static ConfiguracaoDistribuicaoVagas Oferta(Guid ofertaCursoOrigemId, PredicadoDnf? regraDaAmpla = null, params ModalidadeSelecionada[] adicionais) =>
        ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId, voBase: VagasDaOferta, pr: 1m, Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
            regraAjuste: null, referenciaDemografica: null,
            [AmplaConcorrencia(VagasDaOferta - (adicionais.Length * VagasDaModalidadeAdicional), regraDaAmpla), .. adicionais],
            ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
}
