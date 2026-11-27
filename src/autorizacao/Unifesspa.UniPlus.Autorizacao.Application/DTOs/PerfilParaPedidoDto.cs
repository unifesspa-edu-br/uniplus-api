namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>O perfil como quem pede acesso o escolhe: sem a composição administrativa.</summary>
/// <param name="Escopo">Onde o perfil se concede: <c>UNIDADE</c> ou <c>INSTITUICAO</c>.</param>
/// <param name="ExigeAprovacaoDeProtecaoDeDados">O perfil reúne dado pessoal ou sensível, e a proteção de dados também aprova.</param>
/// <param name="Governanca">Papel de governança do perfil de sistema, cujo pedido exige o documento de designação; nulo nos perfis comuns.</param>
public sealed record PerfilParaPedidoDto(
    Guid Id,
    string Nome,
    string Descricao,
    CodigoComRotulo<TipoDeEscopo> Escopo,
    bool ExigeAprovacaoDeProtecaoDeDados,
    CodigoComRotulo<TipoDeGovernanca>? Governanca);
