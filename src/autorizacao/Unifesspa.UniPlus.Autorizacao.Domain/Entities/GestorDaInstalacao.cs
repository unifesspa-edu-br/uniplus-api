namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

/// <summary>
/// Um dos dois gestores que a instalação designa, pelo par emissor e <c>sub</c> do token. Nunca
/// guarda nome, CPF nem e-mail.
/// </summary>
public sealed class GestorDaInstalacao
{
    public string Emissor { get; private set; }

    public string Sub { get; private set; }

    internal GestorDaInstalacao(string emissor, string sub)
    {
        Emissor = emissor;
        Sub = sub;
    }
}
