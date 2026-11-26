namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;

using Abstractions;

using Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ProcessosSeletivos.Formularios;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Avalia a configuração viva do processo — o rascunho, nunca o envelope
/// publicado — contra um perfil simulado de candidato: o que cada formulário mostra e exige e os
/// documentos exigidos pelas regras de comprovação (UNI-REQ-0144, UNI-REQ-0145, UNI-REQ-0064).
/// </summary>
/// <remarks>
/// O perfil carrega fatos pessoais e sensíveis simulados: não é gravado nem registrado em log.
/// </remarks>
public sealed record PreVisualizarProcessoSeletivoQuery(Guid ProcessoSeletivoId, PreVisualizacaoDoProcessoInput Simulacao)
    : IQuery<Result<PreVisualizacaoDoProcessoDto>>;

/// <summary>
/// A pré-visualização usa a mesma avaliação do candidato (<see cref="AvaliacaoDaInscricao"/>): o
/// formulário, com as derivações e os agregados do processo intercalados, e as regras de comprovação,
/// sem apresentação nenhuma — a regra de documento aplicável fica pendente, isto é, exigida.
/// </summary>
public static class PreVisualizarProcessoSeletivoQueryHandler
{
    public const string OcorrenciaInvalida = "ProcessoSeletivo.OcorrenciaSimuladaInvalida";

    public static async Task<Result<PreVisualizacaoDoProcessoDto>> Handle(
        PreVisualizarProcessoSeletivoQuery query,
        IProcessoSeletivoRepository repository,
        IFatoCandidatoReader fatoCandidatoReader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);

        ProcessoSeletivo? processo = await repository.ObterComConfiguracaoAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        if (processo is null)
        {
            return Result<PreVisualizacaoDoProcessoDto>.Failure(AcessoAoProcesso.NaoEncontrado());
        }

