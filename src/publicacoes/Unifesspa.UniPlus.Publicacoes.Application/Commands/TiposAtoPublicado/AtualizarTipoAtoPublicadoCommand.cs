namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza uma versão de tipo de ato. O <c>Codigo</c> é <b>imutável</b> — é a
/// identidade do tipo (a série de vigências agrupa-se por ele, e a vaga de uma
/// linhagem de atos únicos por objeto é chaveada por ele, ADR-0107); o payload
/// reapresenta o mesmo valor, e o agregado recusa qualquer divergência.
/// </summary>
/// <remarks>
/// <c>Codigo</c>/<c>Nome</c> são <c>string?</c>, não <c>string</c> (ADR-0125) —
/// mesma justificativa de <see cref="CriarTipoAtoPublicadoCommand"/>.
/// <para>
/// <c>PublicavelComoAvulso</c> e <c>ExigeAtoCorrigido</c> são obrigatórios no corpo: um valor implícito
/// (<c>false</c>) faria o cliente que não os conhece desligar o atributo em silêncio. Ausentes, o corpo
/// é recusado com 400.
/// </para>
/// </remarks>
public sealed record AtualizarTipoAtoPublicadoCommand(
    Guid Id,
    string? Codigo,
    string? Nome,
    bool CongelaConfiguracao,
    bool UnicoPorObjeto,
    bool EfeitoIrreversivel,
    bool EhResultado,
    [property: JsonRequired] bool PublicavelComoAvulso,
    [property: JsonRequired] bool ExigeAtoCorrigido,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim = null,
    string? BaseLegal = null) : ICommand<Result>;
