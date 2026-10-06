namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;

using System.Text.Json;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As regras de um formulário renderizável — de um arquivo importado, de um caso do corpus, de um
/// modelo ou de um processo — e as respostas simuladas: as do formulário, por fato; as ocorrências de
/// cada grupo, pela identidade de cada uma; as seções dadas como concluídas, pelo código delas nas
/// regras; e os pressupostos.
/// </summary>
public sealed record AvaliacaoSemCadastroInput(
    FormularioPortavel Regras,
    IReadOnlyDictionary<string, JsonElement>? Respostas,
    IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaRecebida>?>? Grupos,
    IReadOnlyList<string>? EtapasConcluidas,
    IReadOnlyDictionary<string, JsonElement>? Pressupostos);

/// <summary>
/// Avalia as regras recebidas contra as respostas pelo mesmo avaliador da inscrição (ADR-0139), sem
/// cadastro nenhum: nada é gravado, e o conteúdo, que pode trazer dado pessoal simulado, não é
/// registrado em log.
/// </summary>
public sealed record AvaliarFormularioSemCadastroQuery(AvaliacaoSemCadastroInput Simulacao) : IQuery<Result<AvaliacaoPortavel>>;

/// <summary>
/// A avaliação autoritativa que a simulação confere: as regras que não formam um formulário são
/// recusadas com a causa, e a ocorrência sem identidade própria no grupo, com o caminho dela.
/// </summary>
public static class AvaliarFormularioSemCadastroQueryHandler
{
    public static Result<AvaliacaoPortavel> Handle(AvaliarFormularioSemCadastroQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        AvaliacaoSemCadastroInput simulacao = query.Simulacao;

        (Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> ocorrencias, List<FieldError> erros) =
            OcorrenciasRecebidas.Ler(simulacao.Grupos, FormularioPortavelErrorCodes.OcorrenciaInvalida);
        if (erros.Count > 0)
        {
            return Result<AvaliacaoPortavel>.ValidationFailure(erros);
        }

        Result<AvaliacaoFormulario> avaliacao = simulacao.Regras.Avaliar(new EntradaAvaliacaoFormulario(
            simulacao.Respostas ?? new Dictionary<string, JsonElement>(),
            new HashSet<string>(simulacao.EtapasConcluidas ?? [], StringComparer.Ordinal),
            RespostaDeCampo.ComoFatosConhecidos(simulacao.Pressupostos),
            ocorrencias));

        return avaliacao.IsSuccess
            ? Result<AvaliacaoPortavel>.Success(AvaliacaoPortavel.De(avaliacao.Value!))
            : Result<AvaliacaoPortavel>.Failure(avaliacao.Error!);
    }
}
