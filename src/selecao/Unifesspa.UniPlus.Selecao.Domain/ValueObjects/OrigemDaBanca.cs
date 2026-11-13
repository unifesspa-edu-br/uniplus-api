namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O que toda banca declarada guarda do tipo de banca de que foi copiada.</summary>
internal static class OrigemDaBanca
{
    /// <summary>Recusa origem incompleta e devolve código e nome já aparados.</summary>
    internal static (string Codigo, string Nome) Exigir(Guid tipoBancaOrigemId, string codigo, string nome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        if (tipoBancaOrigemId == Guid.Empty)
        {
            throw new ArgumentException("O id de origem do tipo de banca é obrigatório.", nameof(tipoBancaOrigemId));
        }

        return (codigo.Trim(), nome.Trim());
    }
}
