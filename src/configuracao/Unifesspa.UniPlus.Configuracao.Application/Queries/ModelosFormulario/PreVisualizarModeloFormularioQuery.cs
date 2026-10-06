namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;

using System.Text.Json;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As respostas simuladas da pré-visualização: as do formulário, por fato; as etapas dadas como
/// concluídas; os pressupostos, os fatos da inscrição que o modelo cita; e as ocorrências de cada grupo
/// repetível, pela identidade de cada uma.
/// </summary>
public sealed record PreVisualizacaoDoModeloInput(
    IReadOnlyDictionary<string, JsonElement>? Respostas,
    IReadOnlyList<string>? EtapasConcluidas,
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
        ICondicaoAtendimentoRepository condicaoRepository,
        ITipoDeficienciaRepository tipoDeficienciaRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(fatoRepository);
        ArgumentNullException.ThrowIfNull(condicaoRepository);
        ArgumentNullException.ThrowIfNull(tipoDeficienciaRepository);

        ModeloFormulario? modelo = await repository.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false);
        if (modelo is null)
        {
            return Result<PreVisualizacaoDoModeloDto?>.Success(null);
        }

        PreVisualizacaoDoModeloInput simulacao = query.Simulacao ?? new(null, null, null);
        (Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> ocorrencias, List<FieldError> erros) =
            OcorrenciasRecebidas.Ler(simulacao.Grupos, ModeloFormularioErrorCodes.OcorrenciaSimuladaInvalida);
        if (erros.Count > 0)
        {
            return Result<PreVisualizacaoDoModeloDto?>.ValidationFailure(erros);
        }

        // A resposta fora das opções que o catálogo oferta não vale, como no renderizável que o front
        // interpreta: os dois têm de dar o mesmo resultado para a mesma simulação.
        IReadOnlyList<FatoCandidato> fatos = await fatoRepository.ListarTodosAsync(cancellationToken).ConfigureAwait(false);
        // O mesmo conteúdo do renderizável: o do modelo aplicado, o de inscrição com o conjunto básico.
        ConteudoDoModelo conteudo = EscritaDoModelo.ComoAplicado(modelo, fatos);
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes = VocabularioDoCatalogo.OpcoesDoModelo(fatos, conteudo);
        await OpcoesDoCadastroInstitucional.CompletarAsync(opcoes, condicaoRepository, tipoDeficienciaRepository, cancellationToken).ConfigureAwait(false);
        Dictionary<string, IReadOnlySet<string>> ofertas = VocabularioDoCatalogo.Ofertas(opcoes);
        AvaliacaoFormulario avaliacao = AvaliadorFormulario.Avaliar(
            conteudo.ParaAvaliacao(VocabularioDoCatalogo.RegrasDeDerivacao(fatos), VocabularioDoCatalogo.AgregadosDosGrupos(fatos, conteudo.Grupos)),
            new EntradaAvaliacaoFormulario(
                RespostaDeCampo.DentroDaOferta(simulacao.Respostas ?? new Dictionary<string, JsonElement>(), ofertas),
                new HashSet<string>(simulacao.EtapasConcluidas ?? [], StringComparer.Ordinal),
                RespostaDeCampo.ComoFatosConhecidos(simulacao.Pressupostos),
                ocorrencias.ToDictionary(
                    static o => o.Key,
                    o => (IReadOnlyList<OcorrenciaRespondida>)[.. o.Value.Select(r => r with { Respostas = RespostaDeCampo.DentroDaOferta(r.Respostas, ofertas) })],
                    StringComparer.Ordinal)));

        Dictionary<string, ItemDoModelo> itemPorFato = conteudo.Itens
            .Concat(conteudo.Grupos.SelectMany(static g => g.Subitens))
            .ToDictionary(static i => i.FatoCodigo, StringComparer.Ordinal);
        return Result<PreVisualizacaoDoModeloDto?>.Success(new PreVisualizacaoDoModeloDto(
            [.. avaliacao.Itens.Select(i => Item(i, itemPorFato))],
            [.. avaliacao.Termos.Select(static t => new TermoPreVisualizadoDto(t.Codigo, t.Visivel.ToCodigo(), t.Obrigatorio.ToCodigo()))],
            [.. avaliacao.Grupos.Select(g => new GrupoPreVisualizadoDto(
                g.Codigo, g.EtapaCodigo, g.Visivel.ToCodigo(), g.Obrigatorio.ToCodigo(), g.ContagemValida, g.OcorrenciaDoCandidatoValida,
                [.. g.Ocorrencias.Select(o => new OcorrenciaPreVisualizadaDto(o.Id, [.. o.Itens.Select(i => Item(i, itemPorFato))]))]))],
            [.. avaliacao.Etapas.Select(static e => new SecaoPreVisualizadaDto(e.Codigo, e.Visivel.ToCodigo()))]));
    }

    private static ItemPreVisualizadoDto Item(AvaliacaoItem item, Dictionary<string, ItemDoModelo> itemPorFato) => new(
        item.FatoCodigo,
        item.EtapaCodigo,
        item.Visivel.ToCodigo(),
        item.Obrigatorio.ToCodigo(),
        [.. item.RestricoesVioladas.Select(static r => RestricaoValorJson.ParaToken(r.Tipo))],
        item.Impedido.ToCodigo(),
        itemPorFato[item.FatoCodigo].Impedimento?.Mensagem,
        item.Opcoes is { } opcoes ? new OpcoesPreVisualizadasDto(opcoes.Codigos, opcoes.Definitivas) : null);
}
