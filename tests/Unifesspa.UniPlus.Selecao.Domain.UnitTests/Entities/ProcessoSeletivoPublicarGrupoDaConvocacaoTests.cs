namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaCoerenciaDaConsequencia;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaPublicacaoDoProcessoSeletivo;

public sealed class ProcessoSeletivoPublicarGrupoDaConvocacaoTests
{
    [Fact(DisplayName = "Exigência pelo grupo da convocação alcança só esse grupo: ELIMINA para o convocado em AC não confronta a ação da cota")]
    public void AvaliarConformidade_GatilhoPeloGrupoDaConvocacao_SoAlcancaEsseGrupo()
    {
        ModalidadeSelecionada ppi = NovaModalidadeComAcao(
            "LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, "RETIRA_A_MODALIDADE");
        ProcessoSeletivo processo = NovoProcessoComModalidade(ppi);
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaCondicionalPorModalidadeComConsequencia(
                faseId, "CERTIFICADO_ENSINO_MEDIO", "Certificado de conclusão do ensino médio", "ESCOLAR",
                ModalidadesFederaisLei12711.Ac, "ELIMINA_DO_PROCESSO", fatoDeModalidade: "MODALIDADE_CONVOCACAO"), 0).Value!]).IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo))
            .Single(static i => i.Codigo == "exigencia_consequencia_incoerente_com_acao_da_vaga").Ok
            .Should().BeTrue("o convocado em AC não é o da cota, cuja ação de indeferimento é reclassificar");
    }

    private static DocumentoExigido ExigenciaDeHeteroidentificacaoPorModalidadeEConvocacao(Guid faseId) => new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "HETEROIDENTIFICACAO", "Parecer de heteroidentificação", "ETNICO_RACIAL", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(faseId, null, FormularioDeTeste.CodigoDoPapel(FinalidadeFormulario.Inscricao)))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, [
                CondicaoGatilho.Criar(0, "MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement("LB_PPI")).Value!,
                CondicaoGatilho.Criar(0, "MODALIDADE_CONVOCACAO", Operador.Igual, JsonSerializer.SerializeToElement(ModalidadesFederaisLei12711.Ac)).Value!,
            ]))
            .ComIndeferimento(new IndeferimentoDoDocumento(false, "ELIMINA_DO_PROCESSO", [BaseLegalResolvidaQualquer()]))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, null, null))
            .ComFatos(null)
            .Criar().Value!;

    [Fact(DisplayName = "Cláusula que cita a modalidade de concorrência e o grupo da convocação alcança as duas")]
    public void AvaliarConformidade_ClausulaComOsDoisFatosDeModalidade_AlcancaCadaUm()
    {
        ModalidadeSelecionada ppi = NovaModalidadeComAcao(
            "LB_PPI", NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, "RETIRA_A_MODALIDADE");
        ProcessoSeletivo processo = NovoProcessoComModalidade(ppi);
        DocumentoExigido exigencia = ExigenciaDeHeteroidentificacaoPorModalidadeEConvocacao(processo.CronogramaFases.Single().Id);
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(exigencia, 0).Value!]).IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo))
            .Single(static i => i.Codigo == "exigencia_consequencia_incoerente_com_acao_da_vaga").Ok
            .Should().BeFalse("quem concorre na cota e foi convocado em AC recebe a exigência, e ELIMINA contraria a ação da cota");
    }
}
