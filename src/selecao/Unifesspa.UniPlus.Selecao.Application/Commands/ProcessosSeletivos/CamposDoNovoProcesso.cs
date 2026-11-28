namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Domain.CodigosDeContrato;
using Kernel.Results;

/// <summary>
/// Os dados que o pedido de criação do processo precisa trazer: nome, tipo, origem dos candidatos e unidade. Só quem
/// tem permissão na unidade chega aqui, e todas as causas saem juntas (ADR-0125), cada uma no campo do pedido, sem
/// repetir o valor recebido.
/// </summary>
internal static class CamposDoNovoProcesso
{
    internal const string CampoNome = "nome";
    internal const string CampoTipo = "tipoProcessoOrigemId";
    internal const string CampoOrigem = "origemCandidatos";
    internal const string CampoUnidade = "unidadeAdministradoraOrigemId";

    internal static IReadOnlyList<FieldError> Conferir(CriarProcessoSeletivoCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return [.. RecusasDoNome(command.Nome), .. RecusaDoTipo(command.TipoProcessoOrigemId), .. RecusasDaOrigem(command.OrigemCandidatos), .. RecusaDaUnidade(command.UnidadeAdministradoraOrigemId)];
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

    private static IEnumerable<FieldError> RecusaDoTipo(Guid tipo) =>
        tipo == Guid.Empty ? [Recusa(CampoTipo, CodigosDeValidacao.Obrigatorio, "O tipo do processo seletivo é obrigatório.")] : [];

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

    private static IEnumerable<FieldError> RecusaDaUnidade(Guid unidade) =>
        unidade == Guid.Empty ? [Recusa(CampoUnidade, CodigosDeValidacao.Obrigatorio, "A unidade administradora do processo seletivo é obrigatória.")] : [];

    private static FieldError Recusa(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));
}
