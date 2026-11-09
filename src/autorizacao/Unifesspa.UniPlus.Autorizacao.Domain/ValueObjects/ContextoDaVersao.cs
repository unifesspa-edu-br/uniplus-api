namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// O que o agregado não vê sozinho e quem opera sob a trava do perfil lê do banco: se outro perfil usa o nome da versão
/// e se o perfil tem janela de migração efetivamente aberta, que não aceita outra versão pendente.
/// </summary>
public sealed record ContextoDaVersao(bool NomeEmUsoPorOutroPerfil, bool JanelaDeMigracaoAberta);
