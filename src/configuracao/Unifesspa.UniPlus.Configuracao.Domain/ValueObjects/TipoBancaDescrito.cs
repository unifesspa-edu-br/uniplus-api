namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Um tipo de banca do conjunto canônico (UNI-REQ-0139), com o rótulo que um cliente usa
/// para montar o `select` de criação sem manter cópia própria do vocabulário.
/// </summary>
/// <param name="Codigo">Código canônico — o mesmo valor aceito por <c>TipoBanca.Criar</c>.</param>
/// <param name="Nome">Rótulo canônico do código, fixo — distinto do <c>Nome</c> editável do cadastro.</param>
public sealed record TipoBancaDescrito(
    string Codigo,
    string Nome);
