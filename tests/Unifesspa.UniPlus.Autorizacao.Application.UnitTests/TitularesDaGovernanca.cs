namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>Quem aprova pela gestão e pela proteção de dados: Diogo pela gestão, Marlene pela proteção de dados e Isabella pelas duas.</summary>
internal sealed class TitularesDaGovernanca : ITitularesDaPermissao
{
    public const string Emissor = "https://idp.test/realms/unifesspa";
    public const string EmailDiogo = "diogo.bruno.souza@uniplus-test.local";
    public const string EmailIsabella = "isabellapriscilafigueiredo@uniplus-test.local";
    public const string EmailMarlene = "marlenemarciamartins@uniplus-test.local";

    private static readonly string Gestao = CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.PerfisAprovarFuncional).Codigo;

    public static Identidade Diogo { get; } = new(Emissor, "diogo.souza");

    public static Identidade Isabella { get; } = new(Emissor, "isabella.figueiredo");

    public static Identidade Marlene { get; } = new(Emissor, "marlene.martins");

    public Task<IReadOnlyList<string>> ListarEmailsAsync(string codigoDaPermissao, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<TitularDaPermissao>> ListarTitularesComEmailAsync(
        string codigoDaPermissao, CancellationToken cancellationToken = default)
    {
        TitularDaPermissao[] titulares = codigoDaPermissao == Gestao
            ? [new(Emissor, Diogo.Sub, EmailDiogo), new(Emissor, Isabella.Sub, EmailIsabella)]
            : [new(Emissor, Marlene.Sub, EmailMarlene), new(Emissor, Isabella.Sub, EmailIsabella)];

        return Task.FromResult<IReadOnlyList<TitularDaPermissao>>(titulares);
    }
}
