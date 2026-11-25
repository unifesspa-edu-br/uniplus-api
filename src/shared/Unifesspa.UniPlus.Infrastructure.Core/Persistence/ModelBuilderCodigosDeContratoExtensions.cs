namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Convenção que grava pelo código de contrato todo enum em que algum membro declara
/// <see cref="CodigoDeContratoAttribute"/>, sem que cada <c>Configuration</c> escreva o próprio conversor.
/// </summary>
public static class ModelBuilderCodigosDeContratoExtensions
{
    /// <summary>
    /// Garante o <see cref="CodigoDeContratoValueConverter{TEnum}"/> em todo valor de enum com código de contrato do
    /// modelo: a propriedade escalar, inclusive a opcional (<c>TEnum?</c>), e o elemento de coleção primitiva, nos tipos de
    /// entidade, nos tipos próprios (<c>owned</c>) e nos tipos complexos, em qualquer nível de aninhamento. Acrescenta
    /// ao comentário da coluna os códigos declarados pelo enum, na ordem de declaração.
    /// </summary>
    /// <remarks>
    /// O ponto sem conversão recebe o conversor do código, um só por enum. O ponto que já usa o conversor do código, por
    /// instância ou por tipo, é aceito como está, e a chamada repetida não muda nada. Não define tamanho nem tipo de
    /// coluna, mas o enum que era gravado por outra representação, como o número, passa a ser gravado como texto: o
    /// tipo da coluna muda no modelo do banco. Chame depois que as configurações registraram os tipos.
    /// </remarks>
    /// <param name="modelBuilder">O <see cref="ModelBuilder"/> em construção.</param>
    /// <exception cref="InvalidOperationException">
    /// Algum valor de enum com código tem outra conversão, de instância, de tipo ou só para o tipo do provedor, como
    /// <c>HasConversion&lt;string&gt;()</c>, ou uma coleção de enum com código não está mapeada como coleção primitiva,
    /// por exemplo com o conversor na coleção inteira. A mensagem nomeia o tipo e a propriedade, e a coleção é
    /// nomeada quando o defeito é do elemento. A primeira ocorrência encontrada interrompe a chamada.
    /// </exception>
    public static void ConfigurarConversaoDosEnumsDeContrato(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        new ConversaoDosEnumsDeContrato().Configurar(modelBuilder.Model);
    }
}
