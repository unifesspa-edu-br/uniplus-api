namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using DTOs;

using Mappings;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Projeta o agregado do processo seletivo para o contrato REST de leitura. Cada bloco é montado pela
/// projeção do seu assunto; aqui ficam a composição e o que não pertence a nenhum deles: identificação,
/// classificação, divulgação e taxa.
/// </summary>
internal static class ProjecaoDoProcessoSeletivo
{
    internal static ProcessoSeletivoDto Projetar(ProcessoSeletivo processo) => new(
        processo.Id,
        processo.Nome,
        processo.IdentificadorLegivel?.Valor,
        new TipoProcessoSnapshotDto(processo.TipoProcessoOrigemId, processo.TipoProcesso.Codigo, processo.TipoProcesso.Nome),
        processo.Status,
        processo.OrigemCandidatos,
        ProjetarUnidadeAdministradora(processo),
        new LocalidadeRegenteDto(processo.Localidade.CodigoIbge, processo.Localidade.Nome, processo.Localidade.Uf),
        [.. processo.Etapas.OrderBy(e => e.Ordem).Select(ProjecaoDaEtapaDoProcesso.ProjetarEtapa)],
        ProjecaoDasVagas.ProjetarAtendimento(processo.AtendimentoEspecializado),
        [.. processo.DistribuicaoVagas.Select(ProjecaoDasVagas.ProjetarDistribuicao)],
        ProjecaoDasVagas.ProjetarBonusRegional(processo.BonusRegional),
        processo.AplicaBonusRegional,
        processo.NumeroDeOpcoesDeCurso is { } opcoesDeCurso ? new OpcoesDeCursoDto(opcoesDeCurso) : null,
        ProjecaoDasVagas.ProjetarCascata(processo.Cascata),
        [.. processo.CriteriosDesempate.OrderBy(c => c.Ordem).Select(ProjetarCriterioDeDesempate)],
        ProjetarClassificacao(processo),
        [.. processo.CronogramaFases.OrderBy(f => f.Ordem).ThenBy(f => f.Id).Select(ProjecaoDaFaseDoCronograma.ProjetarFase)],
        [.. processo.DocumentosExigidos.OrderBy(d => d.Id).Select(ProjecaoDaColeta.ProjetarDocumento)],
        [.. processo.RaizesDeExigencia.OrderBy(n => n.Ordem).ThenBy(n => n.Id).Select(ProjecaoDaColeta.ProjetarNo)],
        ProjecaoDaColeta.ProjetarReferenciaTemporal(processo.ReferenciaTemporalFatos),
        [.. processo.RegrasDerivacao.OrderBy(c => c.CodigoFato, StringComparer.Ordinal).Select(ProjecaoDaColeta.ProjetarDerivacao)],
        [.. processo.Formularios.OrderBy(static f => f.Finalidade).Select(f => ProjecaoDaColeta.ProjetarFormulario(processo, f))],
        [.. processo.OpcoesDeclaradas
            .GroupBy(static o => o.FatoCodigo, StringComparer.Ordinal)
            .OrderBy(static g => g.Key, StringComparer.Ordinal)
            .Select(ProjecaoDaColeta.ProjetarOpcoesDoFato)],
        ProjetarDivulgacao(processo.ConfiguracaoDivulgacao),
        ProjetarTaxaDeInscricao(processo.ConfiguracaoTaxaInscricao),
        processo.AlgoritmoContagemPrazo is { } algoritmo ? Referencia(algoritmo) : null,
        processo.CreatedAt);

    /// <summary>A referência a uma regra versionada, no formato do contrato.</summary>
    internal static ReferenciaRegraDto Referencia(ReferenciaRegra regra) => new(regra.Codigo, regra.Versao, regra.Hash);

    internal static ParametrosRegraPrazoRecursoDto ProjetarParametrosDoPrazo(ParametrosRegraPrazoRecurso parametros) => new(
        parametros.PrazoValor,
        parametros.PrazoUnidade,
        parametros.SuspensividadePrimeiraInstanciaValor,
        parametros.SuspensividadePrimeiraInstanciaUnidade,
        parametros.AdmiteInstanciaSuperior,
        parametros.SuspensividadeSegundaInstanciaValor,
        parametros.SuspensividadeSegundaInstanciaUnidade);

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

