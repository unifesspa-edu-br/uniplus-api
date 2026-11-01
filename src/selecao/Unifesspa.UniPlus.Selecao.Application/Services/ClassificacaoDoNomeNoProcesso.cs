namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>A proteção que as versões do processo congelaram para o nome exibido.</summary>
/// <param name="Classificacao">A classificação mais alta entre as versões.</param>
/// <param name="BaseLegalResolvida">Se todas as versões congelaram a base legal do nome.</param>
internal sealed record ProtecaoDoNome(ClassificacaoDeProtecao Classificacao, bool BaseLegalResolvida)
{
    /// <summary>O nome não resolvido: sensível e sem base legal.</summary>
    public static ProtecaoDoNome NaoResolvida { get; } = new(ClassificacaoDeProtecao.Sensivel, false);

    /// <summary>
    /// Se a proteção congelada deixa o nome ser lido na listagem. O identificador nunca sai por aqui, porque a listagem não
    /// revela; o sensível só sai com a base legal congelada, como no detalhe.
    /// </summary>
    public bool SaiNaListagem => Classificacao switch
    {
        ClassificacaoDeProtecao.Identificador => false,
        ClassificacaoDeProtecao.Sensivel => BaseLegalResolvida,
        _ => true,
    };
}

/// <summary>
/// A proteção do nome de um processo: a classificação mais alta que qualquer das versões dele congelou, porque a listagem
/// mostra inscrições de todas as versões do processo, e a base legal só vale resolvida em todas. Versão que não se lê, ou
/// processo sem versão, vale como sensível sem base legal.
/// </summary>
internal static class ClassificacaoDoNomeNoProcesso
{
    public static async Task<ProtecaoDoNome> ResolverAsync(
        Guid processoSeletivoId,
        IProcessoSeletivoRepository processoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<VersaoConfiguracao> versoes = await processoRepository
            .ObterVersoesAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false);
        if (versoes.Count == 0)
        {
            return ProtecaoDoNome.NaoResolvida;
        }

        ClassificacaoDeProtecao maisAlta = ClassificacaoDeProtecao.Nenhuma;
        bool baseLegal = true;
        foreach (VersaoConfiguracao versao in versoes)
        {
            Result<EnvelopeReidratado> envelope = ContextoDaVersaoFixadaResolver.Reidratar(versao, registroCodecs);
            if (envelope.IsFailure)
            {
                maisAlta = ClassificacaoDeProtecao.Sensivel;
                baseLegal = false;
                continue;
            }

            FatosDaVersao fatos = FatosDaVersao.De(envelope.Value!);
            ClassificacaoDeProtecao daVersao = fatos.ClassificacaoDoNome();
            maisAlta = daVersao > maisAlta ? daVersao : maisAlta;
            baseLegal &= fatos.NomeTemBaseLegal();
        }

        return new ProtecaoDoNome(maisAlta, baseLegal);
    }
}
