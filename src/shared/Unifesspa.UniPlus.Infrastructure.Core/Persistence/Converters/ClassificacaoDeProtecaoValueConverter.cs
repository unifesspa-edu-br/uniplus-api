namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

// Mapeia a escala única de proteção ↔ token canônico (UPPER_SNAKE) no banco. É o único conversor
// da classificação: todo módulo que a guarda usa este e o mesmo tamanho de coluna. A leitura de
// um token fora do vocabulário falha alto, em vez de produzir uma classificação inventada.
public sealed class ClassificacaoDeProtecaoValueConverter : ValueConverter<ClassificacaoDeProtecao, string>
{
    /// <summary>Tamanho da coluna que guarda o token da classificação.</summary>
    public const int MaxLength = 20;

    public ClassificacaoDeProtecaoValueConverter()
        : base(
            classificacao => ClassificacoesDeProtecao.ParaTokenCanonico(classificacao),
            token => ClassificacoesDeProtecao.Analisar(token))
    {
    }
}
