namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;

/// <summary>
/// Projeta o agregado do processo seletivo para o contrato REST de leitura. Cada bloco é montado pela
/// projeção do seu assunto; aqui ficam a composição e o que não pertence a nenhum deles: identificação,
/// tipo do processo, unidade administradora, localidade, divulgação e taxa de inscrição.
/// </summary>
internal static class ProjecaoDoProcessoSeletivo
{
    internal static ProcessoSeletivoDto Projetar(ProcessoSeletivo processo) => new(
        processo.Id, processo.Nome, processo.IdentificadorLegivel?.Valor, ProjetarTipoDoProcesso(processo), processo.Status, processo.OrigemCandidatos,
        ProjetarUnidadeAdministradora(processo), ProjetarLocalidade(processo),
        ProjecaoDaEtapaDoProcesso.ProjetarEtapas(processo),
        ProjecaoDasVagas.ProjetarAtendimento(processo.AtendimentoEspecializado), ProjecaoDasVagas.ProjetarDistribuicoes(processo),
        ProjecaoDasVagas.ProjetarBonusRegional(processo.BonusRegional), processo.AplicaBonusRegional,
        processo.NumeroDeOpcoesDeCurso is { } opcoesDeCurso ? new OpcoesDeCursoDto(opcoesDeCurso) : null,
        ProjecaoDasVagas.ProjetarCascata(processo.Cascata),
        ProjecaoDaClassificacao.ProjetarCriteriosDeDesempate(processo), ProjecaoDaClassificacao.Projetar(processo),
        ProjecaoDaFaseDoCronograma.ProjetarCronograma(processo),
        ProjecaoDaColeta.ProjetarDocumentosExigidos(processo), ProjecaoDaColeta.ProjetarRegrasDeComprovacaoSemGrupo(processo),
        ProjecaoDaColeta.ProjetarReferenciaTemporal(processo.ReferenciaTemporalFatos), ProjecaoDaColeta.ProjetarDerivacoes(processo),
        ProjecaoDaColeta.ProjetarFormularios(processo), ProjecaoDaColeta.ProjetarOpcoesDeclaradas(processo),
        ProjetarDivulgacao(processo.ConfiguracaoDivulgacao), ProjetarTaxaDeInscricao(processo.ConfiguracaoTaxaInscricao),
        processo.AlgoritmoContagemPrazo is { } algoritmo ? Referencia(algoritmo) : null, processo.CreatedAt);

    /// <summary>A referência a uma regra versionada, no formato do contrato.</summary>
    internal static ReferenciaRegraDto Referencia(ReferenciaRegra regra) => new(regra.Codigo, regra.Versao, regra.Hash);

    internal static ParametrosRegraPrazoRecursoDto ProjetarParametrosDoPrazo(ParametrosRegraPrazoRecurso parametros) => new(
        parametros.PrazoDeInterposicao.Valor,
        CodigoComRotulo<UnidadePrazo>.De(parametros.PrazoDeInterposicao.Unidade),
        parametros.AdmiteInstanciaSuperior,
        parametros.PrazoDaSegundaInstancia?.Valor,
        parametros.PrazoDaSegundaInstancia is { } segunda ? CodigoComRotulo<UnidadePrazo>.De(segunda.Unidade) : null,
        CodigoComRotulo<EfeitoSuspensivo>.De(parametros.EfeitoSuspensivo),
        CodigoComRotulo<GuardaDaVaga>.De(parametros.GuardaDaVaga));

    private static UnidadeAdministradoraSnapshotDto ProjetarUnidadeAdministradora(ProcessoSeletivo processo) => new(
        processo.UnidadeAdministradoraOrigemId,
        processo.UnidadeAdministradora.Sigla,
        processo.UnidadeAdministradora.Slug,
        processo.UnidadeAdministradora.Nome,
        processo.UnidadeAdministradora.Tipo,
        processo.UnidadeAdministradora.CidadeCodigoIbge,
        processo.UnidadeAdministradora.CidadeNome,
        processo.UnidadeAdministradora.CidadeUf);

    private static ConfiguracaoDivulgacaoDto? ProjetarDivulgacao(ConfiguracaoDivulgacao? configuracao) =>
        configuracao is null ? null : new ConfiguracaoDivulgacaoDto(configuracao.CamposPublicos, configuracao.Justificativa);

    private static ConfiguracaoTaxaInscricaoDto? ProjetarTaxaDeInscricao(ConfiguracaoTaxaInscricao? configuracao) =>
        configuracao is null
            ? null
            : new ConfiguracaoTaxaInscricaoDto(
                configuracao.Cobra,
                configuracao.Valor,
                [.. configuracao.Fundamentos.Select(static f => f.ToCodigo())]);

    private static TipoProcessoSnapshotDto ProjetarTipoDoProcesso(ProcessoSeletivo processo) =>
        new(processo.TipoProcessoOrigemId, processo.TipoProcesso.Codigo, processo.TipoProcesso.Nome);

    private static LocalidadeRegenteDto ProjetarLocalidade(ProcessoSeletivo processo) =>
        new(processo.Localidade.CodigoIbge, processo.Localidade.Nome, processo.Localidade.Uf);
}
