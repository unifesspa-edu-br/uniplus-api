namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

// Mapeia AcaoQuandoIndeferido ↔ string (varchar) usando o código canônico
// UPPER_SNAKE (RECLASSIFICAR_AC…), não o nome PascalCase do enum. Aplicado a uma
// propriedade nullable (AcaoQuandoIndeferido?) — o EF encapsula null
// automaticamente; este converter só vê valores não-nulos. A reidratação falha
// explicitamente (com contexto) caso a coluna seja corrompida fora do fluxo.
public sealed class AcaoQuandoIndeferidoValueConverter : ValueConverter<AcaoQuandoIndeferido, string>
{
    public AcaoQuandoIndeferidoValueConverter()
        : base(
            acao => AcoesQuandoIndeferido.ParaTokenCanonico(acao),
            codigo => Reidratar(codigo))
    {
    }

    private static AcaoQuandoIndeferido Reidratar(string codigo)
    {
        if (!AcoesQuandoIndeferido.TryAnalisar(codigo, out AcaoQuandoIndeferido acao))
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(AcaoQuandoIndeferido)}: '{codigo}'. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return acao;
    }
}
