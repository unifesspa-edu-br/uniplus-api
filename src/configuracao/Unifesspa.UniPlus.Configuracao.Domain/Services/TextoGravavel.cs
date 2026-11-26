namespace Unifesspa.UniPlus.Configuracao.Domain.Services;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O texto do modelo de formulário na forma em que o banco o grava: sem caractere nulo e em Unicode
/// válido. A regra é do modelo de formulário — a recusa emite <c>ModeloFormularioErrorCodes.TextoNaoGravavel</c> —,
/// e quem consulta o banco com um texto recebido o confere antes, para a recusa ser a do modelo e não um erro do banco.
/// </summary>
public static class TextoGravavel
{
    public static bool EhGravavel(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        return !texto.Contains('\0', StringComparison.Ordinal) && TextoNormalizavel.TentarNormalizar(texto, out _);
    }

    /// <summary>A recusa do campo cujo texto o banco não grava; sem recusa quando o texto é gravável ou ausente.</summary>
    public static IEnumerable<FieldError> Recusar(string campo, string? texto)
    {
        if (texto is not null && !EhGravavel(texto))
        {
            yield return new(campo, new DomainError(
                ModeloFormularioErrorCodes.TextoNaoGravavel, "O texto contém o caractere nulo (U+0000) ou não é Unicode válido."));
        }
    }

}
