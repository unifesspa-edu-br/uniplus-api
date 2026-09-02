namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Acl;

using System.Collections.Generic;

using Unifesspa.UniPlus.Discentes.Domain.Entities;

/// <summary>
/// O que restou de uma página depois de traduzida para o domínio: os vínculos que entram
/// na réplica e os que foram deixados de fora, com o motivo.
/// </summary>
/// <param name="Aceitos">Vínculos prontos para a réplica, cada um com seu resumo.</param>
/// <param name="Descartados">
/// Vínculos que a origem entregou dentro do contrato, mas sem o que a réplica exige.
/// Não são erro: são registros que não servem, e ficam contados para que a diferença
/// entre o que a origem tem e o que a réplica guarda nunca seja silenciosa.
/// </param>
public sealed record ResultadoDaDecodificacao(
    IReadOnlyList<VinculoDecodificado> Aceitos,
    IReadOnlyList<VinculoDescartado> Descartados);

/// <summary>
/// Um vínculo traduzido, com o resumo do seu conteúdo.
/// </summary>
/// <param name="Vinculo">O vínculo como o domínio o entende.</param>
/// <param name="ResumoDoConteudo">
/// Resumo de todos os campos trazidos da origem. Serve para a sincronização perceber que
/// nada mudou e pular a escrita — dois vínculos com o mesmo resumo são o mesmo dado.
/// </param>
public sealed record VinculoDecodificado(VinculoDiscente Vinculo, string ResumoDoConteudo);

/// <summary>
/// Um vínculo que não entrou na réplica, e por quê.
/// </summary>
/// <param name="IdDiscenteSigaa">Identificador do vínculo na origem.</param>
/// <param name="Motivo">O que faltava.</param>
public sealed record VinculoDescartado(long IdDiscenteSigaa, MotivoDeDescarte Motivo);

/// <summary>
/// Por que um vínculo entregue dentro do contrato não serve à réplica.
/// </summary>
/// <remarks>
/// Todos os motivos são campos que o contrato da origem declara opcionais e que o modelo
/// da réplica exige. Enquanto essa diferença existir, esses vínculos ficam de fora — por
/// decisão registrada, não por falha.
/// </remarks>
public enum MotivoDeDescarte
{
    /// <summary>O curso do vínculo não tem unidade acadêmica registrada na origem.</summary>
    CursoSemUnidadeAcademica = 1,

    /// <summary>O vínculo não tem ano ou período de ingresso registrado na origem.</summary>
    SemPeriodoDeIngresso = 2,
}
