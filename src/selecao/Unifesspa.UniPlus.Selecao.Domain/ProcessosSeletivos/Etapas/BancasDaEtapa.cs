namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

/// <summary>As bancas que a etapa declara, como valores: a cópia do tipo e a regra de deliberação de cada uma, na posição declarada.</summary>
public sealed record BancasDaEtapa
{
    internal BancasDaEtapa(IReadOnlyList<BancaDaEtapa> bancas) =>
        Declaradas = BancaDeclarada.De([.. bancas.Select(static banca => (banca.Tipo, banca.RegraDeDeliberacao, banca.ConsequenciaDaAusencia))]);

    public bool TemBancas => Declaradas.Count > 0;

    /// <summary>Se alguma banca da etapa atribui a nota ou o parecer: é ela que a etapa da nota do ENEM não admite.</summary>
    public bool TemBancaQueAvalia => Declaradas.Any(static banca => banca.Tipo.AvaliaOMerito);

    public IReadOnlyList<BancaDeclarada> Declaradas { get; }

    /// <summary>As bancas da etapa cuja cópia do tipo tem o papel de julgar o recurso em 2ª instância, na posição declarada.</summary>
    public IReadOnlyList<BancaDeclarada> BancasDaSegundaInstancia => [.. Declaradas.Where(static banca => banca.Tipo.JulgaASegundaInstancia)];
}
