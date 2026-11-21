namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Atualiza um tipo de banca existente. O <c>Codigo</c> e o <c>Id</c> são
/// <b>imutáveis</b>: o comando <b>não</b> aceita código — o handler carrega a
/// entidade e a atualiza sem alterá-lo. Papel, marcas e tipos de decisão são editáveis, e a edição
/// substitui o que estava gravado (campo ausente vale vazio ou falso). O ator (<c>updated_by</c>) é carimbado
/// server-side via <c>IUserContext</c>.
/// </summary>
public sealed record AtualizarTipoBancaCommand(
    Guid Id,
    string? Nome = null,
    string? FaseTipica = null,
    string? Descricao = null,
    [property: AceitaCodigosDe<PapelDaBanca>] string? Papel = null,
    bool ExigeDuasOuMaisBancas = false,
    bool EncerraRecursoNaPrimeiraInstancia = false,
    [property: AceitaCodigosDe<TipoMotivoDeDecisao>] IReadOnlyList<string?>? TiposDeDecisaoJulgados = null) : ICommand<Result>
{
    public DeclaracaoDoTipoDeBanca Declaracao() => new(
        Nome, FaseTipica, Descricao, Papel, ExigeDuasOuMaisBancas, EncerraRecursoNaPrimeiraInstancia, TiposDeDecisaoJulgados);
}
