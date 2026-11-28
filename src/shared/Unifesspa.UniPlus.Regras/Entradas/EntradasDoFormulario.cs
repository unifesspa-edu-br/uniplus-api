namespace Unifesspa.UniPlus.Regras.Entradas;

using System.ComponentModel;
using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Uma condição de um predicado do formulário — a exibição, a obrigatoriedade ou o impedimento de um item —, a tripla
/// <c>{ Fato, Operador, Valor }</c> na forma flat do wire de comando. Idêntica em forma à
/// condição de gatilho documental: o <see cref="Operador"/> é o código canônico UPPER_SNAKE
/// (<c>IGUAL</c>, <c>ALGUM_DE</c>, …) e o <see cref="Valor"/> é o valor JSON já tipado (string,
/// booleano, número ou array para <c>ALGUM_DE</c>/<c>NENHUM_DE</c>).
/// </summary>
public sealed record CondicaoPrecondicaoInput(string Fato, [AceitaCodigosDe<Operador>] string? Operador, JsonElement Valor);

/// <summary>
/// Uma restrição sobre o valor respondido no item, pelo <see cref="Tipo"/>: <c>FAIXA_NUMERICA</c>
/// e <c>TAMANHO_TEXTO</c> com <see cref="Minimo"/> e <see cref="Maximo"/> (ao menos um; inteiros no
/// tamanho), <c>OPCOES_PERMITIDAS</c> com as <see cref="Entradas"/>, <c>OPCOES_DAS_RESPOSTAS</c> com
/// os <see cref="Fatos"/> cujas respostas formam as opções, <c>MUNICIPIOS_DA_UF</c> com o fato da
/// UF em <see cref="Fatos"/> e <c>FAIXA_DE_DATA</c> com <see cref="DataMinima"/> e
/// <see cref="DataMaxima"/> (ao menos um).
/// </summary>
public sealed record RestricaoValorInput(
    [property: AceitaCodigosDe<TipoRestricaoValor>] string Tipo,
    decimal? Minimo = null,
    decimal? Maximo = null,
    IReadOnlyList<OpcoesCondicionadasInput>? Entradas = null,
    IReadOnlyList<string>? Fatos = null,
    LimiteDeDataInput? DataMinima = null,
    LimiteDeDataInput? DataMaxima = null);

/// <summary>
/// Um limite da faixa de data, pela <see cref="Ancora"/>: <c>DATA_FIXA</c> com a <see cref="Data"/>,
/// ou <c>ENVIO</c> — o dia do envio da resposta — deslocado em <see cref="Anos"/> e depois em
/// <see cref="Dias"/>, que podem ser negativos; o deslocamento que falta é zero.
/// </summary>
public sealed record LimiteDeDataInput(string Ancora, DateOnly? Data = null, int? Anos = null, int? Dias = null);

/// <summary>
/// O envio que a simulação supõe: o instante e a zona, no identificador IANA, em que se conta o dia
/// dele — o mesmo que o formulário apresentado publica em <c>fusoDoEnvio</c>. O instante ausente ou nulo chega ao
/// domínio, que o recusa (422) com as demais recusas do envio.
/// </summary>
public sealed record EnvioSimuladoInput(
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    DateTimeOffset? Instante,
    string Fuso);

/// <summary>
/// Um grupo de opções permitidas e a condição, sobre respostas anteriores, em que ele vale; sem
/// condição, vale sempre.
/// </summary>
public sealed record OpcoesCondicionadasInput(
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Quando,
    IReadOnlyList<string> Valores);

/// <summary>
/// A obrigatoriedade na entrada: o <see cref="Tipo"/> — <c>SEMPRE</c> ou <c>NUNCA</c>, sem
/// <see cref="Predicado"/>, e <c>QUANDO</c>, com ele. O predicado está na forma normal disjuntiva,
/// como o da exibição.
/// </summary>
public sealed record ObrigatoriedadeInput([AceitaCodigosDe<TipoObrigatoriedade>] string? Tipo, IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Predicado = null);

