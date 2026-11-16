namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// A natureza legal e a composição de vagas da modalidade selecionada têm token canônico único na plataforma (o
/// do cadastro de modalidades) e gravam o token, e não o nome do enum. A coluna que guarda um valor fora do vocabulário foi
/// alterada fora da aplicação, e a leitura falha com o valor, em vez de assumir um padrão.
/// </summary>
internal static class ConversoresDosTokensDeVagas
{
    internal static ValueConverter<NaturezaLegalModalidade, string> NaturezaLegal { get; } = new(
        natureza => natureza.ToCodigo(),
        token => Reidratar(token, NaturezaLegalModalidadeCodigo.FromCodigo(token), NaturezaLegalModalidade.Nenhuma));

    internal static ValueConverter<ComposicaoVagasModalidade, string> ComposicaoDeVagas { get; } = new(
        composicao => composicao.ToCodigo(),
        token => Reidratar(token, ComposicaoVagasModalidadeCodigo.FromCodigo(token), ComposicaoVagasModalidade.Nenhuma));

    private static TEnum Reidratar<TEnum>(string token, TEnum valor, TEnum sentinela)
        where TEnum : struct, Enum =>
        EqualityComparer<TEnum>.Default.Equals(valor, sentinela)
            ? throw new InvalidOperationException(
                $"Dado inválido no banco ao reidratar {typeof(TEnum).Name}: '{token}'. Verifique se houve alteração manual da coluna fora do fluxo da aplicação.")
            : valor;
}
