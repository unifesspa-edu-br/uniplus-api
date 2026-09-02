namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Acl;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Discentes.Domain.Entities;

/// <summary>
/// O que restou de uma página depois de traduzida para o domínio: os vínculos que entram
/// na réplica e os que ficaram de fora, com o motivo.
/// </summary>
/// <param name="Aceitos">Vínculos prontos para a réplica, cada um com seu resumo.</param>
/// <param name="Descartados">
/// Vínculos que não entram. Ficam contados para que a diferença entre o que a origem tem
/// e o que a réplica guarda nunca seja silenciosa.
/// </param>
public sealed record ResultadoDaDecodificacao(
    IReadOnlyList<VinculoDecodificado> Aceitos,
    IReadOnlyList<VinculoDescartado> Descartados)
{
    /// <summary>
    /// Quantos vínculos foram deixados de fora porque a origem entregou algo fora do
    /// contrato.
    /// </summary>
    /// <remarks>
    /// Merece atenção à parte do descarte por registro incompleto. Registro incompleto é
    /// esperado e tem volume conhecido; quebra de contrato não deveria acontecer, e um
    /// número que cresce aqui indica que a origem mudou o que entrega. No limite — a
    /// origem parando de enviar um campo obrigatório —, todos os vínculos passariam a ser
    /// descartados e a execução terminaria sem escrever nada, com a réplica congelada. É
    /// esse silêncio que esta contagem existe para quebrar.
    /// </remarks>
    public int QuantidadeForaDoContrato =>
        Descartados.Count(d => d.Motivo == MotivoDeDescarte.ForaDoContrato);
}

/// <summary>
/// Um vínculo traduzido, com o resumo do seu conteúdo.
/// </summary>
/// <param name="Vinculo">O vínculo como o domínio o entende.</param>
/// <param name="ResumoDoConteudo">
/// Resumo dos campos trazidos da origem. Serve para a sincronização perceber que nada
/// mudou e pular a escrita — dois vínculos com o mesmo resumo são o mesmo dado.
/// </param>
public sealed record VinculoDecodificado(VinculoDiscente Vinculo, string ResumoDoConteudo);

/// <summary>
/// Um vínculo que não entrou na réplica, e por quê.
/// </summary>
/// <param name="IdDiscenteSigaa">
/// Identificador do vínculo na origem, quando veio. É nulo quando nem ele foi entregue —
/// caso em que não há como apontar de qual vínculo se trata.
/// </param>
/// <param name="Motivo">A natureza do problema.</param>
/// <param name="Detalhe">
/// O campo envolvido, quando identificável. Serve para o diagnóstico saber onde olhar sem
/// precisar reproduzir a chamada.
/// </param>
public sealed record VinculoDescartado(
    long? IdDiscenteSigaa,
    MotivoDeDescarte Motivo,
    string? Detalhe = null);

/// <summary>
/// Por que um vínculo não entrou na réplica.
/// </summary>
public enum MotivoDeDescarte
{
    /// <summary>
    /// O curso do vínculo não tem unidade acadêmica registrada na origem. O contrato
    /// permite; o modelo da réplica exige. Esperado, e com volume conhecido.
    /// </summary>
    CursoSemUnidadeAcademica = 1,

    /// <summary>
    /// O vínculo não tem ano ou período de ingresso registrado na origem. Mesma natureza
    /// do anterior.
    /// </summary>
    SemPeriodoDeIngresso = 2,

    /// <summary>
    /// A origem entregou o vínculo fora do que o contrato promete — sem um campo declarado
    /// obrigatório, ou com valor que não corresponde ao formato acordado.
    /// </summary>
    /// <remarks>
    /// Diferente dos outros dois: aqui não é o vínculo que não serve, é a entrega que saiu
    /// do combinado. O vínculo é deixado de fora para que um registro estragado não
    /// interrompa a sincronização dos demais, mas a ocorrência não é rotina e precisa ser
    /// olhada — ver <see cref="ResultadoDaDecodificacao.QuantidadeForaDoContrato"/>.
    /// </remarks>
    ForaDoContrato = 3,
}
