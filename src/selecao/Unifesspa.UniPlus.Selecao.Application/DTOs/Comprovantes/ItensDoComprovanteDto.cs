namespace Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Uma seção do formulário com as respostas dela e os grupos que se repetem nela.</summary>
/// <param name="Codigo">O código da seção; vazio para o item que nenhuma seção abriga.</param>
/// <param name="Titulo">O título da seção na versão da inscrição; ausente quando a versão não a nomeia.</param>
/// <param name="Itens">As respostas da seção fora de grupo.</param>
/// <param name="Grupos">Os grupos repetíveis da seção.</param>
public sealed record SecaoDoComprovanteDto(
    string Codigo,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Titulo,
    IReadOnlyList<ItemDoComprovanteDto> Itens,
    IReadOnlyList<GrupoDoComprovanteDto> Grupos);

/// <summary>Um grupo repetível com as ocorrências que o envio respondeu.</summary>
public sealed record GrupoDoComprovanteDto(string Codigo, string Rotulo, IReadOnlyList<OcorrenciaDoComprovanteDto> Ocorrencias);

/// <summary>Uma ocorrência de grupo, titulada como o formulário a mostra: <c>Você (candidato)</c> ou <c>Integrante n</c>.</summary>
public sealed record OcorrenciaDoComprovanteDto(string Id, string Titulo, IReadOnlyList<ItemDoComprovanteDto> Itens);

/// <summary>
/// Uma resposta no comprovante. O <c>tipo</c> diz como ler o <c>valor</c> (<c>texto</c>, <c>data</c>, <c>numero</c>, <c>booleano</c>,
/// <c>opcao</c>, <c>opcoes</c> ou <c>endereco</c>) e o <c>componente</c>, como exibi-lo; o rótulo e a descrição são os da versão da inscrição.
/// </summary>
public sealed record ItemDoComprovanteDto(
    string Rotulo,
    string Descricao,
    string Tipo,
    string Componente,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [property: Description(
        "O valor respondido; a forma depende de `tipo`. `texto`, `data` (AAAA-MM-DD), `data-hora` (ISO 8601 com deslocamento), `hora` (HH:mm), " +
        "`numero` (em texto, como foi respondido) e `booleano` (verdadeiro ou falso) trazem um valor simples. `opcao` traz `{ codigo, rotulo }`. `opcoes` traz uma lista de " +
        "`{ codigo, rotulo }`. `endereco` traz `{ cep, logradouro, numero, complemento, bairro, municipio, municipioCodigoIbge, uf, ufNome }`, " +
        "todos em texto. Ausente quando o item não foi respondido.")]
    JsonElement? Valor);
