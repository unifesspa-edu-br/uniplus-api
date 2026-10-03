namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;

using System.Text.Json;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As respostas simuladas da pré-visualização: as do formulário, por fato; as etapas dadas como
/// concluídas; e os pressupostos, os fatos da inscrição que o modelo cita.
/// </summary>
public sealed record PreVisualizacaoDoModeloInput(
    IReadOnlyDictionary<string, JsonElement>? Respostas,
    IReadOnlyList<string>? EtapasConcluidas,
    IReadOnlyDictionary<string, JsonElement>? Pressupostos);

/// <summary>Avalia o modelo contra as respostas simuladas (UNI-REQ-0145); nulo quando o modelo não existe.</summary>
public sealed record PreVisualizarModeloFormularioQuery(Guid Id, PreVisualizacaoDoModeloInput Simulacao)
    : IQuery<PreVisualizacaoDoModeloDto?>;

/// <summary>
/// A pré-visualização usa o mesmo avaliador da execução da inscrição (<see cref="AvaliadorFormulario"/>):
/// o que ela mostra é o que o candidato vai ver. Os derivados por regra do catálogo são resolvidos com
/// as respostas, como o processo os resolveria a partir das regras padrão copiadas.
/// </summary>
public static class PreVisualizarModeloFormularioQueryHandler
{
    public static async Task<PreVisualizacaoDoModeloDto?> Handle(
        PreVisualizarModeloFormularioQuery query,
        IModeloFormularioRepository repository,
        IFatoCandidatoRepository fatoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(fatoRepository);

        ModeloFormulario? modelo = await repository.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false);
        if (modelo is null)
        {
            return null;
        }

        IReadOnlyList<FatoCandidato> fatos = await fatoRepository.ListarTodosAsync(cancellationToken).ConfigureAwait(false);
        PreVisualizacaoDoModeloInput simulacao = query.Simulacao ?? new(null, null, null);

        Dictionary<string, FatoResolvido> conhecidos = RespostaDeCampo.ComoFatosConhecidos(simulacao.Pressupostos);

        AvaliacaoFormulario avaliacao = AvaliadorFormulario.Avaliar(
            modelo.ParaAvaliacao(VocabularioDoCatalogo.RegrasDeDerivacao(fatos)),
            new EntradaAvaliacaoFormulario(
                simulacao.Respostas ?? new Dictionary<string, JsonElement>(),
                new HashSet<string>(simulacao.EtapasConcluidas ?? [], StringComparer.Ordinal),
                conhecidos));

        return new PreVisualizacaoDoModeloDto(
            [.. avaliacao.Itens.Select(i => new ItemPreVisualizadoDto(
                i.FatoCodigo, i.EtapaCodigo, i.Visivel.ToCodigo(), i.Obrigatorio.ToCodigo(),
                [.. i.RestricoesVioladas.Select(static r => RestricaoValorJson.ParaToken(r.Tipo))],
                i.Impedido.ToCodigo(),
                modelo.Conteudo.Itens.First(item => item.FatoCodigo == i.FatoCodigo).Impedimento?.Mensagem))],
            [.. avaliacao.Termos.Select(static t => new TermoPreVisualizadoDto(t.Codigo, t.Visivel.ToCodigo(), t.Obrigatorio.ToCodigo()))]);
    }
}
