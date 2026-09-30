namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Desativar um fato ou um valor no catálogo recusa só vínculo novo (ADR-0136): a configuração
/// que já usava o fato, ou já citava o valor, continua com ele. Compara o que o comando propõe
/// com o que o processo já vincula e recusa o que é novo e está desativado.
/// </summary>
internal static class ConferenciaDeVinculoNovo
{
    public const string FatoDesativado = "ProcessoSeletivo.FatoDesativado";
    public const string ValorDesativado = "ProcessoSeletivo.ValorDeDominioDesativado";

    public static Result Conferir(
        IReadOnlyDictionary<string, FatoCandidatoView> catalogo,
        VinculosDeFatos existentes,
        VinculosDeFatos propostos)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(existentes);
        ArgumentNullException.ThrowIfNull(propostos);

        string? fatoDesativado = propostos.Fatos
            .Where(fato => !existentes.Fatos.Contains(fato))
            .Order(StringComparer.Ordinal)
            .FirstOrDefault(fato => catalogo.TryGetValue(fato, out FatoCandidatoView? view) && !view.Ativo);
        if (fatoDesativado is not null)
        {
            return Result.Failure(new DomainError(
                FatoDesativado,
                $"O fato '{fatoDesativado}' está desativado no catálogo e não aceita vínculo novo."));
        }

        IEnumerable<(string Fato, string Valor)> valoresNovos = propostos.Valores
            .Where(valor => !existentes.Valores.Contains(valor))
            .OrderBy(static v => v.Fato, StringComparer.Ordinal)
            .ThenBy(static v => v.Valor, StringComparer.Ordinal);
        foreach ((string fato, string valor) in valoresNovos)
        {
            bool desativado = catalogo.TryGetValue(fato, out FatoCandidatoView? view)
                && view.ValoresDominioDeclarados?.Any(d => d.Codigo == valor && !d.Ativo) == true;
            if (desativado)
            {
                return Result.Failure(new DomainError(
                    ValorDesativado,
                    $"O valor '{valor}' do fato '{fato}' está desativado no catálogo e não aceita vínculo novo."));
            }
        }

        return Result.Success();
    }
}
