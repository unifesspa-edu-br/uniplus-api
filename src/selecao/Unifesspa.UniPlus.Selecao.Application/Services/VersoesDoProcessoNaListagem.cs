namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// O que a listagem lê das versões do processo, cada versão reidratada uma vez: a proteção do nome e o nome das ofertas de
/// cada versão. A proteção do nome é a classificação mais alta que qualquer das versões congelou, porque a listagem mostra
/// inscrições de todas as versões do processo, e a base legal só vale resolvida em todas. Versão que não se lê, ou processo
/// sem versão, vale como sensível sem base legal.
/// </summary>
internal static class VersoesDoProcessoNaListagem
{
    /// <summary>
    /// Se a classe do nome entra nos pares que a listagem pede ao projetor: o identificador nunca sai pelo nome, e o
    /// sensível só entra com a base legal congelada em todas as versões, para que a trilha não registre uma classe que o
    /// nome não vai usar.
    /// </summary>
    public static bool EntraNosPares(ClassificacaoDeProtecao classificacao, string? baseLegal) =>
        classificacao != ClassificacaoDeProtecao.Identificador
        && (classificacao != ClassificacaoDeProtecao.Sensivel || !string.IsNullOrWhiteSpace(baseLegal));

    public static async Task<LeituraDasVersoes> LerAsync(
        Guid processoSeletivoId,
        IProcessoSeletivoRepository processoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<VersaoConfiguracao> versoes = await processoRepository
            .ObterVersoesAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, IReadOnlyDictionary<Guid, string>> nomesPorVersao = [];
        if (versoes.Count == 0)
        {
            return new LeituraDasVersoes(ClassificacaoDeProtecao.Sensivel, null, nomesPorVersao);
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

            nomesPorVersao[versao.Id] = OfertasEscolhidas.NomesDe(ContextoDaVersaoFixadaResolver.OfertasDaVersao(envelope.Value!.Grafo));
        }

        return new LeituraDasVersoes(maisAlta, resolvida ? string.Join("; ", basesLegais.Distinct(StringComparer.Ordinal)) : null, nomesPorVersao);
    }
}

/// <summary>As versões do processo lidas para a listagem.</summary>
/// <param name="ClassificacaoDoNome">A classe mais alta que as versões congelaram para o nome.</param>
/// <param name="BaseLegalDoNome">A base legal do nome, resolvida em todas as versões; nula quando alguma não a tem.</param>
/// <param name="NomesDasOfertasPorVersao">Por versão legível, o nome de cada oferta dela.</param>
internal sealed record LeituraDasVersoes(
    ClassificacaoDeProtecao ClassificacaoDoNome,
    string? BaseLegalDoNome,
    IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, string>> NomesDasOfertasPorVersao);
