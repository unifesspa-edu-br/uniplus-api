namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A avaliação de um formulário na forma que atravessa o fio (ADR-0139): o que a avaliação sem
/// cadastro devolve, o resultado esperado de cada caso do corpus compartilhado e o que o interpretador
/// do front produz para conferir com o servidor. Os estados vão pelo código canônico —
/// <c>VERDADEIRO</c>, <c>FALSO</c> e <c>INDETERMINADO</c> para o que aparece, é obrigatório ou impede;
/// <c>RESOLVIDO</c>, <c>INDETERMINADO</c>, <c>NAO_APLICAVEL</c> e <c>NAO_INFORMADO</c> para o fato.
/// </summary>
public sealed record AvaliacaoPortavel(
    IReadOnlyList<EtapaAvaliada> Etapas,
    IReadOnlyList<CampoAvaliado> Campos,
    IReadOnlyList<GrupoAvaliado> Grupos,
    IReadOnlyList<TermoAvaliado> Termos)
{
    public static AvaliacaoPortavel De(AvaliacaoFormulario avaliacao)
    {
        ArgumentNullException.ThrowIfNull(avaliacao);
        return new(
            [.. avaliacao.Etapas.Select(static e => new EtapaAvaliada(e.Codigo, e.Visivel.ToCodigo()))],
            [.. avaliacao.Itens.Select(i => Campo(i, avaliacao.Fatos))],
            [.. avaliacao.Grupos.Select(static g => new GrupoAvaliado(
                g.Codigo, g.EtapaCodigo, g.Visivel.ToCodigo(), g.Obrigatorio.ToCodigo(), g.Estado.ToCodigo(), g.ContagemValida, g.OcorrenciaDoCandidatoValida,
                [.. g.Ocorrencias.Select(static o => new OcorrenciaAvaliada(o.Id, o.Estado.ToCodigo(), [.. o.Itens.Select(i => Campo(i, o.Fatos))]))]))],
            [.. avaliacao.Termos.Select(static t => new TermoAvaliado(t.Codigo, t.Visivel.ToCodigo(), t.Obrigatorio.ToCodigo()))]);
    }

    private static CampoAvaliado Campo(AvaliacaoItem item, IReadOnlyDictionary<string, FatoResolvido> fatos) => new(
        item.FatoCodigo,
        item.EtapaCodigo,
        fatos.TryGetValue(item.FatoCodigo, out FatoResolvido? fato) ? fato.Estado.ToCodigo() : EstadoFatoCodigo.Indeterminado,
        item.Visivel.ToCodigo(),
        item.Obrigatorio.ToCodigo(),
        [.. item.RestricoesVioladas.Select(static r => RestricaoValorJson.ParaToken(r.Tipo))],
        item.Impedido.ToCodigo(),
        item.Opcoes);
}

/// <summary>
/// Uma etapa das regras avaliada, pelo código nas regras: se ela aparece. As etapas são as seções do
/// formulário e, no rascunho com campo ainda sem seção, a etapa da finalidade; os blocos que o sistema
/// monta não têm regra e não entram.
/// </summary>
public sealed record EtapaAvaliada(string Codigo, string Visivel);

/// <summary>
/// Um campo avaliado: o estado do fato que ele produz, se aparece, se é obrigatório, as restrições que
/// a resposta viola, se a resposta impede a inscrição e as opções vigentes do campo de escolha.
/// </summary>
public sealed record CampoAvaliado(
    string FatoCodigo,
    string EtapaCodigo,
    string Estado,
    string Visivel,
    string Obrigatorio,
    IReadOnlyList<string> RestricoesVioladas,
    string Impedido,
    OpcoesVigentes? Opcoes);

/// <summary>
/// Um grupo repetível avaliado: se aparece e é obrigatório, o estado da lista, se a contagem e a
/// ocorrência do próprio candidato valem, e cada ocorrência.
/// </summary>
public sealed record GrupoAvaliado(
    string Codigo,
    string EtapaCodigo,
    string Visivel,
    string Obrigatorio,
    string Estado,
    bool ContagemValida,
    bool OcorrenciaDoCandidatoValida,
    IReadOnlyList<OcorrenciaAvaliada> Ocorrencias);

/// <summary>Uma ocorrência avaliada: o estado dela e os campos.</summary>
public sealed record OcorrenciaAvaliada(string Id, string Estado, IReadOnlyList<CampoAvaliado> Campos);

/// <summary>Um termo avaliado: se aparece e se é obrigatório.</summary>
public sealed record TermoAvaliado(string Codigo, string Visivel, string Obrigatorio);
