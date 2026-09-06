namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Uma fase canônica do conjunto fechado (UNI-REQ-0139), com o rótulo que um cliente usa
/// para montar o `select` de criação sem manter cópia própria do vocabulário.
/// </summary>
/// <param name="Codigo">Código canônico — o mesmo valor aceito por <c>FaseCanonica.Criar</c>.</param>
/// <param name="Nome">
/// Rótulo canônico do código, fixo — distinto do <c>Nome</c> editável do cadastro. Coincide
/// com o <c>Nome</c> do seed institucional (<c>FaseCanonicaSeed</c>) no valor inicial, mas as
/// duas fontes têm ciclo de vida diferente: este é o rótulo do vocabulário, aquele é dado
/// persistido que o CRUD admin pode editar depois.
/// </param>
public sealed record FaseCanonicaDescrito(
    string Codigo,
    string Nome);
