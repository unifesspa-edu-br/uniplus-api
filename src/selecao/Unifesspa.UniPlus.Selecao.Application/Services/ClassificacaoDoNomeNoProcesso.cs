namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// A proteção do nome de um processo: a classificação mais alta que qualquer das versões dele congelou, porque a listagem
/// mostra inscrições de todas as versões do processo, e a base legal só vale resolvida em todas. Versão que não se lê, ou
/// processo sem versão, vale como sensível sem base legal.
/// </summary>
internal static class ClassificacaoDoNomeNoProcesso
{
    /// <summary>
    /// Se a classe do nome entra nos pares que a listagem pede ao projetor: o identificador nunca sai pelo nome, e o
    /// sensível só entra com a base legal congelada em todas as versões, para que a trilha não registre uma classe que o
    /// nome não vai usar.
    /// </summary>
    public static bool EntraNosPares(ClassificacaoDeProtecao classificacao, string? baseLegal) =>
        classificacao != ClassificacaoDeProtecao.Identificador
        && (classificacao != ClassificacaoDeProtecao.Sensivel || !string.IsNullOrWhiteSpace(baseLegal));

    public static async Task<(ClassificacaoDeProtecao Classificacao, string? BaseLegal)> ResolverAsync(
        Guid processoSeletivoId,
        IProcessoSeletivoRepository processoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<VersaoConfiguracao> versoes = await processoRepository
            .ObterVersoesAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false);
        if (versoes.Count == 0)
        {
            return (ClassificacaoDeProtecao.Sensivel, null);
        }

        ClassificacaoDeProtecao maisAlta = ClassificacaoDeProtecao.Nenhuma;
        List<string> basesLegais = [];
        bool resolvida = true;
        foreach (VersaoConfiguracao versao in versoes)
        {
            Result<EnvelopeReidratado> envelope = ContextoDaVersaoFixadaResolver.Reidratar(versao, registroCodecs);
            if (envelope.IsFailure)
            {
                maisAlta = ClassificacaoDeProtecao.Sensivel;
                resolvida = false;
                continue;
            }

            FatosDaVersao fatos = FatosDaVersao.De(envelope.Value!);
            ClassificacaoDeProtecao daVersao = fatos.ClassificacaoDoNome();
            maisAlta = daVersao > maisAlta ? daVersao : maisAlta;
            if (fatos.BaseLegalDoNome() is { } baseLegal)
            {
                basesLegais.Add(baseLegal);
            }
            else
            {
                resolvida = false;
            }
        }

        return (maisAlta, resolvida ? string.Join("; ", basesLegais.Distinct(StringComparer.Ordinal)) : null);
    }
}
