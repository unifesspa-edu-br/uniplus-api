namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que o candidato pode fazer agora com a inscrição. Com as inscrições encerradas, nenhuma ação vale:
/// corrigir e cancelar pedem a enviada; continuar e descartar pedem o rascunho.
/// </summary>
public sealed record AcoesDaInscricao(bool PodeContinuar, bool PodeCorrigir, bool PodeCancelar, bool PodeDescartar);
