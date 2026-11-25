namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

internal sealed class PontoDoElemento(IMutableProperty colecao, IMutableElementType elemento) : IPontoDeConversao
{
    public Type TipoDeclarado => elemento.ClrType;

    public string Descricao => $"O elemento da coleção '{colecao.Name}' de '{colecao.DeclaringType.DisplayName()}'";

    public ValueConverter? Conversor => elemento.GetValueConverter();

    public Type? TipoNoProvedor => elemento.GetProviderClrType();

    public string? Comentario => colecao.GetComment();

    public void DefinirComentario(string comentario) => colecao.SetComment(comentario);

    public void DefinirConversor(ValueConverter conversor) => elemento.SetValueConverter(conversor);
}
