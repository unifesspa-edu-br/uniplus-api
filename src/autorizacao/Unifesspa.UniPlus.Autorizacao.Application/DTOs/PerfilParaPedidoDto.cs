namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>O perfil como quem pede acesso o escolhe: sem a composição administrativa.</summary>
/// <param name="Escopo">Onde o perfil se concede: <c>unidade</c> ou <c>instituicao</c>.</param>
/// <param name="ExigeEncarregada">O perfil reúne dado pessoal ou sensível, e a encarregada de dados também aprova.</param>
public sealed record PerfilParaPedidoDto(
    Guid Id,
    string Nome,
    string Descricao,
    string Escopo,
    bool ExigeEncarregada);
