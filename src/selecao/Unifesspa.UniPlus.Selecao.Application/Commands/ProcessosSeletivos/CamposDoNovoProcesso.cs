namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Domain.CodigosDeContrato;
using Kernel.Results;

/// <summary>
/// Os dados do pedido de criação do processo que não dependem de nenhum cadastro: o nome e a origem dos candidatos.
/// Só quem tem permissão na unidade chega aqui, e todas as causas saem juntas (ADR-0125), cada uma no campo do
/// pedido, sem repetir o valor recebido. A unidade e o tipo informados são conferidos em
/// <see cref="ReferenciasDoNovoProcesso"/>, na posição de cada um.
/// </summary>
internal static class CamposDoNovoProcesso
{
    internal const string CampoNome = "nome";
    internal const string CampoOrigem = "origemCandidatos";

    internal static IReadOnlyList<FieldError> Conferir(CriarProcessoSeletivoCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return [.. RecusasDoNome(command.Nome), .. RecusasDaOrigem(command.OrigemCandidatos)];
    }

    private static IEnumerable<FieldError> RecusasDoNome(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            yield return Recusa(CampoNome, CodigosDeValidacao.Obrigatorio, "O nome do processo seletivo é obrigatório.");
        }
        else if (nome.Length > DocumentoDoRascunho.NomeMaxLength)
        {
            yield return Recusa(CampoNome, CodigosDeValidacao.Tamanho, $"O nome do processo seletivo deve ter no máximo {DocumentoDoRascunho.NomeMaxLength} caracteres.");
        }
    }

    // A origem é obrigatória na criação: o piso mínimo do cronograma deriva dela, nunca do tipo.
    private static IEnumerable<FieldError> RecusasDaOrigem(string? origem)
    {
        if (string.IsNullOrWhiteSpace(origem))
        {
            yield return Recusa(CampoOrigem, CodigosDeValidacao.Obrigatorio, "A origem dos candidatos é obrigatória.");
        }
        else if (!CatalogoDe<OrigemCandidatos>.TentarObter(origem, out _))
        {
            yield return Recusa(CampoOrigem, CodigosDeValidacao.Regra, "A origem dos candidatos é inválida.");
        }
    }

    private static FieldError Recusa(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));
}
