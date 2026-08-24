namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Um campo do vocabulário fechado da divulgação pública (UNI-REQ-0050), com o que decide
/// se ele pode entrar numa configuração: o código de wire, o rótulo humano, se é o piso que
/// nenhuma configuração remove, e se publicá-lo exige justificativa.
/// </summary>
/// <param name="Codigo">Código de wire — o mesmo valor que trafega em <c>camposPublicos</c> e no envelope.</param>
/// <param name="Nome">Rótulo humano do campo, para quem monta a configuração.</param>
/// <param name="Obrigatorio">Piso da divulgação: está sempre presente e não pode ser removido da lista.</param>
/// <param name="ExigeJustificativa">Publicar este campo obriga a declarar justificativa.</param>
public sealed record CampoDivulgacaoPublica(
    string Codigo,
    string Nome,
    bool Obrigatorio,
    bool ExigeJustificativa);
