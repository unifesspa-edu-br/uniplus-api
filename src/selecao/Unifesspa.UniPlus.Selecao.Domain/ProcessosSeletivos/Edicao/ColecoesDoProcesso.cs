namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// As coleções vivas do agregado, para os planos que as reescrevem depois de a conferência passar.
/// Só a raiz constrói: quem recebe uma instância muta o que o plano calculou e mais nada.
/// </summary>
internal sealed record ColecoesDoProcesso(
    List<EtapaProcesso> Etapas,
    List<FaseCronograma> CronogramaFases,
    List<DocumentoExigido> DocumentosExigidos,
    List<NoExigencia> NosExigencia,
    List<FatoColetado> Campos,
    List<GrupoColetado> GruposColetados,
    List<FormularioProcesso> Formularios,
    List<TermoExigidoFormulario> TermosExigidos,
    List<OpcaoDeclaradaFato> OpcoesDeclaradas,
    List<ConfiguracaoDerivacaoFato> RegrasDerivacao,
    List<ConfiguracaoDistribuicaoVagas> DistribuicaoVagas,
    List<CriterioDesempate> CriteriosDesempate);
