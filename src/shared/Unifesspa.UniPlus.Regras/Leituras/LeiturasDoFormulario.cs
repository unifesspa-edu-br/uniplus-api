namespace Unifesspa.UniPlus.Regras.Leituras;

using System.Text.Json;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Uma condição de um predicado do formulário na forma de leitura. Tem a forma flat de
/// <see cref="CondicaoPrecondicaoInput"/>, que a escrita aceita: fecha a volta da leitura à escrita sem
/// transformação do cliente. O <see cref="Valor"/> é o valor JSON já tipado (texto, booleano, número ou
/// lista), não um texto.
/// </summary>
public sealed record CondicaoPrecondicaoDto(string Fato, [property: AceitaCodigosDe<Operador>] string Operador, JsonElement Valor);

/// <summary>
/// A obrigatoriedade na leitura: <c>SEMPRE</c>, <c>NUNCA</c> ou <c>QUANDO</c>, com o predicado só em
/// <c>QUANDO</c>, na mesma forma flat da condição.
/// </summary>
public sealed record ObrigatoriedadeDto([property: AceitaCodigosDe<TipoObrigatoriedade>] string Tipo, IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Predicado);

/// <summary>
/// Como o campo se apresenta ao candidato: o rótulo, o tipo de campo, a ajuda e o pedido de confirmação da
/// resposta (UNI-REQ-0145). Tem a forma de <see cref="ApresentacaoDoCampoInput"/>, que a escrita aceita.
/// </summary>
public sealed record ApresentacaoDoCampoDto(
    string Rotulo,
    [property: AceitaCodigosDe<TipoDeCampo>] string TipoDeCampo,
    string? Ajuda,
    bool PedirConfirmacao);

/// <summary>Um limite da faixa de data, pela referência: <c>DATA_FIXA</c> com a data, ou <c>ENVIO</c> — o dia do envio da resposta — deslocado em anos e depois em dias, nulos no limite fixo.</summary>
public sealed record LimiteDeDataDto(string Ancora, DateOnly? Data, int? Anos, int? Dias);

/// <summary>Um grupo de opções permitidas e a condição em que ele vale; nula quando vale sempre.</summary>
public sealed record OpcoesCondicionadasDto(
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Quando,
    IReadOnlyList<string> Valores);

/// <summary>
/// Uma restrição sobre o valor respondido no item, pelo <see cref="Tipo"/>: <c>FAIXA_NUMERICA</c> e
/// <c>TAMANHO_TEXTO</c> com os limites, <c>OPCOES_PERMITIDAS</c> com os grupos de opções,
/// <c>OPCOES_DAS_RESPOSTAS</c> com os fatos cujas respostas formam as opções, <c>MUNICIPIOS_DA_UF</c> com o
/// fato da UF e <c>FAIXA_DE_DATA</c> com a data mínima e a máxima.
/// </summary>
public sealed record RestricaoValorDto(
    string Tipo,
    decimal? Minimo,
    decimal? Maximo,
    IReadOnlyList<OpcoesCondicionadasDto>? Entradas,
    IReadOnlyList<string>? Fatos,
    LimiteDeDataDto? DataMinima,
    LimiteDeDataDto? DataMaxima);

/// <summary>A resposta do campo que impede a inscrição e a mensagem que explica o motivo ao candidato.</summary>
public sealed record ImpedimentoDto(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>> Quando, string Mensagem);

/// <summary>
/// As regras do campo sobre as respostas anteriores. A <see cref="Exibicao"/> é o predicado na forma normal
/// disjuntiva — o OU de cláusulas, cada cláusula o E de condições —, ou <see langword="null"/> quando o campo
/// aparece sempre (nunca uma lista vazia). <see cref="Impedimento"/> é a resposta que impede a inscrição, com a
/// mensagem ao candidato, ou <see langword="null"/> quando nenhuma impede. Tem a forma de
/// <see cref="RegrasDoCampoInput"/>.
/// </summary>
public sealed record RegrasDoCampoDto(
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Exibicao,
    ObrigatoriedadeDto Obrigatoriedade,
    IReadOnlyList<RestricaoValorDto> Restricoes,
    ImpedimentoDto? Impedimento);

/// <summary>
/// Quando um grupo repetível ou um termo aparece — a exibição, nula quando sempre aparece — e quando é
/// obrigatório. Tem a forma de <see cref="ExibicaoEObrigatoriedadeInput"/>, que a escrita aceita.
/// </summary>
public sealed record ExibicaoEObrigatoriedadeDto(
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Exibicao,
    ObrigatoriedadeDto Obrigatoriedade);

/// <summary>
/// Quantas ocorrências o grupo repetível admite: o mínimo, o máximo — nulo quando não há — e se o próprio
/// candidato é uma delas. Tem a forma de <see cref="OcorrenciasDoGrupoInput"/>, que a escrita aceita.
/// </summary>
public sealed record OcorrenciasDoGrupoDto(int Minimo, int? Maximo, bool IncluiCandidato);

/// <summary>
/// Como a página se apresenta ao candidato: o título, a descrição e o aviso. Tem a forma de
/// <see cref="ApresentacaoDaPaginaInput"/>, que a escrita aceita.
/// </summary>
public sealed record ApresentacaoDaPaginaDto(string Titulo, string? Descricao, string? Aviso);

/// <summary>
/// Uma página do formulário: seção ou bloco de sistema, em token canônico, com a apresentação e a exibição
/// condicional da seção — nula quando ela sempre aparece. Tem a forma de <see cref="PaginaDoFormularioInput"/>,
/// que a escrita aceita.
/// </summary>
public sealed record PaginaDoFormularioDto(
    string Codigo,
    int Ordem,
    [property: AceitaCodigosDe<TipoPaginaFormulario>] string Tipo,
    [property: AceitaCodigosDe<BlocoSistema>] string? Bloco,
    ApresentacaoDaPaginaDto Apresentacao,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? Exibicao);

/// <summary>O conteúdo congelado da versão do termo: o nome, o texto, a base legal, a forma de aceite e o hash.</summary>
public sealed record ConteudoDoTermoDto(string Nome, string Texto, string BaseLegal, CodigoComRotulo<FormaAceite> FormaAceite, string HashVersao);
