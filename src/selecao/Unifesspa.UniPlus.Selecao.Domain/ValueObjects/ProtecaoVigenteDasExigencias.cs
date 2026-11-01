namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// A proteção das exigências no momento de avaliar ou congelar: a classificação congelada dos fatos
/// derivados (com que se confere o fato comprovado que nenhum formulário coleta) e a proteção efetiva
/// de cada exigência, por id. É parâmetro das
/// pendências, e não estado do agregado: o painel de conformidade, a publicação e a retificação
/// avaliam o mesmo contexto.
/// </summary>
/// <param name="ClassificacaoDosFatosDerivados">
/// A classificação de cada fato derivado, por código: a já congelada na versão vigente, ou a do catálogo
/// para o derivado que a versão ainda não congelou (ADR-0154).
/// </param>
/// <param name="PorExigencia">
/// A proteção efetiva da exigência: a já congelada na versão vigente, se a exigência é a mesma (mesmo
/// id e mesmo tipo); senão, a vigente no cadastro. A exigência ausente vale pela cópia que tem.
/// </param>
/// <param name="DerivadosSemProtecaoAprovada">
/// Os códigos dos fatos derivados do catálogo cuja proteção a proteção de dados ainda não aprovou: continuam
/// sendo fatos derivados do catálogo, mas sem classificação a congelar. O que o processo usa deles é recusado
/// pela conferência da aprovação, e não por esta avaliação.
/// </param>
public sealed record ProtecaoVigenteDasExigencias(
    IReadOnlyDictionary<string, ClassificacaoDeProtecao> ClassificacaoDosFatosDerivados,
    IReadOnlyDictionary<Guid, ProtecaoDoDocumento> PorExigencia,
    IReadOnlySet<string> DerivadosSemProtecaoAprovada);
