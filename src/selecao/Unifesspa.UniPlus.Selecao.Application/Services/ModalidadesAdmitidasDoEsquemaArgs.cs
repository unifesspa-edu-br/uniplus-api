namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Extrai <c>esquema_args.modalidades_admitidas</c> de uma <see cref="RegraCatalogo"/> — ponto
/// único compartilhado por <see cref="Commands.ProcessosSeletivos.ConfiguracaoDistribuicaoVagasResolver"/>
/// (que resolve o rol para a validação de domínio) e por
/// <see cref="Mappings.RegraCatalogoMapping"/> (que o projeta no <c>RegraCatalogoDto</c>).
/// </summary>
/// <remarks>
/// O rol vive no esquema porque ele já compõe o hash da definição
/// (<see cref="Domain.ValueObjects.HashCanonicalComputer.ComputeRegraCatalogo"/>): um campo
/// próprio em <c>RegraCatalogo</c> mudaria a fórmula do hash e, com ela, o valor congelado nas
/// referências das versões já publicadas.
/// <para>Ausente ou nulo significa rol aberto — a regra não restringe o conjunto.</para>
/// </remarks>
public static class ModalidadesAdmitidasDoEsquemaArgs
{
    public static IReadOnlyList<string>? Extrair(RegraCatalogo regra)
    {
        ArgumentNullException.ThrowIfNull(regra);

        return regra.EsquemaArgs.TryGetProperty("modalidades_admitidas", out JsonElement rol)
            && rol.ValueKind == JsonValueKind.Array
            ? [.. rol.EnumerateArray()
                .Where(static item => item.ValueKind == JsonValueKind.String)
                .Select(static item => item.GetString()!)]
            : null;
    }
}
