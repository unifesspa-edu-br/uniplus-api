namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

public static class ObterProcessoSeletivoQueryHandler
{
    public static async Task<ProcessoSeletivoDto?> Handle(
        ObterProcessoSeletivoQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterComConfiguracaoAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return processo is null ? null : Project(processo);
    }

    internal static ProcessoSeletivoDto Project(ProcessoSeletivo processo) => new(
        processo.Id,
        processo.Nome,
        new TipoProcessoSnapshotDto(
            processo.TipoProcessoOrigemId,
            processo.TipoProcesso.Codigo,
            processo.TipoProcesso.Nome),
        processo.Status,
        processo.OrigemCandidatos,
        new UnidadeAdministradoraSnapshotDto(
            processo.UnidadeAdministradoraOrigemId,
            processo.UnidadeAdministradora.Sigla,
            processo.UnidadeAdministradora.Slug,
            processo.UnidadeAdministradora.Nome,
            processo.UnidadeAdministradora.Tipo,
            processo.UnidadeAdministradora.CidadeCodigoIbge,
            processo.UnidadeAdministradora.CidadeNome,
            processo.UnidadeAdministradora.CidadeUf),
        new LocalidadeRegenteDto(
            processo.Localidade.CodigoIbge,
            processo.Localidade.Nome,
            processo.Localidade.Uf),
        [.. processo.Etapas
            .OrderBy(e => e.Ordem)
            .Select(e => new EtapaProcessoDto(
                e.Id,
                e.Nome,
                e.Carater,
                new TipoEtapaSnapshotDto(e.TipoEtapa.OrigemId, e.TipoEtapa.Codigo, e.TipoEtapa.Nome),
                e.Peso,
                e.NotaMinima,
                e.Ordem))],
        ProjectOfertaAtendimento(processo.OfertaAtendimento),
        [.. processo.DistribuicaoVagas.Select(ProjectDistribuicaoVagas)],
        ProjectBonusRegional(processo.BonusRegional),
        ProjectCascata(processo.Cascata),
        [.. processo.CriteriosDesempate.OrderBy(c => c.Ordem).Select(ProjectCriterioDesempate)],
        ProjectClassificacao(processo),
        [.. processo.CronogramaFases.OrderBy(f => f.Ordem).ThenBy(f => f.Id).Select(ProjectFaseCronograma)],
        [.. processo.DocumentosExigidos.OrderBy(d => d.Id).Select(ProjectDocumentoExigido)],
        [.. processo.RaizesDeExigencia.OrderBy(n => n.Ordem).ThenBy(n => n.Id).Select(ProjectNoExigencia)],
        ProjectReferenciaTemporalFatos(processo.ReferenciaTemporalFatos),
        [.. processo.FatosColetados.OrderBy(f => f.Ordem).Select(ProjectFatoColetado)],
        [.. processo.RegrasDerivacao.OrderBy(c => c.CodigoFato, StringComparer.Ordinal).Select(ProjectConfiguracaoDerivacao)],
        processo.FormularioTitulo,
        processo.FormularioTermoAceiteTexto,
        ProjectConfiguracaoDivulgacao(processo.ConfiguracaoDivulgacao),
        ProjectConfiguracaoTaxaInscricao(processo.ConfiguracaoTaxaInscricao),
        processo.AlgoritmoContagemPrazo is { } algoritmo
            ? new ReferenciaRegraDto(algoritmo.Codigo, algoritmo.Versao, algoritmo.Hash)
            : null,
        processo.CreatedAt);

    private static FatoColetadoDto ProjectFatoColetado(FatoColetado fato) => new(
        fato.FatoCodigo,
        fato.Ordem,
        fato.Rotulo,
        fato.TipoRenderizacao.ToCodigo(),
        fato.Obrigatorio,
        ProjectPredicado(fato.Precondicoes, static c => (c.Clausula, c.Fato, c.Operador, c.Valor),
            static (f, o, v) => new CondicaoPrecondicaoDto(f, o, v)));

    private static ConfiguracaoDerivacaoDto ProjectConfiguracaoDerivacao(ConfiguracaoDerivacaoFato config) => new(
        config.CodigoFato,
        [.. config.Regras.OrderBy(r => r.Ordem).Select(ProjectRegraDerivacao)]);

    private static RegraDerivacaoDto ProjectRegraDerivacao(RegraDerivacaoConfigurada regra) => new(
        regra.Ordem,
        regra.Contribui,
        ProjectPredicado(regra.Condicoes, static c => (c.Clausula, c.Fato, c.Operador, c.Valor),
            static (f, o, v) => new CondicaoDerivacaoDto(f, o, v)));

