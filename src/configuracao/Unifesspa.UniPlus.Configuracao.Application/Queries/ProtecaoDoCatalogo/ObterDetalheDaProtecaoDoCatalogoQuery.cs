namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>Os cadastros do catálogo cuja proteção passa pela aprovação da proteção de dados.</summary>
public enum CadastroDaProtecaoDoCatalogo
{
    TipoDocumento,
    Fato,
}

/// <summary>
/// O item do catálogo com o que a proteção de dados precisa ver para decidir (ADR-0153), em qualquer situação da
/// aprovação; nulo quando o item não existe. A permissão de aprovar é verificada na borda.
/// </summary>
public sealed record ObterDetalheDaProtecaoDoCatalogoQuery(CadastroDaProtecaoDoCatalogo Cadastro, Guid Id) : IQuery<DetalheDaProtecaoDoCatalogoDto?>;
