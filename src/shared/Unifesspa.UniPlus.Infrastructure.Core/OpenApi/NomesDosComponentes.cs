namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using System.Collections.Concurrent;
using System.Text.Json.Serialization.Metadata;

using Microsoft.AspNetCore.OpenApi;

/// <summary>
/// Dá o nome de cada componente do documento OpenAPI, um objeto por documento: o dono de um nome vale só dentro dele. O
/// código com rótulo de um enum leva o nome do enum, o código com descrição leva o nome do enum seguido de
/// <c>ComDescricao</c>, e os códigos aceitos do enum levam o nome do enum seguido de <c>Codigo</c>; os demais tipos ficam
/// no nome padrão. O tipo e a sua versão anulável são o mesmo tipo. Dois tipos diferentes nunca recebem o mesmo nome: a
/// geração do documento falha, em vez de publicar um componente que serve a tipos diferentes.
/// </summary>
internal sealed class NomesDosComponentes
{
    private readonly ConcurrentDictionary<string, Type> _donos = new(StringComparer.Ordinal);

    public string? Nome(JsonTypeInfo tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        string? nome = FormatoDeCodigo.De(tipo.Type)?.NomeDoComponente ?? OpenApiOptions.CreateDefaultSchemaReferenceId(tipo);

        if (nome is not null)
        {
            ExigirNomeDeUmSoTipo(nome, Nullable.GetUnderlyingType(tipo.Type) ?? tipo.Type);
        }

        return nome;
    }

    /// <summary>O nome do componente dos códigos aceitos do enum, que passa a ser do enum.</summary>
    /// <exception cref="InvalidOperationException">Outro tipo do documento já tem esse nome.</exception>
    public string NomeDosCodigos(Type enumDeContrato)
    {
        ArgumentNullException.ThrowIfNull(enumDeContrato);

        string nome = $"{enumDeContrato.Name}Codigo";
        ExigirNomeDeUmSoTipo(nome, enumDeContrato);

        return nome;
    }

    private void ExigirNomeDeUmSoTipo(string nome, Type tipo)
    {
        Type dono = _donos.GetOrAdd(nome, tipo);

        if (dono != tipo)
        {
            throw new InvalidOperationException(
                $"O componente '{nome}' do documento OpenAPI serviria a dois tipos diferentes: {dono.FullName} e {tipo.FullName}. "
                + "Dê nomes diferentes aos tipos ou aos enums.");
        }
    }
}