    private static CriterioDesempateDto ProjetarCriterioDeDesempate(CriterioDesempate criterio)
    {
        ReferenciaRegraDto regra = Referencia(criterio.Regra);

        return criterio.Parametros switch
        {
            ParametrosDesempateMaiorNotaEtapa parametros => new CriterioDesempateDto(
                criterio.Id, criterio.Ordem, regra, parametros.EtapaRef, null, null, null, null, null),
            ParametrosDesempateIdoso parametros => new CriterioDesempateDto(
                criterio.Id, criterio.Ordem, regra, null, parametros.IdadeMinima, null, null, null, null),
            ParametrosDesempatePredicadoFato parametros => new CriterioDesempateDto(
                criterio.Id, criterio.Ordem, regra, null, null,
                parametros.Condicao.Fato,
                parametros.Condicao.Operador.ToCodigo(),
                ProjetarValorDaCondicao(parametros.Condicao.Valor),
                null),
            ParametrosDesempateMaiorNotaAreaEnem parametros => new CriterioDesempateDto(
                criterio.Id, criterio.Ordem, regra, null, null, null, null, null, [.. parametros.Areas]),
            _ => new CriterioDesempateDto(criterio.Id, criterio.Ordem, regra, null, null, null, null, null, null),
        };
    }

    /// <summary>
    /// <c>CondicaoDnf.Valor</c> como texto JSON canônico, o mesmo formato de
    /// <see cref="CondicaoGatilhoDto.Valor"/>: as duas leituras carregam a mesma tripla
    /// (fato, operador, valor) e o mesmo PUT as recebe de volta.
    /// </summary>
    /// <remarks>
    /// Desachatar a string — devolver <c>PRETA</c> em vez de <c>"PRETA"</c> — perdia o tipo no
    /// ida-e-volta: um valor de texto <c>"18"</c> voltava como <c>18</c> e era reinterpretado
    /// como número na gravação seguinte, mudando qual ramo da matriz operador × domínio o
    /// validaria.
    /// </remarks>
    private static string ProjetarValorDaCondicao(JsonElement valor) => valor.GetRawText();

    private static ConfiguracaoClassificacaoDto? ProjetarClassificacao(ProcessoSeletivo processo)
    {
        ConfiguracaoClassificacao? classificacao = processo.Classificacao;
        if (classificacao is null)
        {
            return null;
        }

        return new ConfiguracaoClassificacaoDto(
            classificacao.Id,
            Referencia(classificacao.RegraCalculo),
            classificacao.RegraArredondamento is { } arredondamento ? Referencia(arredondamento) : null,
            classificacao.CasasArredondamento,
            Referencia(classificacao.RegraOrdemAlocacao),
            [.. classificacao.RegrasEliminacao.Select(ProjetarRegraDeEliminacao)],
            Domain.ProcessosSeletivos.Vagas.RegrasDaDistribuicaoDeVagas.ConcorrenciaDuplaAplicavel(processo.Declaracao()),
            classificacao.BaseadoEmEnem,
            classificacao.ResolucaoPesoAreaEnem,
            [.. classificacao.QuadroPesoAreaEnem
                .OrderBy(static g => g.GrupoAreaEnem.Codigo, StringComparer.Ordinal)
                .Select(ProjetarGrupoDePesos)],
            classificacao.EscalaNotaFinal);
    }

    private static GrupoPesoAreaEnemCongeladoDto ProjetarGrupoDePesos(GrupoPesoAreaEnemCongelado g) => new(
        new GrupoAreaEnemSnapshotDto(g.GrupoAreaEnem.Codigo, g.GrupoAreaEnem.Rotulo),
        g.BaseLegal,
        [.. g.Areas
            .OrderBy(static a => a.Codigo, StringComparer.Ordinal)
            .Select(static a => new AreaPesoAreaEnemCongeladaDto(a.Codigo, a.Rotulo, a.Peso, a.Corte))]);

    private static RegraEliminacaoDto ProjetarRegraDeEliminacao(RegraEliminacao regra)
    {
        ReferenciaRegraDto referenciaRegra = Referencia(regra.Regra);

        return regra.Parametros switch
        {
            ParametrosElimNotaMinimaEtapa parametros => new RegraEliminacaoDto(regra.Id, referenciaRegra, parametros.EtapaRef, parametros.NotaMinima, null, null),
            ParametrosElimAusenciaNaEtapa parametros => new RegraEliminacaoDto(regra.Id, referenciaRegra, parametros.EtapaRef, null, null, null),
            ParametrosElimCorteEmArea parametros => new RegraEliminacaoDto(regra.Id, referenciaRegra, null, null, parametros.Minimo, parametros.AreaCodigo),
            _ => new RegraEliminacaoDto(regra.Id, referenciaRegra, null, null, null, null),
        };
    }
}
