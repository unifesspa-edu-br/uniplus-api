namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <remarks>
/// <c>Codigo</c> e <c>Nome</c> são <c>string?</c>, não <c>string</c> (ADR-0125):
/// sem validator FluentValidation garantindo não-nulo a montante, o model
/// binding automático do <c>[ApiController]</c> interceptaria um campo
/// ausente/nulo com um 400 genérico do ASP.NET, antes de o domínio rodar.
/// <para>
/// Os dois sinalizadores de caráter admitido são anuláveis pelo mesmo motivo, com um
/// agravante: <c>bool</c> não-anulável faria campo ausente virar <c>false</c> em silêncio, e
/// o tipo nasceria declarando que não compõe a nota final sem ninguém ter dito isso.
/// </para>
/// </remarks>
public sealed record CriarTipoEtapaCommand(
    string? Codigo,
    string? Nome,
    bool? AdmitePontuacao,
    bool? AdmiteEliminacao,
    string? Descricao = null) : ICommand<Result<Guid>>;
