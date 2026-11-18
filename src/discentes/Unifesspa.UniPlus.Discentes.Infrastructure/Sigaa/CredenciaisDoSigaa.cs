namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

/// <summary>
/// O usuário de serviço e a senha com que o Uni+ se autentica no SIGAA, montados a partir das
/// chaves <c>Sigaa:Usuario</c> e <c>Sigaa:Senha</c>. A representação textual não traz a senha.
/// </summary>
/// <param name="Usuario">O usuário de serviço.</param>
/// <param name="Senha">A senha, que vem do cofre em execução real.</param>
public sealed record CredenciaisDoSigaa(string Usuario, string Senha)
{
    public override string ToString() => $"[CredenciaisDoSigaa Usuario={Usuario}]";
}