/// <summary>
/// Como o campo se apresenta ao candidato: o rótulo, o tipo de campo, a ajuda e o pedido de
/// confirmação da resposta. <see cref="TipoDeCampo"/> é o código de
/// <see cref="Enums.TipoDeCampo"/>; a leitura dele, que recusa o código ausente e o desconhecido,
/// é de <see cref="TipoDeCampoDaEntrada"/>.
/// </summary>
public sealed record ApresentacaoDoCampoInput(
    string? Rotulo,
    [AceitaCodigosDe<TipoDeCampo>] string? TipoDeCampo,
    string? Ajuda = null,
    bool PedirConfirmacao = false);

/// <summary>
/// As regras do campo sobre as respostas anteriores: a <see cref="Exibicao"/> condicional, a
/// obrigatoriedade, as restrições de valor e o impedimento. A exibição e o predicado da obrigatoriedade
/// estão na forma normal disjuntiva — a lista externa é o <b>OU</b> de cláusulas, e cada cláusula
/// interna é o <b>E</b> de condições. A ausência de condição é <see langword="null"/>, nunca uma lista
/// vazia: o campo sem condição aparece sempre, e um predicado sem cláusula avaliaria falso, o oposto.
/// </summary>
public sealed record RegrasDoCampoInput(
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Exibicao,
    ObrigatoriedadeInput? Obrigatoriedade,
    IReadOnlyList<RestricaoValorInput>? Restricoes = null,
    ImpedimentoInput? Impedimento = null)
{
    /// <summary>
    /// Os predicados que as regras trazem — exibição, predicado da obrigatoriedade, condição do impedimento
    /// e condição de cada grupo de opções —, os ausentes como <see langword="null"/>.
    /// </summary>
    public IEnumerable<IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>?> Predicados() =>
    [
        Exibicao,
        Obrigatoriedade?.Predicado,
        Impedimento?.Quando,
        .. (Restricoes ?? []).SelectMany(static r => (r?.Entradas ?? []).Select(static e => e?.Quando)),
    ];
}

/// <summary>
/// Um item do formulário, no processo e no modelo: o fato que o campo coleta, a posição na ordem de
/// coleta, a apresentação e as regras do campo e a seção em que ele aparece. A apresentação ou as
/// regras ausentes chegam ao domínio, que recusa o rótulo, o tipo de campo e a obrigatoriedade que
/// faltam, cada um no caminho da parte a que pertence.
/// </summary>
public sealed record FatoColetadoInput(
    string FatoCodigo,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    int? Ordem,
    ApresentacaoDoCampoInput? Apresentacao,
    RegrasDoCampoInput? Regras,
    string? PaginaCodigo = null)
{
    /// <summary>O caminho da apresentação do campo no item, onde a recusa de rótulo, tipo e ajuda se aponta.</summary>
    public const string CaminhoDaApresentacao = "apresentacao";

    /// <summary>O caminho das regras do campo no item, onde a recusa de exibição, obrigatoriedade, restrição e impedimento se aponta.</summary>
    public const string CaminhoDasRegras = "regras";
}

/// <summary>
/// A resposta do campo que impede a inscrição: a condição, cujas cláusulas citam a resposta do
/// próprio campo, e a mensagem ao candidato.
/// </summary>
public sealed record ImpedimentoInput(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Quando, string? Mensagem);

/// <summary>
/// Quantas ocorrências o grupo repetível admite — o mínimo e, quando declarado, o máximo — e se o próprio
/// candidato é uma delas, como na composição familiar. O mínimo e a inclusão do candidato são obrigatórios e,
/// quando ausentes, chegam ao domínio, que os recusa; o máximo ausente é o grupo sem limite. O mínimo negativo e
/// o máximo abaixo do mínimo também são recusados pelo domínio.
/// </summary>
public sealed record OcorrenciasDoGrupoInput(int? Minimo, bool? IncluiCandidato, int? Maximo = null);

/// <summary>
/// Quando um grupo repetível ou um termo aparece e quando é obrigatório: a <see cref="Exibicao"/> condicional,
/// nula quando aparece sempre, e a obrigatoriedade. Diferente das regras do campo, não tem restrição de valor
/// nem impedimento.
/// </summary>
public sealed record ExibicaoEObrigatoriedadeInput(
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Exibicao,
    ObrigatoriedadeInput? Obrigatoriedade);

