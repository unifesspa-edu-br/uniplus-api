namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using DTOs;

using Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Leituras;

/// <summary>
/// Projeta para o contrato REST o que o processo coleta e exige do candidato: os formulários com seus
/// fatos, grupos e termos, as regras de derivação, as opções declaradas e os documentos exigidos com as
/// regras de comprovação.
/// </summary>
internal static class ProjecaoDaColeta
{
    private static readonly JsonSerializerOptions OpcoesDaProjecao = new(JsonSerializerDefaults.Web);

    internal static IReadOnlyList<FormularioDto> ProjetarFormularios(ProcessoSeletivo processo) =>
        [.. processo.Formularios.OrderBy(static f => f.Finalidade).Select(f => ProjetarFormulario(processo, f))];

    internal static IReadOnlyList<OpcoesDeclaradasDoFatoDto> ProjetarOpcoesDeclaradas(ProcessoSeletivo processo) =>
        [.. processo.OpcoesDeclaradas
            .GroupBy(static o => o.FatoCodigo, StringComparer.Ordinal)
            .OrderBy(static g => g.Key, StringComparer.Ordinal)
            .Select(ProjetarOpcoesDoFato)];

    internal static IReadOnlyList<ConfiguracaoDerivacaoDto> ProjetarDerivacoes(ProcessoSeletivo processo) =>
        [.. processo.RegrasDerivacao.OrderBy(static c => c.CodigoFato, StringComparer.Ordinal).Select(ProjetarDerivacao)];

    internal static IReadOnlyList<DocumentoExigidoDto> ProjetarDocumentosExigidos(ProcessoSeletivo processo) =>
        [.. processo.DocumentosExigidos.OrderBy(static d => d.Id).Select(ProjetarDocumento)];

    internal static IReadOnlyList<RegraDeComprovacaoDto> ProjetarRegrasDeComprovacaoSemGrupo(ProcessoSeletivo processo) =>
        [.. processo.RegrasDeComprovacaoSemGrupo.OrderBy(static n => n.Ordem).ThenBy(static n => n.Id).Select(ProjetarRegra)];

    private static FormularioDto ProjetarFormulario(ProcessoSeletivo processo, FormularioProcesso formulario) => new(
        CodigoComRotulo<FinalidadeFormulario>.De(formulario.Finalidade),
        formulario.FaseId,
        formulario.Titulo,
        formulario.ModeloDeOrigem is { } origem ? new ModeloDeOrigemDto(origem.Id, origem.Codigo) : null,
        [.. formulario.Paginas.OrderBy(static e => e.Ordem).Select(ProjetarPagina)],
        ProjetarColeta(processo, formulario.Finalidade),
        [.. processo.TermosExigidos.Where(t => t.Finalidade == formulario.Finalidade).OrderBy(static t => t.Ordem).Select(static t => t.ToDto())])
    {
        ConsequenciaDaFaltaDeEnvio = formulario.ConsequenciaDaFaltaDeEnvio is { } consequencia ? CodigoComRotulo<ConsequenciaDoDescumprimento>.De(consequencia) : null,
    };

    private static PaginaDoFormularioDto ProjetarPagina(PaginaDoFormulario pagina) => new(
        pagina.Codigo,
        pagina.Ordem,
        pagina.Tipo.Codigo(),
        pagina.Bloco?.Codigo(),
        new ApresentacaoDaPaginaDto(pagina.Titulo, pagina.Descricao, pagina.Aviso),
        pagina.Exibicao?.ToDto());

    private static ColetaDoFormularioDto ProjetarColeta(ProcessoSeletivo processo, FinalidadeFormulario finalidade) => new(
        [.. processo.FatosColetados.Where(f => f.Finalidade == finalidade).OrderBy(static f => f.Ordem).Select(f => ProjetarFato(processo, f))],
        [.. processo.GruposColetados.Where(g => g.Finalidade == finalidade).OrderBy(static g => g.Ordem).Select(g => ProjetarGrupo(processo, g))]);

    private static GrupoColetadoDto ProjetarGrupo(ProcessoSeletivo processo, GrupoColetado g) => new(
        g.Codigo, g.Ordem, g.PaginaCodigo, g.Rotulo,
        new OcorrenciasDoGrupoDto(g.Minimo, g.Maximo, g.IncluiCandidato),
        new ExibicaoEObrigatoriedadeDto(g.Exibicao?.ToDto(), g.Obrigatoriedade.ToDto()),
        [.. g.Subitens.OrderBy(static s => s.Ordem).Select(s => ProjetarFato(processo, s))]);

    private static FatoColetadoDto ProjetarFato(ProcessoSeletivo processo, FatoColetado fato) => new(
        fato.FatoCodigo,
        fato.Ordem,
        fato.PaginaCodigo,
        new ApresentacaoDoCampoDto(fato.Rotulo, fato.TipoDeCampo.Codigo(), fato.Ajuda, fato.PedirConfirmacao),
        ProjetarRegras(fato),
        fato.Formato,
        ProjetarOpcoesDoFato(processo, fato));