        return await AvaliarAsync(processo, query.Simulacao ?? new(null, null, null, null), fatoCandidatoReader, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<PreVisualizacaoDoProcessoDto>> AvaliarAsync(
        ProcessoSeletivo processo, PreVisualizacaoDoProcessoInput simulacao, IFatoCandidatoReader fatos, CancellationToken cancellationToken)
    {
        (Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> ocorrencias, List<FieldError> erros) = OcorrenciasRecebidas.Ler(
            simulacao.Grupos?.Select(static g => KeyValuePair.Create(
                g.Key, (IReadOnlyList<OcorrenciaRecebida>?)g.Value?.Select(static o => new OcorrenciaRecebida(o?.Id, o?.Respostas)).ToList())), OcorrenciaInvalida);
        IReadOnlyList<FatoCandidatoView> catalogo = await fatos.ListarAsync(cancellationToken).ConfigureAwait(false);
        Result<DefinicaoAvaliavel> montada = DefinicaoAvaliavelDoProcesso.DaConfiguracaoViva(processo, catalogo);
        if (montada.IsFailure) return Result<PreVisualizacaoDoProcessoDto>.Failure(montada.Error!);
        if (erros.Count > 0) return Result<PreVisualizacaoDoProcessoDto>.ValidationFailure(erros);
        Result<ResultadoDaAvaliacaoDaInscricao> avaliada = AvaliacaoDaInscricao.Avaliar(
            ConfiguracaoAvaliavelDoProcesso.DaConfiguracaoViva(processo, montada.Value!), Entrada(processo, simulacao, ocorrencias));
        return avaliada.IsFailure ? Result<PreVisualizacaoDoProcessoDto>.Failure(avaliada.Error!)
            : Result<PreVisualizacaoDoProcessoDto>.Success(new(Formularios(processo, avaliada.Value!.Formulario), [.. avaliada.Value.Documentos.Itens.Select(Documento)]));
    }

    private static EntradaDaAvaliacaoDaInscricao Entrada(ProcessoSeletivo processo, PreVisualizacaoDoProcessoInput simulacao,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaRespondida>> ocorrencias) =>
        EntradaDaAvaliacaoDaInscricao.Montar(DefinicoesDasRespostas(processo), new EntradaDaAvaliacaoDaInscricao(
            simulacao.Respostas ?? new Dictionary<string, JsonElement>(), ocorrencias, RespostaDeCampo.ComoFatosConhecidos(simulacao.Pressupostos),
            new HashSet<string>((simulacao.PaginasConcluidas ?? []).Where(static e => e is not null).Select(PaginaConcluida).OfType<string>(), StringComparer.Ordinal),
            Envio: null, Apresentacoes: EntradaDaAvaliacaoDaInscricao.SemApresentacoes));

    /// <summary>O código da página na definição do processo; nulo quando o código do formulário não tem o formato válido.</summary>
    private static string? PaginaConcluida(PaginaConcluidaInput pagina) =>
        CodigoDoFormulario.Criar(pagina.FormularioCodigo) is { IsSuccess: true } codigo
            ? DefinicaoDoProcesso.CodigoDaPagina(codigo.Value!, pagina.Pagina)
            : null;

    /// <summary>A definição da resposta de cada fato e subitem que o processo coleta, em qualquer formulário, e a dos derivados do sistema.</summary>
    private static IReadOnlyDictionary<string, DefinicaoDaResposta> DefinicoesDasRespostas(ProcessoSeletivo processo)
    {
        Dictionary<string, DefinicaoDaResposta> definicoes = new(StringComparer.Ordinal);
        foreach (FatoColetado fato in processo.FatosColetados.Concat(processo.GruposColetados.SelectMany(static g => g.Subitens)))
        {
            definicoes.TryAdd(fato.FatoCodigo, DefinicaoDaResposta.De(fato.TipoDeCampo, fato.ClassificacaoProtecao, fato.Formato, fato.FatoCodigo));
        }

        return DerivadosDoSistemaInscricao.ComOsDerivados(definicoes);
    }

    private static List<FormularioSimuladoDto> Formularios(ProcessoSeletivo processo, AvaliacaoFormulario avaliacao)
    {
        Dictionary<string, FatoColetado> itemPorFato = processo.FatosColetados.ToDictionary(static f => f.FatoCodigo, StringComparer.Ordinal);
        Dictionary<string, GrupoColetado> grupoPorCodigo = processo.GruposColetados.ToDictionary(static g => g.Codigo, StringComparer.Ordinal);
        Dictionary<string, TermoExigidoFormulario> termoPorCodigo = TermosPeloCodigoNaDefinicao(processo);

        return [.. processo.Formularios.OrderBy(static f => f.Codigo, CodigoDoFormulario.OrdemOrdinal).Select(formulario => new FormularioSimuladoDto(
            formulario.Codigo.Valor,
            [.. avaliacao.Itens.Where(i => itemPorFato[i.FatoCodigo].FormularioId == formulario.Id).Select(i => Item(i, itemPorFato[i.FatoCodigo]))],
            [.. avaliacao.Grupos.Where(g => grupoPorCodigo[g.Codigo].FormularioId == formulario.Id).Select(g => Grupo(g, grupoPorCodigo[g.Codigo]))],
            [.. avaliacao.Termos.Where(t => termoPorCodigo[t.Codigo].FormularioId == formulario.Id)
                .Select(t => new TermoSimuladoDto(termoPorCodigo[t.Codigo].Codigo, CodigoComRotulo<Ternario>.De(t.Visivel), CodigoComRotulo<Ternario>.De(t.Obrigatorio)))],
            Secoes(formulario, avaliacao)))];
    }

    /// <summary>Os termos do processo pelo código que têm na definição, que leva o do formulário: o código do termo só é único dentro dele.</summary>
    private static Dictionary<string, TermoExigidoFormulario> TermosPeloCodigoNaDefinicao(ProcessoSeletivo processo) =>
        processo.TermosExigidos.ToDictionary(
            t => DefinicaoDoProcesso.CodigoDoTermo(processo.Formularios.Single(f => f.Id == t.FormularioId).Codigo, t.Codigo),
            StringComparer.Ordinal);

    private static GrupoSimuladoDto Grupo(AvaliacaoGrupo grupo, GrupoColetado coletado) => new(
        grupo.Codigo, coletado.PaginaCodigo, CodigoComRotulo<Ternario>.De(grupo.Visivel), CodigoComRotulo<Ternario>.De(grupo.Obrigatorio), grupo.ContagemValida, grupo.OcorrenciaDoCandidatoValida,
        [.. grupo.Ocorrencias.Select(o => new OcorrenciaSimuladaDto(o.Id,
            [.. o.Itens.Select(i => Item(i, coletado.Subitens.Single(s => s.FatoCodigo == i.FatoCodigo), coletado.PaginaCodigo))]))]);

    private static List<SecaoSimuladaDto> Secoes(FormularioProcesso formulario, AvaliacaoFormulario avaliacao) =>
        [.. formulario.Paginas.Where(static e => e.Tipo == TipoPaginaFormulario.Secao).OrderBy(static e => e.Ordem)
            .Select(e => new SecaoSimuladaDto(e.Codigo,
                CodigoComRotulo<Ternario>.De(avaliacao.Paginas.Single(a => a.Codigo == DefinicaoDoProcesso.CodigoDaPagina(formulario.Codigo, e.Codigo)).Visivel)))];

    /// <summary>O item avaliado; o campo de grupo segue a seção do grupo.</summary>
    private static ItemSimuladoDto Item(AvaliacaoItem item, FatoColetado campo, string? paginaDoGrupo = null) => new(
        item.FatoCodigo, paginaDoGrupo ?? campo.PaginaCodigo, CodigoComRotulo<Ternario>.De(item.Visivel), CodigoComRotulo<Ternario>.De(item.Obrigatorio),
        [.. item.RestricoesVioladas.Select(static r => r.Tipo.Codigo())],
        CodigoComRotulo<Ternario>.De(item.Impedido), campo.Impedimento?.Mensagem,
        item.Opcoes is { } opcoes ? new OpcoesSimuladasDto(opcoes.Codigos, opcoes.Definitivas) : null);

    private static DocumentoSimuladoDto Documento(DocumentoAvaliado avaliado)
    {
        DocumentoExigido documento = avaliado.Documento;
        return new(
            documento.Id, documento.TipoDocumentoCodigo, documento.TipoDocumentoNome, documento.Obrigatorio,
            documento.ExigidoNaFaseId, documento.FormularioCodigo?.Valor,
            documento.ExigidoNaEtapaId, Situacao(avaliado.Situacao), avaliado.OcorrenciaId,
            [.. avaliado.Grupos
                .Where(static g => g.Forma == FormaDeComprovacao.QuantidadeMinimaDeItens)
                .Select(static g => new AlternativasSimuladasDto(g.GrupoId, g.Minimo!.Value))]);
    }

    /// <summary>
    /// A situação no vocabulário da pré-visualização, que simula o perfil inteiro: ela diz só se o
    /// documento é exigido, se não é, ou se o perfil simulado ainda não o decide.
    /// </summary>
    private static string Situacao(SituacaoDoDocumento situacao) => situacao switch
    {
        SituacaoDoDocumento.Pendente or SituacaoDoDocumento.Satisfeita => "EXIGIDO",
        SituacaoDoDocumento.NaoExigido => "NAO_EXIGIDO",
        _ => "INDETERMINADO",
    };
}
