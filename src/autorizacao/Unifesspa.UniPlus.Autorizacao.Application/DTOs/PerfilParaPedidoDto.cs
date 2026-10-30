namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>O perfil como quem pede acesso o escolhe: sem a composição administrativa.</summary>
/// <param name="Escopo">Onde o perfil se concede: <c>unidade</c> ou <c>instituicao</c>.</param>
/// <param name="ExigeAprovacaoDeProtecaoDeDados">O perfil reúne dado pessoal ou sensível, e a proteção de dados também aprova.</param>
/// <param name="Governanca">Papel de governança do perfil de sistema, cujo pedido exige o documento de designação; nulo nos perfis comuns.</param>
public sealed record PerfilParaPedidoDto(
    Guid Id,
    string Nome,
    string Descricao,
    string Escopo,
    bool ExigeAprovacaoDeProtecaoDeDados,
    string? Governanca);
