namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O conteúdo de uma obrigatoriedade legal como o cliente o enviou, ainda sem validar: criação e atualização
/// o conferem pelas mesmas regras. Os campos chegam anuláveis porque a ausência é violação de campo do domínio
/// (ADR-0125), não falha de leitura do corpo.
/// </summary>
public interface IObrigatoriedadeLegalDoComando
{
    string? TipoProcessoCodigo { get; }

    CategoriaObrigatoriedade Categoria { get; }

    string? RegraCodigo { get; }

    PredicadoObrigatoriedade? Predicado { get; }

    DateOnly? VigenciaInicio { get; }

    DateOnly? VigenciaFim { get; }

    TextoNormativo ComoTextoNormativo();
}
