namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

using Unifesspa.UniPlus.Kernel.Results;

// Helper compartilhado pelos converters de Value Object.
//
// O padrão `Cpf.Criar(x).Value!` em ValueConverters é convidativo, mas
// usar o forgiving null `!` no caminho de materialização do EF Core gera
// NullReferenceException tardia (no primeiro uso da propriedade) caso o
// dado armazenado esteja corrompido — pior diagnóstico do que falhar
// explicitamente com contexto.
//
// Este helper centraliza o fail-fast: ao reidratar um VO, se o Result
// vier como Failure ou Value null, lançamos InvalidOperationException
// com o Code do erro e o nome do VO, evidenciando a corrupção no banco.
internal static class ValueObjectMaterialization
{
    public static T Reidratar<T>(Result<T> resultado, string nomeVo) where T : class
    {
        ArgumentNullException.ThrowIfNull(resultado);

        if (resultado.IsFailure || resultado.Value is null)
        {
            string codigo = resultado.Error?.Code ?? "Desconhecido";
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nomeVo}: {codigo}. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return resultado.Value;
    }
}
