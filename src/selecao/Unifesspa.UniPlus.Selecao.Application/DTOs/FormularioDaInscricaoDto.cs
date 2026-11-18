namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.ComponentModel;
using System.Text.Json;

/// <summary>O formulário e as respostas da versão em que a inscrição se encontra.</summary>
public sealed record FormularioDaInscricaoDto(
    Guid VersaoFormularioId,
    [property: Description("As respostas por código do fato. O município vem como texto, o código IBGE, no rascunho, e como objeto { \"codigo\", \"rotulo\" } na inscrição enviada, com o nome que o Geo conferiu no envio.")]
    IReadOnlyDictionary<string, JsonElement> Respostas,
    [property: Description("O que o sistema calculou das respostas. O município de residência vem na mesma forma do município das respostas: { \"codigo\", \"rotulo\" } na inscrição enviada.")]
    IReadOnlyDictionary<string, JsonElement> Derivados,
    IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>> Grupos,
    IReadOnlyList<AceiteDeTermoInput> Aceites);
