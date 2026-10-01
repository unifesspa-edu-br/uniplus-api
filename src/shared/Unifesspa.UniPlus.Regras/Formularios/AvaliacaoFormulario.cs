namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O que o avaliador recebe além da descrição do formulário: as respostas do candidato, as etapas
/// que ele já concluiu e os fatos já conhecidos de fora do formulário — os coletados por uma
/// finalidade anterior e os derivados pelo sistema, como o grupo da convocação.
/// </summary>
/// <remarks>
/// Uma resposta a fato que o formulário não produz é ignorada: a descrição é a autoridade sobre o
/// que existe, e uma resposta órfã não cria fato.
/// </remarks>
public sealed record EntradaAvaliacaoFormulario(
    IReadOnlyDictionary<string, JsonElement> Respostas,
    IReadOnlySet<string> EtapasConcluidas,
    IReadOnlyDictionary<string, FatoResolvido> FatosConhecidos,
    IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaRespondida>>? RespostasDosGrupos = null);

/// <summary>
/// Uma ocorrência respondida de um grupo repetível, pela identidade estável que ela tem — a mesma que
/// correlaciona os documentos exigidos por membro (UNI-REQ-0069) — e as respostas dos subitens. Sem
/// identidade, ou com a mesma de outra ocorrência do grupo, o documento do membro não teria a quem
/// se ligar; por isso o avaliador recusa as duas com <see cref="ArgumentException"/>.
/// </summary>
public sealed record OcorrenciaRespondida(string Id, IReadOnlyDictionary<string, JsonElement> Respostas)
{
    public string Id { get; } = !string.IsNullOrWhiteSpace(Id) ? Id : throw new ArgumentException("A ocorrência precisa de identidade.", nameof(Id));
}

/// <summary>O resultado da avaliação: o estado final de cada fato, de cada item e de cada termo.</summary>
public sealed record AvaliacaoFormulario(
    IReadOnlyDictionary<string, FatoResolvido> Fatos,
    IReadOnlyList<AvaliacaoItem> Itens,
    IReadOnlyList<AvaliacaoTermo> Termos,
    IReadOnlyList<AvaliacaoGrupo> Grupos);

/// <summary>
/// A avaliação de um grupo repetível: se aparece, se é obrigatório, se a contagem de ocorrências cabe
/// no mínimo e no máximo, o estado do grupo inteiro e cada ocorrência avaliada. Os fatos de membro
/// não entram nos fatos do candidato: ficam em cada ocorrência.
/// </summary>
/// <remarks>
/// O estado segue o UNI-REQ-0074: oculto, não aplicável; sem resposta ou com contagem que não vale,
/// pendente se é obrigatório ou a etapa está aberta, e não informado se é opcional com a etapa
/// concluída; a lista vazia do opcional é não informado; e resolvido quando todas as ocorrências
/// resolveram.
/// </remarks>
public sealed record AvaliacaoGrupo(
    string Codigo,
    string EtapaCodigo,
    Ternario Visivel,
    Ternario Obrigatorio,
    EstadoFato Estado,
    bool ContagemValida,
    IReadOnlyList<AvaliacaoOcorrencia> Ocorrencias);

/// <summary>
/// Uma ocorrência avaliada: a identidade, a avaliação de cada subitem, o estado de cada fato de membro
/// dela e o estado da ocorrência — resolvida quando nenhum subitem ficou pendente.
/// </summary>
public sealed record AvaliacaoOcorrencia(
    string Id,
    IReadOnlyList<AvaliacaoItem> Itens,
    IReadOnlyDictionary<string, FatoResolvido> Fatos,
    EstadoFato Estado);

/// <summary>
/// A avaliação de um item: se aparece, se é obrigatório e quais restrições a resposta viola. Uma
/// resposta que viola restrição não vale — o fato do item é resolvido como se não houvesse resposta.
/// </summary>
public sealed record AvaliacaoItem(
    string FatoCodigo,
    string EtapaCodigo,
    Ternario Visivel,
    Ternario Obrigatorio,
    IReadOnlyList<RestricaoValor> RestricoesVioladas);

/// <summary>A avaliação de um termo: se aparece e se é obrigatório.</summary>
public sealed record AvaliacaoTermo(string Codigo, Ternario Visivel, Ternario Obrigatorio);
