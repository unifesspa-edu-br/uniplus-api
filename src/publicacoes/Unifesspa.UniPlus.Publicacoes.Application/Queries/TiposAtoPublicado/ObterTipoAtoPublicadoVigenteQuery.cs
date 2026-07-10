namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.TiposAtoPublicado;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;

/// <summary>
/// Resolve o significado de um código de tipo de ato numa data: a única versão viva
/// cuja janela semiaberta <c>[inicio, fim)</c> contém <paramref name="Data"/>.
/// </summary>
/// <param name="Codigo">Código do tipo de ato, em UPPER_SNAKE.</param>
/// <param name="Data">Data de referência. Nula significa hoje.</param>
public sealed record ObterTipoAtoPublicadoVigenteQuery(
    string Codigo,
    DateOnly? Data) : IQuery<TipoAtoPublicadoDto?>;
