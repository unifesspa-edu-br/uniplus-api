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
        [.. processo.Etapas.OrderBy(e => e.Ordem).Select(ProjecaoDoCronogramaEEtapas.ProjetarEtapa)],
        ProjecaoDasVagas.ProjetarAtendimento(processo.OfertaAtendimento),
        [.. processo.DistribuicaoVagas.Select(ProjecaoDasVagas.ProjetarDistribuicao)],
        ProjecaoDasVagas.ProjetarBonusRegional(processo.BonusRegional),
        processo.AplicaBonusRegional,
        processo.NumeroDeOpcoesDeCurso is { } opcoesDeCurso ? new OpcoesDeCursoDto(opcoesDeCurso) : null,
        ProjecaoDasVagas.ProjetarCascata(processo.Cascata),
        [.. processo.CriteriosDesempate.OrderBy(c => c.Ordem).Select(ProjetarCriterioDeDesempate)],
        ProjetarClassificacao(processo),
        [.. processo.CronogramaFases.OrderBy(f => f.Ordem).ThenBy(f => f.Id).Select(ProjecaoDoCronogramaEEtapas.ProjetarFase)],
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

        return criterio.Args switch
        {
            ArgsDesempateMaiorNotaEtapa args => new CriterioDesempateDto(
                criterio.Id, criterio.Ordem, regra, args.EtapaRef, null, null, null, null, null),
            ArgsDesempateIdoso args => new CriterioDesempateDto(
                criterio.Id, criterio.Ordem, regra, null, args.IdadeMinima, null, null, null, null),
            ArgsDesempatePredicadoFato args => new CriterioDesempateDto(
                criterio.Id, criterio.Ordem, regra, null, null,
                args.Condicao.Fato,
                args.Condicao.Operador.ToCodigo(),
                ProjetarValorDaCondicao(args.Condicao.Valor),
                null),
            ArgsDesempateMaiorNotaAreaEnem args => new CriterioDesempateDto(
                criterio.Id, criterio.Ordem, regra, null, null, null, null, null, [.. args.Areas]),
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
            Domain.ProcessosSeletivos.Vagas.RegrasDaDistribuicaoDeVagas.ConcorrenciaDuplaAplicavel(Domain.ProcessosSeletivos.Edicao.EstadoDaConfiguracao.De(processo)),
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

        return regra.Args switch
        {
            ArgsElimNotaMinimaEtapa args => new RegraEliminacaoDto(regra.Id, referenciaRegra, args.EtapaRef, args.NotaMinima, null, null),
            ArgsElimAusenciaNaEtapa args => new RegraEliminacaoDto(regra.Id, referenciaRegra, args.EtapaRef, null, null, null),
            ArgsElimCorteEmArea args => new RegraEliminacaoDto(regra.Id, referenciaRegra, null, null, args.Minimo, args.AreaCodigo),
            _ => new RegraEliminacaoDto(regra.Id, referenciaRegra, null, null, null, null),
        };
    }
}
