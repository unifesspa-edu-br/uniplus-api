namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

// Mapeia ComposicaoVagas ↔ string (varchar) usando o token canônico UPPER_SNAKE
// (RESIDUAL_DO_VO…), não o nome PascalCase do enum. O comprimento da coluna é
// definido na própria propriedade via HasMaxLength no IEntityTypeConfiguration. A
// reidratação falha explicitamente (com contexto) caso a coluna seja corrompida
// fora do fluxo da aplicação, em vez de um valor de enum silenciosamente inválido.
public sealed class ComposicaoVagasValueConverter : ValueConverter<ComposicaoVagas, string>
{
    public ComposicaoVagasValueConverter()
        : base(
            composicao => ComposicoesVagas.ParaTokenCanonico(composicao),
            token => Reidratar(token))
    {
    }

    private static ComposicaoVagas Reidratar(string token)
    {
        if (!ComposicoesVagas.TryAnalisar(token, out ComposicaoVagas composicao))
        {
            throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {nameof(ComposicaoVagas)}: '{token}'. " +
                "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        }

        return composicao;
    }
}
