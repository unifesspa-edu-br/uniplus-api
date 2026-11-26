namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConsequenciasDoDescumprimento;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaCoerenciaDaConsequencia;

/// <summary>Os processos e as leituras que os testes da consequência da falta de envio dos formulários montam.</summary>
internal static class ApoioDosFormulariosComConsequencia
{
    internal static IReadOnlyList<ItemConformidade> ItensDoFormulario(ConfiguracaoDeclarada configuracao, ProcessoSeletivo processo) =>
    [
        .. ItensDasConsequenciasDoDescumprimento.Avaliar(configuracao, Fatos(processo))
            .Where(static item => item.Codigo.StartsWith("formulario_", StringComparison.Ordinal)),
    ];

    internal static IReadOnlyList<FieldError> Causas(ConfiguracaoDeclarada configuracao, ProcessoSeletivo processo) =>
        [.. CoerenciaDaConsequenciaDoDescumprimento.Causas(configuracao, Fatos(processo), Cronograma(configuracao), EtapasDoProcesso.Das(configuracao.Avaliacao.Etapas, configuracao.Origem))];

    internal static CronogramaDoProcesso Cronograma(ConfiguracaoDeclarada configuracao) =>
        CronogramaDoProcesso.Do(configuracao.Cronograma.CronogramaFases, configuracao.Origem);

    internal static FatosDeModalidade Fatos(ProcessoSeletivo processo) => CadastrosDeTeste.Para(processo).FatosDeModalidade;

    internal static void ExigirNoFormulario(ProcessoSeletivo processo, FinalidadeFormulario finalidade, string modalidade)
    {
        Guid fase = finalidade == FinalidadeFormulario.Habilitacao ? AcrescentarFaseDaHabilitacao(processo) : processo.CronogramaFases.Single().Id;
        DocumentoExigido exigencia = new DocumentoExigidoBuilder()
            .ComMomento(new MomentoDaExigencia(fase, null, FormularioDeTeste.CodigoDoPapel(finalidade)))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Condicional,
                [CondicaoGatilho.Criar(0, "MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement(modalidade)).Value!]))
            .ComIndeferimento(new IndeferimentoDoDocumento(false, null, []))
            .Construir();
        Result definido = processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(exigencia, 0).Value!]);
        definido.IsSuccess.Should().BeTrue(string.Join("; ", definido.Errors.Select(static e => $"{e.Field}: {e.Error.Code}")));
    }

    internal static Guid AcrescentarFaseDaHabilitacao(ProcessoSeletivo processo, bool coletaInscricao = false)
    {
        FaseCronograma habilitacao = FaseCronograma.Criar(ChaveLocal.Criar("habilitacao").Value!,
            OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
                Guid.CreateVersion7(), FormularioProcesso.CodigoFaseHabilitacao, OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: coletaInscricao, ColetaSolicitacaoIsencao: false)),
            new DecisoesDeCronogramaDaFase(
                2, Inicio: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar(FormularioProcesso.CodigoFaseHabilitacao, PapelDaPublicacaoPrevista.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;
        processo.DefinirCronogramaFases([.. processo.CronogramaFases, habilitacao], []).IsSuccess.Should().BeTrue();
        return habilitacao.Id;
    }

    /// <summary>
    /// Um processo com uma oferta cuja modalidade LB_PPI retira a modalidade do indeferido, e o formulário da finalidade
    /// com a consequência declarada; o formulário de inscrição do processo é o primeiro, e o de isenção, o segundo.
    /// </summary>
    internal static ProcessoSeletivo ProcessoCom(FinalidadeFormulario finalidade, ConsequenciaDoDescumprimento? consequencia)
    {
        ModalidadeSelecionada modalidade = NovaModalidadeComAcao("LB_PPI", NaturezaLegalModalidade.CotaReservada,
            ComposicaoVagasModalidade.DentroDaReservaLegal, "RETIRA_A_MODALIDADE");
        ProcessoSeletivo processo = NovoProcessoComModalidade(modalidade);
        Guid? fase = finalidade == FinalidadeFormulario.Inscricao ? processo.FormularioDe(FinalidadeFormulario.Inscricao)!.FaseId : null;
        Result definido = processo.DefinirFormulario(finalidade, fase, null, FormularioDeTeste.Paginas(), null, consequencia);
        definido.IsSuccess.Should().BeTrue(definido.Error?.Message);
        return processo;
    }
}
