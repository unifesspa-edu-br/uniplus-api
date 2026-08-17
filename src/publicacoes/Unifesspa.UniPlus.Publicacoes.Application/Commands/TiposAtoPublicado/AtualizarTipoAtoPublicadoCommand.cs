namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

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
/// </remarks>
public sealed record AtualizarTipoAtoPublicadoCommand(
    Guid Id,
    string? Codigo,
    string? Nome,
    bool CongelaConfiguracao,
    bool UnicoPorObjeto,
    bool EfeitoIrreversivel,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim = null,
    string? BaseLegal = null) : ICommand<Result>;
