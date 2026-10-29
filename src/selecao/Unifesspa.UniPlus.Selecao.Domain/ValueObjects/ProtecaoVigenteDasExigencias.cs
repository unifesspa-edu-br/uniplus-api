namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// O que o cadastro diz, no momento de avaliar ou congelar, sobre a proteção das exigências: a
/// classificação dos fatos do catálogo (com que se confere o fato comprovado que nenhum formulário
/// coleta, como o derivado) e a proteção efetiva de cada exigência, por id. É parâmetro das
/// pendências, e não estado do agregado: o painel de conformidade, a publicação e a retificação
/// avaliam o mesmo contexto.
/// </summary>
/// <param name="ClassificacaoDosFatosDoCatalogo">A classificação de cada fato do catálogo, por código.</param>
/// <param name="PorExigencia">
/// A proteção efetiva da exigência: a já congelada na versão vigente, se a exigência é a mesma (mesmo
/// id e mesmo tipo); senão, a vigente no cadastro. A exigência ausente vale pela cópia que tem.
/// </param>
public sealed record ProtecaoVigenteDasExigencias(
    IReadOnlyDictionary<string, ClassificacaoDeProtecao> ClassificacaoDosFatosDoCatalogo,
    IReadOnlyDictionary<Guid, ProtecaoDoDocumento> PorExigencia);
