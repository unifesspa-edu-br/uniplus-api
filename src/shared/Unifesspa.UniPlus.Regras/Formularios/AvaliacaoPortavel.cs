namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
    IReadOnlyList<PaginaAvaliada> Paginas,
    IReadOnlyList<CampoAvaliado> Campos,
    IReadOnlyList<GrupoAvaliado> Grupos,
    IReadOnlyList<TermoAvaliado> Termos)
{
    public static AvaliacaoPortavel De(AvaliacaoFormulario avaliacao)
    {
        ArgumentNullException.ThrowIfNull(avaliacao);
        return new(
            [.. avaliacao.Paginas.Select(static e => new PaginaAvaliada(e.Codigo, e.Visivel.Codigo()))],
            [.. avaliacao.Itens.Select(i => Campo(i, avaliacao.Fatos))],
            [.. avaliacao.Grupos.Select(Grupo)],
            [.. avaliacao.Termos.Select(static t => new TermoAvaliado(t.Codigo, t.Visivel.Codigo(), t.Obrigatorio.Codigo()))]);
    }

    private static GrupoAvaliado Grupo(AvaliacaoGrupo grupo) => new(
        grupo.Codigo,
        grupo.PaginaCodigo,
        grupo.Estado.Codigo(),
        new ExibicaoEObrigatoriedadeAvaliadas(grupo.Visivel.Codigo(), grupo.Obrigatorio.Codigo()),
        new OcorrenciasAvaliadas(grupo.ContagemValida, grupo.OcorrenciaDoCandidatoValida, [.. grupo.Ocorrencias.Select(Ocorrencia)]));

    private static OcorrenciaAvaliada Ocorrencia(AvaliacaoOcorrencia ocorrencia) =>
        new(ocorrencia.Id, ocorrencia.Estado.Codigo(), [.. ocorrencia.Itens.Select(i => Campo(i, ocorrencia.Fatos))]);

    private static CampoAvaliado Campo(AvaliacaoItem item, IReadOnlyDictionary<string, FatoResolvido> fatos) => new(
        item.FatoCodigo,
        item.PaginaCodigo,
        fatos.TryGetValue(item.FatoCodigo, out FatoResolvido? fato) ? fato.Estado.Codigo() : EstadoFato.Indeterminado.Codigo(),
        new RegrasDoCampoAvaliadas(
            item.Visivel.Codigo(),
            item.Obrigatorio.Codigo(),
            [.. item.RestricoesVioladas.Select(static r => r.Tipo.Codigo())],
            item.Impedido.Codigo()),
        item.Opcoes);
}

/// <summary>
/// Uma página das regras avaliada, pelo código nas regras: se ela aparece. As páginas são as seções do
/// formulário e, no rascunho com campo ainda sem seção, a página da finalidade; os blocos que o sistema
/// monta não têm regra e não entram.
/// </summary>
public sealed record PaginaAvaliada(string Codigo, [property: AceitaCodigosDe<Ternario>] string Visivel);

/// <summary>
/// Um campo avaliado: o estado do fato que ele produz, o que as regras do campo resultam e as opções
/// vigentes do campo de escolha.
/// </summary>
public sealed record CampoAvaliado(
    string FatoCodigo,
    string PaginaCodigo,
    [property: AceitaCodigosDe<EstadoFato>] string Estado,
    RegrasDoCampoAvaliadas Regras,
    OpcoesVigentes? Opcoes);

/// <summary>
/// O que as regras de um campo resultam: se aparece, se é obrigatório, as restrições que a resposta
/// viola e se a resposta impede a inscrição.
/// </summary>
public sealed record RegrasDoCampoAvaliadas(
    [property: AceitaCodigosDe<Ternario>] string Visivel,
    [property: AceitaCodigosDe<Ternario>] string Obrigatorio,
    IReadOnlyList<string> RestricoesVioladas,
    [property: AceitaCodigosDe<Ternario>] string Impedido);

/// <summary>Um grupo repetível avaliado: o estado da lista, o que as regras resultam e as ocorrências.</summary>
public sealed record GrupoAvaliado(
    string Codigo,
    string PaginaCodigo,
    [property: AceitaCodigosDe<EstadoFato>] string Estado,
    ExibicaoEObrigatoriedadeAvaliadas Regras,
    OcorrenciasAvaliadas Ocorrencias);

/// <summary>O que as regras de um grupo resultam: se aparece e se é obrigatório.</summary>
public sealed record ExibicaoEObrigatoriedadeAvaliadas([property: AceitaCodigosDe<Ternario>] string Visivel, [property: AceitaCodigosDe<Ternario>] string Obrigatorio);

/// <summary>
/// As ocorrências de um grupo avaliadas: se a contagem e a ocorrência do próprio candidato valem, e cada
/// ocorrência.
/// </summary>
public sealed record OcorrenciasAvaliadas(
    bool ContagemValida,
    bool OcorrenciaDoCandidatoValida,
    IReadOnlyList<OcorrenciaAvaliada> Lista);

/// <summary>Uma ocorrência avaliada: o estado dela e os campos.</summary>
public sealed record OcorrenciaAvaliada(string Id, [property: AceitaCodigosDe<EstadoFato>] string Estado, IReadOnlyList<CampoAvaliado> Campos);

/// <summary>Um termo avaliado: se aparece e se é obrigatório.</summary>
public sealed record TermoAvaliado(string Codigo, [property: AceitaCodigosDe<Ternario>] string Visivel, [property: AceitaCodigosDe<Ternario>] string Obrigatorio);