/// <summary>
/// Um grupo repetível do formulário (UNI-REQ-0146): o código, a posição na ordem dos itens, o rótulo, as
/// <see cref="Ocorrencias"/> admitidas, a exibição e a obrigatoriedade do grupo em <see cref="Regras"/> — na forma
/// das do item —, os campos de cada ocorrência, cada um na forma do item, com ordem própria dentro do grupo e sem
/// seção, e a seção do grupo. Os campos (<see cref="Subitens"/>) ausentes são recusados pelo domínio como obrigatórios, sem conferir o que depende deles. As ocorrências ausentes são recusadas pelo domínio, como o mínimo e a inclusão
/// do candidato que faltarem; as regras ausentes chegam ao domínio, que recusa a obrigatoriedade que falta.
/// </summary>
public sealed record GrupoColetadoInput(
    string Codigo,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    int? Ordem,
    string Rotulo,
    OcorrenciasDoGrupoInput? Ocorrencias,
    ExibicaoEObrigatoriedadeInput? Regras,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    IReadOnlyList<FatoColetadoInput>? Subitens,
    string? PaginaCodigo = null)
{
    /// <summary>O caminho das ocorrências no grupo, onde a recusa de mínimo e máximo se aponta.</summary>
    public const string CaminhoDasOcorrencias = "ocorrencias";

    /// <summary>O caminho das regras no grupo, onde a recusa de exibição e obrigatoriedade se aponta.</summary>
    public const string CaminhoDasRegras = "regras";
}

/// <summary>
/// Como a página se apresenta ao candidato: o título, a descrição e o aviso. O título é obrigatório; o domínio
/// recusa o que falta.
/// </summary>
public sealed record ApresentacaoDaPaginaInput(string? Titulo, string? Descricao, string? Aviso);

/// <summary>
/// Uma página do formulário: seção (<c>SECAO</c>) ou bloco de sistema (<c>BLOCO</c>, com o bloco em
/// <see cref="Bloco"/>), com a ordem, a <see cref="Apresentacao"/> e, na seção, a
/// <see cref="Exibicao"/> condicional — predicado sobre fatos conhecidos antes dela. A apresentação ausente chega
/// ao domínio, que recusa o título que falta no caminho dela.
/// </summary>
public sealed record PaginaDoFormularioInput(
    string Codigo,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    int? Ordem,
    [AceitaCodigosDe<TipoPaginaFormulario>] string Tipo,
    [AceitaCodigosDe<BlocoSistema>] string? Bloco,
    ApresentacaoDaPaginaInput? Apresentacao,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Exibicao = null)
{
    /// <summary>O caminho da apresentação na página, onde a recusa de título, descrição e aviso se aponta.</summary>
    public const string CaminhoDaApresentacao = "apresentacao";
}

/// <summary>
/// A versão do catálogo de termos que o formulário exige: o termo e a versão dele. A leitura devolve também o
/// conteúdo congelado; a escrita lê só a identidade.
/// </summary>
public sealed record VersaoDoTermoInput(Guid TermoId, Guid VersaoId);

/// <summary>
/// Um termo que o formulário exige: o identificador da exigência, a ordem, o <see cref="Termo"/> e a versão do
/// catálogo, a exibição (nula quando o termo sempre aparece) e a obrigatoriedade
/// (<c>SEMPRE</c>, <c>NUNCA</c> ou <c>QUANDO</c>, esta com predicado) em <see cref="Regras"/>. Os predicados têm a forma
/// da exibição do campo: OU de cláusulas, cada uma E de condições. O termo ausente não é encontrado no catálogo, e as
/// regras ausentes chegam ao domínio, que recusa a obrigatoriedade que falta.
/// </summary>
public sealed record TermoExigidoInput(
    string Codigo,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    int? Ordem,
    VersaoDoTermoInput? Termo,
    ExibicaoEObrigatoriedadeInput? Regras)
{
    /// <summary>O caminho da versão do termo na exigência, onde a recusa de termo e versão se aponta.</summary>
    public const string CaminhoDoTermo = "termo";

    /// <summary>O caminho das regras na exigência, onde a recusa de exibição e obrigatoriedade se aponta.</summary>
    public const string CaminhoDasRegras = "regras";
}
