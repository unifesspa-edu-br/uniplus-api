namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Um nó do grafo de dependência conjunto (Story #928, §6): identidade <c>(Classe, Codigo)</c>. Campo
/// e fato do mesmo código são nós distintos — a igualdade de valor os separa pela <see cref="Classe"/>.
/// </summary>
/// <remarks>
/// Para campo e fato, <see cref="Codigo"/> é o código do fato no vocabulário; para exigência, é a
/// identidade estável da exigência no processo. A identidade canônica de serialização
/// (<c>tipoDeNo/escopo/codigo</c> em UTF-8 NFC) é da fatia de congelamento (§7); aqui o par
/// <c>(Classe, Codigo)</c> basta como identidade de runtime para a detecção de ciclo e a ordem.
/// </remarks>
public sealed record NoGrafoDependencia(ClasseNoGrafo Classe, string Codigo)
{
    /// <summary>Rótulo legível do nó, para mensagens de erro de ciclo.</summary>
    public string Rotulo => $"{Classe}:{Codigo}";
}
