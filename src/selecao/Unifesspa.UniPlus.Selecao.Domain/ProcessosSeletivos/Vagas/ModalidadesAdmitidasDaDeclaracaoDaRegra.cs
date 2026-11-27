namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using System.Text.Json;

/// <summary>
/// Extrai <c>modalidades_admitidas</c> da declaração de uma <see cref="Entities.RegraCatalogo"/> — ponto
/// único compartilhado por <c>ConfiguracaoDistribuicaoVagasResolver</c>
/// (que resolve o rol para a validação de domínio) e por
/// <c>RegraCatalogoMapping</c> (que o projeta no <c>RegraCatalogoDto</c>).
/// </summary>
/// <remarks>
/// O rol vive na declaração da regra e compõe o hash da definição
/// (<see cref="ValueObjects.HashCanonicalComputer.ComputeRegraCatalogo"/>): um campo
/// próprio em <c>RegraCatalogo</c> mudaria a fórmula do hash e, com ela, o valor congelado nas
/// referências das versões já publicadas.
/// <para>Ausente ou nulo significa rol aberto — a regra não restringe o conjunto.</para>
/// </remarks>
public static class ModalidadesAdmitidasDaDeclaracaoDaRegra
{
    public static IReadOnlyList<string>? Extrair(JsonElement declaracaoDaRegra) =>
        declaracaoDaRegra.ValueKind == JsonValueKind.Object && declaracaoDaRegra.TryGetProperty("modalidades_admitidas", out JsonElement rol)
            && rol.ValueKind == JsonValueKind.Array
            ? [.. rol.EnumerateArray()
                .Where(static item => item.ValueKind == JsonValueKind.String)
                .Select(static item => item.GetString()!)]
            : null;
}