    /// <summary>
    /// Projeta um predicado relacional (linhas com ordinal de cláusula) na forma normal disjuntiva
    /// tipada: agrupa por cláusula na ordem do ordinal, e dentro de cada cláusula ordena por
    /// conteúdo (fato, operador, valor) para um round-trip determinístico. Ausência de condição é
    /// <see langword="null"/>, nunca uma lista vazia — a projeção reflete o contrato de escrita.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<TCondicao>>? ProjectPredicado<TLinha, TCondicao>(
        IReadOnlyCollection<TLinha> linhas,
        Func<TLinha, (int Clausula, string Fato, Domain.Enums.Operador Operador, JsonElement Valor)> extrair,
        Func<string, string, JsonElement, TCondicao> criar)
    {
        if (linhas.Count == 0)
        {
            return null;
        }

        return [.. linhas
            .Select(extrair)
            .GroupBy(static c => c.Clausula)
            .OrderBy(static g => g.Key)
            .Select(g => (IReadOnlyList<TCondicao>)[.. g
                .OrderBy(static c => c.Fato, StringComparer.Ordinal)
                .ThenBy(static c => c.Operador.ToCodigo(), StringComparer.Ordinal)
                .ThenBy(static c => c.Valor.GetRawText(), StringComparer.Ordinal)
                .Select(c => criar(c.Fato, c.Operador.ToCodigo(), c.Valor))])];
    }

    // issue #892: sem isto, um FIM_FASE/DATA_ESPECIFICA salvo
    // desaparecia do agregado GET, e o formulário de edição podia sobrescrevê-lo ou
    // removê-lo sem querer ao pré-preencher com um estado que não reflete o persistido.
    private static ReferenciaTemporalFatosDto? ProjectReferenciaTemporalFatos(ReferenciaTemporalFatos? referencia) =>
        referencia is null ? null : new ReferenciaTemporalFatosDto(referencia.Tipo.ToCodigo(), referencia.Data, referencia.FaseId);

    private static OfertaAtendimentoEspecializadoDto? ProjectOfertaAtendimento(OfertaAtendimentoEspecializado? oferta)
    {
        if (oferta is null)
        {
            return null;
        }

        return new OfertaAtendimentoEspecializadoDto(
            oferta.Id,
            [.. oferta.Condicoes.Select(c => new OfertaCondicaoDto(c.Id, c.CondicaoOrigemId, c.CondicaoCodigo, c.CondicaoNome))],
            [.. oferta.Recursos.Select(r => new OfertaRecursoDto(r.Id, r.RecursoOrigemId, r.RecursoNome))],
            [.. oferta.TiposDeficiencia.Select(t => new OfertaTipoDeficienciaDto(t.Id, t.TipoDeficienciaOrigemId, t.TipoDeficienciaNome))]);
    }

    /// <summary>
    /// <c>internal</c> (não <c>private</c>) porque <see cref="SimularDistribuicaoVagasQueryHandler"/>
    /// (issue #1282) reaproveita esta mesma projeção — o preview de simulação e a
    /// leitura persistida devolvem exatamente o mesmo shape, sem uma segunda
    /// definição que poderia divergir em silêncio.
    /// </summary>
    internal static ConfiguracaoDistribuicaoVagasDto ProjectDistribuicaoVagas(ConfiguracaoDistribuicaoVagas configuracao) => new(
        configuracao.Id,
        configuracao.OfertaCursoOrigemId,
        configuracao.VoBase,
        configuracao.Pr,
        new ReferenciaRegraDto(configuracao.RegraDistribuicao.Codigo, configuracao.RegraDistribuicao.Versao, configuracao.RegraDistribuicao.Hash),
        configuracao.RegraAjuste is { } regraAjuste
            ? new ReferenciaRegraDto(regraAjuste.Codigo, regraAjuste.Versao, regraAjuste.Hash)
            : null,
        configuracao.ReferenciaDemografica is { } demografica
            ? new ReferenciaReservaDemograficaSnapshotDto(
                demografica.OrigemId, demografica.CensoReferencia, demografica.PpiPercentual, demografica.QuilombolaPercentual, demografica.PcdPercentual, demografica.BaseLegal)
            : null,
        [.. configuracao.Modalidades.Select(m => new ModalidadeSelecionadaDto(
            m.Id, m.ModalidadeOrigemId, m.Codigo, m.Descricao, m.NaturezaLegal.ToCodigo(), m.ComposicaoVagas.ToCodigo(),
            m.ComposicaoOrigemCodigo, m.RegraRemanejamento.ToCodigo(), m.RemanejamentoDestino, m.RemanejamentoPar, m.RemanejamentoFallback,
            m.CriteriosCumulativos, m.AcaoQuandoIndeferido, m.BaseLegal, m.QuantidadeDeclarada))],
        [.. configuracao.VagasOfertadas.Select(v => new VagaOfertadaDto(v.Id, v.ModalidadeOrigemId, v.ModalidadeCodigo, v.Quantidade))],
        configuracao.VrNominal,
        configuracao.VrFinal,
        configuracao.Estouro,
        configuracao.CapadoEmVo,
        configuracao.TotalPublicado);

    private static ConfiguracaoBonusRegionalDto? ProjectBonusRegional(ConfiguracaoBonusRegional? bonus)
    {
        if (bonus is null)
        {
            return null;
        }

        return new ConfiguracaoBonusRegionalDto(
            bonus.Id,
            new ReferenciaRegraDto(bonus.Regra.Codigo, bonus.Regra.Versao, bonus.Regra.Hash),
            bonus.Fator,
            bonus.Teto,
            bonus.MunicipioConvenio,
            bonus.BaseLegal);
    }

    private static ConfiguracaoDivulgacaoDto? ProjectConfiguracaoDivulgacao(ConfiguracaoDivulgacao? configuracao) =>
        configuracao is null ? null : new ConfiguracaoDivulgacaoDto(configuracao.CamposPublicos, configuracao.Justificativa);

    private static ConfiguracaoTaxaInscricaoDto? ProjectConfiguracaoTaxaInscricao(ConfiguracaoTaxaInscricao? configuracao) =>
        configuracao is null
            ? null
            : new ConfiguracaoTaxaInscricaoDto(
                configuracao.Cobra,
                configuracao.Valor,
                [.. configuracao.Fundamentos.Select(static f => f.ToCodigo())]);

    private static ConfiguracaoCascataRemanejamentoDto? ProjectCascata(ConfiguracaoCascataRemanejamento? cascata)
    {
        if (cascata is null)
        {
            return null;
        }

        return new ConfiguracaoCascataRemanejamentoDto(
            cascata.Id,
            new ReferenciaRegraDto(cascata.Regra.Codigo, cascata.Regra.Versao, cascata.Regra.Hash),
            cascata.FallbackCodigo,
            [.. cascata.Destinos
                .OrderBy(d => d.ModalidadeOrigemCodigo, StringComparer.Ordinal)
                .ThenBy(d => d.Ordem)
                .Select(d => new DestinoRemanejamentoDto(d.Id, d.ModalidadeOrigemCodigo, d.Ordem, d.ModalidadeDestinoCodigo))]);
    }

    private static CriterioDesempateDto ProjectCriterioDesempate(CriterioDesempate criterio)
    {
        ReferenciaRegraDto regra = new(criterio.Regra.Codigo, criterio.Regra.Versao, criterio.Regra.Hash);

        return criterio.Args switch
        {
            ArgsDesempateMaiorNotaEtapa args => new CriterioDesempateDto(
                criterio.Id, criterio.Ordem, regra, args.EtapaRef, null, null, null, null),
            ArgsDesempateIdoso args => new CriterioDesempateDto(
                criterio.Id, criterio.Ordem, regra, null, args.IdadeMinima, null, null, null),
            ArgsDesempatePredicadoFato args => new CriterioDesempateDto(
                criterio.Id, criterio.Ordem, regra, null, null,
                args.Condicao.Fato,
                args.Condicao.Operador.ToCodigo(),
                ProjetarValorCondicao(args.Condicao.Valor)),
            _ => new CriterioDesempateDto(criterio.Id, criterio.Ordem, regra, null, null, null, null, null),
        };
    }

    /// <summary>
    /// Achata <c>CondicaoDnf.Valor</c> (JSON escalar ou array) para o formato
    /// textual de <see cref="DTOs.CriterioDesempateDto.Valor"/> — projeção de
    /// leitura, não round-trip byte-a-byte (esse é o do envelope canônico).
    /// </summary>
    private static string ProjetarValorCondicao(JsonElement valor) =>
        valor.ValueKind == JsonValueKind.String ? valor.GetString()! : valor.GetRawText();

    private static ConfiguracaoClassificacaoDto? ProjectClassificacao(ProcessoSeletivo processo)
    {
        ConfiguracaoClassificacao? classificacao = processo.Classificacao;
        if (classificacao is null)
        {
            return null;
        }

        return new ConfiguracaoClassificacaoDto(
            classificacao.Id,
            new ReferenciaRegraDto(classificacao.RegraCalculo.Codigo, classificacao.RegraCalculo.Versao, classificacao.RegraCalculo.Hash),
            classificacao.RegraArredondamento is { } arredondamento
                ? new ReferenciaRegraDto(arredondamento.Codigo, arredondamento.Versao, arredondamento.Hash)
                : null,
            classificacao.CasasArredondamento,
            new ReferenciaRegraDto(classificacao.RegraOrdemAlocacao.Codigo, classificacao.RegraOrdemAlocacao.Versao, classificacao.RegraOrdemAlocacao.Hash),
            classificacao.NOpcoesAlocacao,
            [.. classificacao.RegrasEliminacao.Select(ProjectRegraEliminacao)],
            processo.ConcorrenciaDuplaAplicavel(),
            classificacao.BaseadoEmEnem);
    }

    private static RegraEliminacaoDto ProjectRegraEliminacao(RegraEliminacao regra)
    {
        ReferenciaRegraDto referenciaRegra = new(regra.Regra.Codigo, regra.Regra.Versao, regra.Regra.Hash);

        return regra.Args switch
        {
            ArgsElimNotaMinimaEtapa args => new RegraEliminacaoDto(regra.Id, referenciaRegra, args.EtapaRef, args.NotaMinima, null),
            ArgsElimCorteRedacao args => new RegraEliminacaoDto(regra.Id, referenciaRegra, null, null, args.Minimo),
            _ => new RegraEliminacaoDto(regra.Id, referenciaRegra, null, null, null),
        };
    }

    private static FaseCronogramaDto ProjectFaseCronograma(FaseCronograma fase) => new(
        fase.Id,
        fase.Ordem,
        fase.FaseCanonicaOrigemId,
        fase.Codigo,
        fase.DonoInstitucional,
        fase.OrigemData.ToCodigo(),
        fase.AgrupaEtapas,
        fase.PermiteComplementacao,
        fase.ProduzResultado,
        fase.ResultadoDefinitivo,
        fase.ColetaInscricao,
        fase.ColetaSolicitacaoIsencao,
        fase.Inicio,
        fase.Fim,
        fase.AtoProduzidoCodigo,
        fase.AtoProduzidoEfeitoIrreversivel,
        [.. fase.BancasRequeridas.Select(static b => new BancaRequeridaDto(b.Id, b.TipoBancaOrigemId, b.Codigo))],
        fase.RegraRecurso is { } regraRecurso ? ProjectRegraRecursoFase(regraRecurso) : null);

    private static DocumentoExigidoDto ProjectDocumentoExigido(DocumentoExigido documento) => new(
        documento.Id,
        documento.ExigidoNaFaseId,
        documento.TipoDocumentoOrigemId,
        documento.TipoDocumentoCodigo,
        documento.TipoDocumentoNome,
        documento.TipoDocumentoCategoria,
        ProjectAplicabilidade(documento.Aplicabilidade),
        documento.Obrigatorio,
        documento.ConsequenciaIndeferimento,
        documento.GrupoSatisfacaoId,
        [.. documento.Condicoes.OrderBy(c => c.Clausula).ThenBy(c => c.Id).Select(ProjectCondicaoGatilho)],
        [.. documento.BasesLegais.OrderBy(b => b.Id).Select(ProjectBaseLegal)],
        ProjectIdadeMaximaEmissao(documento.IdadeMaximaEmissao),
        ProjectFormatosPermitidos(documento.FormatosPermitidos),
        documento.TamanhoMaximoBytes);

    /// <summary>Projeta um nó da árvore de satisfação (<see cref="NoExigencia"/>, Story #920) recursivamente — mesmo formato de <c>NoExigenciaInput</c> (comando de escrita).</summary>
    private static NoExigenciaDto ProjectNoExigencia(NoExigencia no) => new(
        no.Id,
        no.Tipo.ToCodigo(),
        no.Tipo == TipoNo.Folha ? ProjectDocumentoExigido(no.DocumentoExigido!) : null,
        no.QuantidadeMinima,
        no.Consequencia,
        [.. no.BasesLegais.OrderBy(static b => b.Id).Select(ProjectBaseLegalDeNo)],
        [.. no.Filhos.OrderBy(static f => f.Ordem).ThenBy(static f => f.Id).Select(ProjectNoExigencia)],
        no.ChaveDistincao?.ToCodigo(),
        no.DataReferencia,
        no.OcorrenciasEsperadas,
        no.RepetePorEntidade?.ToCodigo());

    private static BaseLegalDto ProjectBaseLegalDeNo(NoExigenciaBaseLegal baseLegal) => new(
        baseLegal.Id, baseLegal.Referencia, baseLegal.Abrangencia.ToCodigo(), baseLegal.Status.ToCodigo(), baseLegal.Observacao);

    /// <summary>
    /// Projeta <see cref="FormatosPermitidos"/> (Story #918) no MESMO valor JSON polimórfico
    /// que <c>DefinirDocumentosExigidosCommandHandler.ResolverFormatosPermitidos</c> aceita
    /// de volta — <c>"QUALQUER"</c> ou um array de <c>{formato, tamanhoMaximoBytesMax}</c> —
    /// fechando o round-trip GET→PUT sem transformação do cliente.
    /// </summary>
    private static JsonElement ProjectFormatosPermitidos(FormatosPermitidos formatosPermitidos) =>
        formatosPermitidos.Qualquer
            ? JsonSerializer.SerializeToElement("QUALQUER")
            : JsonSerializer.SerializeToElement(formatosPermitidos.Lista!.Select(static e => new
            {
                formato = e.Formato.ToCodigo(),
                tamanhoMaximoBytesMax = e.TamanhoMaximoBytesMax,
            }));

    // Valor como texto JSON canônico (GetRawText) — o mesmo PUT que aceita este DTO de
    // volta (DefinirDocumentosExigidosCommandHandler.InterpretarValor) reparseia texto
    // JSON válido como tal, fechando o round-trip GET→PUT sem perda de tipo.
    private static CondicaoGatilhoDto ProjectCondicaoGatilho(CondicaoGatilho condicao) => new(
        condicao.Id,
        condicao.Clausula,
        condicao.Fato,
        condicao.Operador.ToCodigo(),
        condicao.Valor.GetRawText());

    private static IdadeMaximaEmissaoDto? ProjectIdadeMaximaEmissao(IdadeMaximaEmissao? idade) =>
        idade is null ? null : new IdadeMaximaEmissaoDto(idade.Valor, idade.Unidade.ToCodigo(), idade.ReferenciaTipo.ToCodigo(), idade.Data, idade.ReferenciaFaseId);

    private static BaseLegalDto ProjectBaseLegal(DocumentoExigidoBaseLegal baseLegal) => new(
        baseLegal.Id,
        baseLegal.Referencia,
        baseLegal.Abrangencia.ToCodigo(),
        baseLegal.Status.ToCodigo(),
        baseLegal.Observacao);

    // Emite o mesmo token de wire aceito por DefinirDocumentosExigidosCommandValidator
    // ("GERAL"/"CONDICIONAL") — Aplicabilidade.ToString() produziria "Geral"/"Condicional"
    // (PascalCase), que o validator rejeitaria num reenvio direto do GET para o PUT.
    private static string ProjectAplicabilidade(Aplicabilidade aplicabilidade) => aplicabilidade switch
    {
        Aplicabilidade.Geral => "GERAL",
        Aplicabilidade.Condicional => "CONDICIONAL",
        _ => throw new InvalidOperationException($"Aplicabilidade '{aplicabilidade}' não deveria estar persistida — DocumentoExigido.Criar recusa Aplicabilidade.Nenhuma."),
    };

    private static RegraRecursoFaseDto ProjectRegraRecursoFase(RegraRecursoFase regraRecurso) => new(
        regraRecurso.Id,
        new ReferenciaRegraDto(regraRecurso.Regra.Codigo, regraRecurso.Regra.Versao, regraRecurso.Regra.Hash),
        new ArgsRegraPrazoRecursoDto(
            regraRecurso.Args.PrazoValor,
            regraRecurso.Args.PrazoUnidade,
            regraRecurso.Args.AtoAncoraCodigo,
            regraRecurso.Args.SuspensividadePrimeiraInstanciaValor,
            regraRecurso.Args.SuspensividadePrimeiraInstanciaUnidade,
            regraRecurso.Args.SuspensividadeSegundaInstanciaValor,
            regraRecurso.Args.SuspensividadeSegundaInstanciaUnidade));
}
