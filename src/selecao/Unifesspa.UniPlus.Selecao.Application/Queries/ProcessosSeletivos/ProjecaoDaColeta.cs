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
/// Projeta para o contrato REST o que o processo coleta e exige do candidato: os formulários com seus
/// fatos, grupos e termos, as regras de derivação, as opções declaradas e os documentos exigidos com a
/// árvore de satisfação.
/// </summary>
internal static class ProjecaoDaColeta
{
    internal static FormularioDto ProjetarFormulario(ProcessoSeletivo processo, FormularioProcesso formulario) => new(
        EstruturaFormulario.ParaToken(formulario.Finalidade),
        formulario.FaseId,
        formulario.Titulo,
        formulario.ModeloOrigemId,
        formulario.ModeloOrigemCodigo,
        [.. formulario.Etapas.OrderBy(static e => e.Ordem).Select(static e => new EtapaFormularioDto(
            e.Codigo, e.Ordem, EstruturaFormulario.ParaToken(e.Tipo), EstruturaFormulario.ParaToken(e.Bloco), e.Titulo, e.Descricao, e.Aviso,
            e.Exibicao?.ToDto()))],
        [.. processo.FatosColetados.Where(f => f.Finalidade == formulario.Finalidade).OrderBy(static f => f.Ordem)
            .Select(f => ProjetarFato(processo, f))],
        [.. processo.TermosExigidos.Where(t => t.Finalidade == formulario.Finalidade).OrderBy(static t => t.Ordem)
            .Select(static t => t.ToDto())],
        [.. processo.GruposColetados.Where(g => g.Finalidade == formulario.Finalidade).OrderBy(static g => g.Ordem)
            .Select(g => ProjetarGrupo(processo, g))]);

    private static GrupoColetadoDto ProjetarGrupo(ProcessoSeletivo processo, GrupoColetado g) => new(
        g.Codigo, g.Ordem, g.EtapaCodigo, g.Rotulo, g.Minimo, g.Maximo, g.IncluiCandidato, g.Exibicao?.ToDto(), g.Obrigatoriedade.ToDto(),
        [.. g.Subitens.OrderBy(static s => s.Ordem).Select(s => ProjetarFato(processo, s))]);

    private static FatoColetadoDto ProjetarFato(ProcessoSeletivo processo, FatoColetado fato) => new(
        fato.FatoCodigo,
        fato.Ordem,
        fato.Rotulo,
        fato.TipoRenderizacao.ToCodigo(),
        fato.Obrigatoriedade.ToDto(),
        ProjetarPredicado(fato.Precondicoes, static c => (c.Clausula, c.Fato, c.Operador, c.Valor),
            static (f, o, v) => new CondicaoPrecondicaoDto(f, o, v)),
        Domain.ProcessosSeletivos.Opcoes.RegrasDasOpcoesDeclaradas.OrigemDasOpcoes(fato) == OrigemValoresColeta.OpcoesDoProcesso
            ? [.. processo.OpcoesDoProcesso(fato.FatoCodigo).Select(ProjetarOpcao)]
            : null,
        fato.EtapaCodigo,
        fato.Formato,
        fato.Ajuda,
        fato.PedirConfirmacao,
        [.. fato.Restricoes.Select(static r => r.ToDto())],
        fato.Impedimento?.ToDto());

    private static OpcaoDoProcessoDto ProjetarOpcao(OpcaoDoProcesso opcao) => new(opcao.Codigo, opcao.Rotulo, opcao.Ordem);

    private static OpcaoDoProcessoDto ProjetarOpcao(OpcaoDeclaradaFato opcao) => new(opcao.Codigo, opcao.Rotulo, opcao.Ordem);

    internal static OpcoesDeclaradasDoFatoDto ProjetarOpcoesDoFato(IGrouping<string, OpcaoDeclaradaFato> opcoes) => new(
        opcoes.Key, [.. opcoes.OrderBy(static o => o.Ordem).Select(ProjetarOpcao)]);