    private static RegrasDoCampoDto ProjetarRegras(FatoColetado fato) => new(
        ProjetarPredicado(fato.Precondicoes, static c => (c.Clausula, c.Fato, c.Operador, c.Valor),
            static (f, o, v) => new CondicaoPrecondicaoDto(f, o, v)),
        fato.Obrigatoriedade.ToDto(),
        [.. fato.Restricoes.Select(static r => r.ToDto())],
        fato.Impedimento?.ToDto());

    private static IReadOnlyList<OpcaoDoProcessoDto>? ProjetarOpcoesDoFato(ProcessoSeletivo processo, FatoColetado fato) =>
        Domain.ProcessosSeletivos.Opcoes.RegrasDasOpcoesDeclaradas.OrigemDasOpcoes(fato) == OrigemValoresColeta.OpcoesDoProcesso
            ? [.. processo.OpcoesDoProcesso(fato.FatoCodigo).Select(ProjetarOpcao)] : null;

    private static OpcaoDoProcessoDto ProjetarOpcao(OpcaoDoProcesso opcao) => new(opcao.Codigo, opcao.Rotulo, opcao.Ordem);

    private static OpcaoDoProcessoDto ProjetarOpcao(OpcaoDeclaradaFato opcao) => new(opcao.Codigo, opcao.Rotulo, opcao.Ordem);

    private static OpcoesDeclaradasDoFatoDto ProjetarOpcoesDoFato(IGrouping<string, OpcaoDeclaradaFato> opcoes) => new(
        opcoes.Key, [.. opcoes.OrderBy(static o => o.Ordem).Select(ProjetarOpcao)]);

    private static ConfiguracaoDerivacaoDto ProjetarDerivacao(ConfiguracaoDerivacaoFato config) => new(
        config.CodigoFato,
        [.. config.Regras.OrderBy(r => r.Ordem).Select(ProjetarRegraDeDerivacao)]);

    private static RegraDerivacaoDto ProjetarRegraDeDerivacao(RegraDerivacaoConfigurada regra) => new(
        regra.Ordem,
        regra.Contribui,
        ProjetarPredicado(regra.Condicoes, static c => (c.Clausula, c.Fato, c.Operador, c.Valor),
            static (f, o, v) => new CondicaoDerivacaoDto(f, o, v)));

