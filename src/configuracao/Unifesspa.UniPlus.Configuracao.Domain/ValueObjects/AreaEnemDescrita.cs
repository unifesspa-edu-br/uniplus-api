namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Uma das áreas de conhecimento do ENEM definidas pelo cadastro de Pesos por Área
/// (Anexo I da Resolução nº 805/2024/Consepe), com o rótulo oficial que o cadastro grava
/// ao lado do código. Não há vocabulário de áreas fora deste cadastro: a lista vive em
/// <see cref="Entities.PesoAreaEnem.Areas"/>.
/// </summary>
/// <param name="Codigo">Código da área: sem abreviação e sem acento, identidade estável.</param>
/// <param name="Rotulo">Rótulo oficial da área, fixo por código e posto pelo sistema.</param>
public sealed record AreaEnemDescrita(
    string Codigo,
    string Rotulo);