    internal static ConfiguracaoDerivacaoDto ProjetarDerivacao(ConfiguracaoDerivacaoFato config) => new(
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
                .ThenBy(static c => c.Operador.ToCodigo(), StringComparer.Ordinal)
                .ThenBy(static c => c.Valor.GetRawText(), StringComparer.Ordinal)
                .Select(c => criar(c.Fato, c.Operador.ToCodigo(), c.Valor))])];
    }

    // Sem a referência temporal, um FIM_FASE ou DATA_ESPECIFICA salvo desapareceria do agregado, e o
    // formulário de edição podia sobrescrevê-lo ou removê-lo sem querer ao pré-preencher com um estado
    // que não reflete o persistido.
    internal static ReferenciaTemporalFatosDto? ProjetarReferenciaTemporal(ReferenciaTemporalFatos? referencia) =>
        referencia is null ? null : new ReferenciaTemporalFatosDto(referencia.Tipo.ToCodigo(), referencia.Data, referencia.FaseId);

    internal static DocumentoExigidoDto ProjetarDocumento(DocumentoExigido documento) => new(
        documento.Id,
        documento.ExigidoNaFaseId,
        documento.TipoDocumentoOrigemId,
        documento.TipoDocumentoCodigo,
        documento.TipoDocumentoNome,
        documento.TipoDocumentoCategoria,
        ProjetarAplicabilidade(documento.Aplicabilidade),
        documento.Obrigatorio,
        documento.ConsequenciaIndeferimento,
        [.. documento.Condicoes.OrderBy(c => c.Clausula).ThenBy(c => c.Id).Select(ProjetarCondicaoDoGatilho)],
        [.. documento.BasesLegais.OrderBy(b => b.Id).Select(ProjetarBaseLegal)],
        ProjetarIdadeMaximaDeEmissao(documento.IdadeMaximaEmissao),
        ProjetarFormatosPermitidos(documento.FormatosPermitidos),
        documento.TamanhoMaximoBytes,
        documento.ExigidoNaEtapaId,
        documento.Modelo is { } modelo
            ? new ModeloDaExigenciaDto(modelo.ModeloId, modelo.NomeArquivo, ModeloDeDocumento.TokenDe(modelo.Formato), modelo.HashSha256)
            : null,
        documento.Finalidade is { } finalidade ? EstruturaFormulario.ParaToken(finalidade) : null,
        ClassificacoesDeProtecao.ParaTokenCanonico(documento.ClassificacaoProtecao),
        documento.FinalidadeTratamento,
        documento.HipoteseLegal,
        [.. documento.FatosComprovados]);

    // Um nó da árvore de satisfação, recursivamente, no mesmo formato de NoExigenciaInput (comando de escrita).
    internal static NoExigenciaDto ProjetarNo(NoExigencia no) => new(
        no.Id,
        no.Tipo.ToCodigo(),
        no.Tipo == TipoNo.Folha ? ProjetarDocumento(no.DocumentoExigido!) : null,
        no.QuantidadeMinima,
        no.Consequencia,
        [.. no.BasesLegais.OrderBy(static b => b.Id).Select(ProjetarBaseLegalDoNo)],
        [.. no.Filhos.OrderBy(static f => f.Ordem).ThenBy(static f => f.Id).Select(ProjetarNo)],
        no.ChaveDistincao?.ToCodigo(),
        no.DataReferencia,
        no.OcorrenciasEsperadas,
        no.RepetePorEntidade);

    private static BaseLegalDto ProjetarBaseLegalDoNo(NoExigenciaBaseLegal baseLegal) => new(
        baseLegal.Id, baseLegal.Referencia, baseLegal.Abrangencia.ToCodigo(), baseLegal.Status.ToCodigo(), baseLegal.Observacao);

    private static BaseLegalDto ProjetarBaseLegal(DocumentoExigidoBaseLegal baseLegal) => new(
        baseLegal.Id,
        baseLegal.Referencia,
        baseLegal.Abrangencia.ToCodigo(),
        baseLegal.Status.ToCodigo(),
        baseLegal.Observacao);

    /// <summary>
    /// Projeta <see cref="FormatosPermitidos"/> no mesmo valor JSON polimórfico que
    /// <c>EscritaDosFormatosPermitidos.Resolver</c> aceita de volta — <c>"QUALQUER"</c> ou um array de
    /// <c>{formato, tamanhoMaximoBytesMax}</c> —, fechando o round-trip GET e PUT sem transformação do cliente.
    /// </summary>
    private static JsonElement ProjetarFormatosPermitidos(FormatosPermitidos formatosPermitidos) =>
        formatosPermitidos.Qualquer
            ? JsonSerializer.SerializeToElement("QUALQUER")
            : JsonSerializer.SerializeToElement(formatosPermitidos.Lista!.Select(static e => new
            {
                formato = e.Formato.ToCodigo(),
                tamanhoMaximoBytesMax = e.TamanhoMaximoBytesMax,
            }));

    // O valor vai como texto JSON canônico (GetRawText): o mesmo PUT que aceita este DTO de volta
    // reparseia texto JSON válido como tal, fechando o round-trip sem perda de tipo.
    private static CondicaoGatilhoDto ProjetarCondicaoDoGatilho(CondicaoGatilho condicao) => new(
        condicao.Id,
        condicao.Clausula,
        condicao.Fato,
        condicao.Operador.ToCodigo(),
        condicao.Valor.GetRawText());

    private static IdadeMaximaEmissaoDto? ProjetarIdadeMaximaDeEmissao(IdadeMaximaEmissao? idade) =>
        idade is null
            ? null
            : new IdadeMaximaEmissaoDto(idade.Valor, idade.Unidade.ToCodigo(), idade.ReferenciaTipo.ToCodigo(), idade.Data, idade.ReferenciaFaseId);

    // Emite o mesmo token de wire aceito pelo validador de documentos exigidos ("GERAL" e "CONDICIONAL"):
    // Aplicabilidade.ToString() produziria PascalCase, que o validador rejeitaria num reenvio direto do GET para o PUT.
    private static string ProjetarAplicabilidade(Aplicabilidade aplicabilidade) => aplicabilidade switch
    {
        Aplicabilidade.Geral => "GERAL",
        Aplicabilidade.Condicional => "CONDICIONAL",
        _ => throw new InvalidOperationException($"Aplicabilidade '{aplicabilidade}' não deveria estar persistida — DocumentoExigido.Criar recusa Aplicabilidade.Nenhuma."),
    };
}
