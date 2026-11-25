namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;

using System.Text.Json;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As respostas simuladas da pré-visualização: as do formulário, por fato; as páginas dadas como
/// concluídas; os pressupostos, os fatos da inscrição que o modelo cita; e as ocorrências de cada grupo
/// repetível, pela identidade de cada uma.
/// </summary>
public sealed record PreVisualizacaoDoModeloInput(
    IReadOnlyDictionary<string, JsonElement>? Respostas,
    IReadOnlyList<string>? PaginasConcluidas,
    IReadOnlyDictionary<string, JsonElement>? Pressupostos,
    IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaRecebida>?>? Grupos = null);

/// <summary>
/// Avalia o modelo contra as respostas simuladas (UNI-REQ-0145, UNI-REQ-0146); sucesso nulo quando o
/// modelo não existe, e recusa quando uma ocorrência não tem identidade própria no grupo.
/// </summary>
public sealed record PreVisualizarModeloFormularioQuery(Guid Id, PreVisualizacaoDoModeloInput Simulacao)
    : IQuery<Result<PreVisualizacaoDoModeloDto?>>;

/// <summary>
/// A pré-visualização usa o mesmo avaliador da execução da inscrição (<see cref="AvaliadorFormulario"/>):
/// o que ela mostra é o que o candidato vai ver. Os derivados por regra do catálogo são resolvidos com
/// as respostas, como o processo os resolveria a partir das regras padrão copiadas.
/// </summary>
public static class PreVisualizarModeloFormularioQueryHandler
{
    public static async Task<Result<PreVisualizacaoDoModeloDto?>> Handle(
        PreVisualizarModeloFormularioQuery query,
        IModeloFormularioRepository repository,
        IFatoCandidatoRepository fatoRepository,
        OpcoesDoCadastroInstitucional cadastro,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(fatoRepository);
        ArgumentNullException.ThrowIfNull(cadastro);

        ModeloFormulario? modelo = await repository.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false);
        return modelo is null
            ? Result<PreVisualizacaoDoModeloDto?>.Success(null)
            : await PreVisualizarAsync(modelo, query.Simulacao ?? new(null, null, null), fatoRepository, cadastro, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<PreVisualizacaoDoModeloDto?>> PreVisualizarAsync(
        ModeloFormulario modelo,
        PreVisualizacaoDoModeloInput entrada,
        IFatoCandidatoRepository fatoRepository,
        OpcoesDoCadastroInstitucional cadastro,
        CancellationToken cancellationToken)
    {
        (Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> ocorrencias, List<FieldError> erros) =
            OcorrenciasRecebidas.Ler(entrada.Grupos, ModeloFormularioErrorCodes.OcorrenciaSimuladaInvalida);
        if (erros.Count > 0)
        {
            return Result<PreVisualizacaoDoModeloDto?>.ValidationFailure(erros);
        }

        IReadOnlyList<FatoCandidato> fatos = await fatoRepository.ListarTodosAsync(cancellationToken).ConfigureAwait(false);
        SimulacaoDoModelo simulacao = await SimulacaoDoModelo.MontarAsync(modelo, fatos, cadastro, cancellationToken).ConfigureAwait(false);
        return Result<PreVisualizacaoDoModeloDto?>.Success(Projetar(Avaliar(simulacao, entrada, ocorrencias), simulacao.Conteudo));
    }

    private static AvaliacaoFormulario Avaliar(
        SimulacaoDoModelo simulacao, PreVisualizacaoDoModeloInput entrada, Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> ocorrencias) =>
        AvaliadorFormulario.Avaliar(
            simulacao.Definicao(),
            new EntradaAvaliacaoFormulario(
                simulacao.RespostasDentroDaOferta(entrada.Respostas ?? new Dictionary<string, JsonElement>()),
                new HashSet<string>(entrada.PaginasConcluidas ?? [], StringComparer.Ordinal),
                RespostaDeCampo.ComoFatosConhecidos(entrada.Pressupostos),
                Envio: null,
                OcorrenciasDentroDaOferta(simulacao, ocorrencias)));

    private static Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> OcorrenciasDentroDaOferta(
        SimulacaoDoModelo simulacao, Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> ocorrencias) =>
        ocorrencias.ToDictionary(
            static o => o.Key,
            o => (IReadOnlyList<OcorrenciaRespondida>)[.. o.Value.Select(r => r with { Respostas = simulacao.RespostasDentroDaOferta(r.Respostas) })],
            StringComparer.Ordinal);

    private static PreVisualizacaoDoModeloDto Projetar(AvaliacaoFormulario avaliacao, ConteudoDoModelo conteudo)
    {
        Dictionary<string, ItemDoModelo> itemPorFato = conteudo.Campos().ToDictionary(static i => i.FatoCodigo, StringComparer.Ordinal);
        return new PreVisualizacaoDoModeloDto(
            [.. avaliacao.Itens.Select(i => Item(i, itemPorFato))],
            [.. avaliacao.Termos.Select(static t => new TermoPreVisualizadoDto(t.Codigo, t.Visivel.Codigo(), t.Obrigatorio.Codigo()))],
            [.. avaliacao.Grupos.Select(g => Grupo(g, itemPorFato))],
            [.. avaliacao.Paginas.Select(static e => new SecaoPreVisualizadaDto(e.Codigo, e.Visivel.Codigo()))]);
    }

    private static GrupoPreVisualizadoDto Grupo(AvaliacaoGrupo grupo, Dictionary<string, ItemDoModelo> itemPorFato) => new(
        grupo.Codigo,
        grupo.PaginaCodigo,
        grupo.Visivel.Codigo(),
        grupo.Obrigatorio.Codigo(),
        grupo.ContagemValida,
        grupo.OcorrenciaDoCandidatoValida,
        [.. grupo.Ocorrencias.Select(o => new OcorrenciaPreVisualizadaDto(o.Id, [.. o.Itens.Select(i => Item(i, itemPorFato))]))]);

    private static ItemPreVisualizadoDto Item(AvaliacaoItem item, Dictionary<string, ItemDoModelo> itemPorFato) => new(
        item.FatoCodigo,
        item.PaginaCodigo,
        item.Visivel.Codigo(),
        item.Obrigatorio.Codigo(),
        [.. item.RestricoesVioladas.Select(static r => r.Tipo.Codigo())],
        item.Impedido.Codigo(),
        itemPorFato[item.FatoCodigo].Regras.Impedimento?.Mensagem,
        item.Opcoes is { } opcoes ? new OpcoesPreVisualizadasDto(opcoes.Codigos, opcoes.Definitivas) : null);
}
