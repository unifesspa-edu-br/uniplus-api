namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que o candidato pode fazer agora com a inscrição. Corrigir e cancelar valem só para a enviada e
/// com as inscrições abertas; descartar vale só para o rascunho, em qualquer época, porque guardar dado
/// pessoal sem finalidade é pior do que apagá-lo.
/// </summary>
public sealed record AcoesDaInscricao(bool PodeCorrigir, bool PodeCancelar, bool PodeDescartar);
