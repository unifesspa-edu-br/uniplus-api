namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Cria um tipo de banca (UNI-REQ-0064): código do vocabulário de tipos de banca (chave natural imutável),
/// nome, fase típica opcional (rótulo orientativo, não vinculante), descrição
/// opcional, o papel do tipo, as duas marcas e os tipos de decisão que o tipo julga. O ator de auditoria (<c>created_by</c>) é carimbado server-side via
/// <c>IUserContext</c>, não no payload.
/// </summary>
/// <remarks>
/// <c>Codigo</c> é <c>string?</c>, não <c>string</c> (ADR-0125): sem validator
/// FluentValidation garantindo não-nulo a montante, o model binding automático do
/// <c>[ApiController]</c> interceptaria um campo ausente/nulo com um 400 genérico
/// do ASP.NET, antes de o domínio rodar.
/// </remarks>
public sealed record CriarTipoBancaCommand(
    [property: AceitaCodigosDe<TipoDeBanca>] string? Codigo,
    string? Nome = null,
    string? FaseTipica = null,
    string? Descricao = null,
    [property: AceitaCodigosDe<PapelDaBanca>] string? Papel = null,
    bool ExigeDuasOuMaisBancas = false,
    bool EncerraRecursoNaPrimeiraInstancia = false,
    [property: AceitaCodigosDe<TipoMotivoDeDecisao>] IReadOnlyList<string?>? TiposDeDecisaoJulgados = null) : ICommand<Result<Guid>>
{
    public DeclaracaoDoTipoDeBanca Declaracao() => new(
        Nome, FaseTipica, Descricao, Papel, ExigeDuasOuMaisBancas, EncerraRecursoNaPrimeiraInstancia, TiposDeDecisaoJulgados);
}