    /// <summary>
    /// Projeta um predicado relacional (linhas com ordinal de cláusula) na forma normal disjuntiva
    /// tipada: agrupa por cláusula na ordem do ordinal, e dentro de cada cláusula ordena por
    /// conteúdo (fato, operador, valor) para um round-trip determinístico. Ausência de condição é
    /// <see langword="null"/>, nunca uma lista vazia — a projeção reflete o contrato de escrita.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<TCondicao>>? ProjetarPredicado<TLinha, TCondicao>(
        IReadOnlyCollection<TLinha> linhas,
        Func<TLinha, (int Clausula, string Fato, Operador Operador, JsonElement Valor)> extrair,
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
                .ThenBy(static c => c.Operador.Codigo(), StringComparer.Ordinal)
                .ThenBy(static c => c.Valor.GetRawText(), StringComparer.Ordinal)
                .Select(c => criar(c.Fato, c.Operador.Codigo(), c.Valor))])];
    }

    // Sem a referência temporal, um FIM_FASE ou DATA_ESPECIFICA salvo desapareceria do agregado, e o
    // formulário de edição podia sobrescrevê-lo ou removê-lo sem querer ao pré-preencher com um estado
    // que não reflete o persistido.
    internal static ReferenciaTemporalFatosDto? ProjetarReferenciaTemporal(ReferenciaTemporalFatos? referencia) =>
        referencia is null ? null : new ReferenciaTemporalFatosDto(CodigoComRotulo<ReferenciaTipo>.De(referencia.Tipo), referencia.Data, referencia.FaseId);

    private static DocumentoExigidoDto ProjetarDocumento(DocumentoExigido documento) => new(
        documento.Id,
        new TipoDocumentoDaExigenciaDto(
            documento.TipoDocumentoOrigemId, documento.TipoDocumentoCodigo, documento.TipoDocumentoNome, documento.TipoDocumentoCategoria,
            new ProtecaoDeDadosDto(
                ClassificacoesDeProtecao.ParaTokenCanonico(documento.ClassificacaoProtecao), documento.FinalidadeTratamento, documento.HipoteseLegal)),
        ProjetarMomento(documento),
        new AplicabilidadeDaExigenciaDto(
            CodigoComRotulo<Aplicabilidade>.De(documento.Aplicabilidade),
            [.. documento.Condicoes.OrderBy(c => c.Clausula).ThenBy(c => c.Id).Select(ProjetarCondicaoDoGatilho)]),
        new IndeferimentoDoDocumentoDto(
            documento.Obrigatorio, ProjetarConsequencia(documento.ConsequenciaDoDescumprimento), [.. documento.BasesLegais.OrderBy(b => b.Id).Select(ProjetarBaseLegal)], documento.AbrePrazoParaReenvio),
        ProjetarRequisitos(documento),
        [.. documento.FatosComprovados])
    { Chave = documento.Chave.Valor };

    private static MomentoDaExigenciaDto ProjetarMomento(DocumentoExigido documento) => new(
        documento.ExigidoNaFaseId,
        documento.ExigidoNaEtapaId,
        documento.Finalidade is { } finalidade ? CodigoComRotulo<FinalidadeFormulario>.De(finalidade) : null);

    private static RequisitosDoDocumentoDto ProjetarRequisitos(DocumentoExigido documento) => new(
        ProjetarFormatosPermitidos(documento.FormatosPermitidos),
        documento.TamanhoMaximoBytes,
        ProjecaoDoPrazoEDaValidadeDoDocumento.Projetar(documento.PrazoDeEmissao),
        ProjecaoDoPrazoEDaValidadeDoDocumento.Projetar(documento.Validade),
        ProjetarModelo(documento.Modelo));

    private static ModeloDaExigenciaDto? ProjetarModelo(ModeloDaExigencia? modelo) => modelo is null ? null
        : new ModeloDaExigenciaDto(modelo.ModeloId, modelo.NomeArquivo, CodigoComRotulo<FormatoDeDocumento>.De(modelo.Formato), modelo.HashSha256);

    private static RegraDeComprovacaoDto ProjetarRegra(RegraDeComprovacao regra) => new(
        regra.Id,
        CodigoComRotulo<FormaDeComprovacao>.De(regra.Forma),
        regra.Forma == FormaDeComprovacao.Documento ? ProjetarDocumento(regra.DocumentoExigido!) : null,
        new CardinalidadeQualificadaDto(regra.QuantidadeMinima, ProjetarDistincao(regra.Distincao)),
        new IndeferimentoDoGrupoDto(ProjetarConsequencia(regra.ConsequenciaDoDescumprimento), [.. regra.BasesLegais.OrderBy(static b => b.Id).Select(ProjetarBaseLegalDaRegra)], regra.AbrePrazoParaReenvio),
        [.. regra.Itens.OrderBy(static f => f.Ordem).ThenBy(static f => f.Id).Select(ProjetarRegra)],
        regra.GrupoDoFormularioParaRepeticao);

    private static CodigoComRotulo<ConsequenciaDoDescumprimento>? ProjetarConsequencia(ConsequenciaDoDescumprimento? valor) =>
        valor is null ? null : CodigoComRotulo<ConsequenciaDoDescumprimento>.De(valor.Value);

    private static DistincaoDasApresentacoesDto? ProjetarDistincao(DistincaoDasApresentacoes? distincao) =>
        distincao is null ? null : new DistincaoDasApresentacoesDto(distincao.Criterio is { } criterio ? CodigoComRotulo<CriterioDeDistincaoDasApresentacoes>.De(criterio) : null, distincao.DataReferencia, distincao.OcorrenciasEsperadas);

    private static BaseLegalDto ProjetarBaseLegalDaRegra(BaseLegalDaComprovacao baseLegal) => new(
        baseLegal.Id, baseLegal.Referencia, CodigoComRotulo<TipoAbrangencia>.De(baseLegal.Abrangencia), CodigoComRotulo<StatusBaseLegal>.De(baseLegal.Situacao), baseLegal.Observacao);

    private static BaseLegalDto ProjetarBaseLegal(DocumentoExigidoBaseLegal baseLegal) => new(
        baseLegal.Id,
        baseLegal.Referencia,
        CodigoComRotulo<TipoAbrangencia>.De(baseLegal.Abrangencia),
        CodigoComRotulo<StatusBaseLegal>.De(baseLegal.Situacao),
        baseLegal.Observacao);

    /// <summary>
    /// Projeta <see cref="FormatosPermitidos"/> como valor JSON polimórfico: <c>"QUALQUER"</c> ou um array de
    /// <c>{formato, tamanhoMaximoBytesMax}</c>, em que o formato sai com código e rótulo, como todo enum da resposta.
    /// </summary>
    private static JsonElement ProjetarFormatosPermitidos(FormatosPermitidos formatosPermitidos) =>
        formatosPermitidos.Qualquer
            ? JsonSerializer.SerializeToElement("QUALQUER", OpcoesDaProjecao)
            : JsonSerializer.SerializeToElement(
                formatosPermitidos.Lista!.Select(static e => new
                {
                    formato = CodigoComRotulo<FormatoPermitido>.De(e.Formato),
                    tamanhoMaximoBytesMax = e.TamanhoMaximoBytesMax,
                }),
                OpcoesDaProjecao);

    // O valor vai como texto JSON canônico (GetRawText): o mesmo PUT que aceita este DTO de volta
    // reparseia texto JSON válido como tal, fechando o round-trip sem perda de tipo.
    private static CondicaoGatilhoDto ProjetarCondicaoDoGatilho(CondicaoGatilho condicao) => new(
        condicao.Id,
        condicao.Clausula,
        condicao.Fato,
        condicao.Operador.Codigo(),
        condicao.Valor.GetRawText());
}
