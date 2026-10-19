namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// A avaliação da inscrição diante das opções de curso que ela escolhe. O formulário, os valores
/// admitidos e os documentos saem de uma avaliação só, sobre a união das distribuições das ofertas
/// escolhidas (<see cref="ContextoDaVersaoFixada.Configuracao"/>). As modalidades são por opção: cada
/// oferta tem a distribuição de vagas e as regras de aplicabilidade dela, então a 1ª e a 2ª opção são
/// avaliadas cada uma na sua, sobre as mesmas respostas — a elegibilidade do candidato é uma só.
/// </summary>
public static class AvaliacaoDaInscricaoPorOpcao
{
    public static Result<ResultadoDaAvaliacaoDaInscricao> Avaliar(ContextoDaVersaoFixada contexto, EntradaDaAvaliacaoDaInscricao entrada)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(entrada);

        Result<ResultadoDaAvaliacaoDaInscricao> dasEscolhidas = AvaliacaoDaInscricao.Avaliar(contexto.Configuracao, entrada);
        if (contexto.ConfiguracaoDaSegundaOpcao is not { } configuracaoDaSegunda || dasEscolhidas.IsFailure)
        {
            return dasEscolhidas;
        }

        return Result<ResultadoDaAvaliacaoDaInscricao>.Success(dasEscolhidas.Value! with
        {
            Modalidades = AvaliacaoDaInscricao.AvaliarModalidades(contexto.ConfiguracaoDaPrimeiraOpcao, entrada),
            ModalidadesSegundaOpcao = AvaliacaoDaInscricao.AvaliarModalidades(configuracaoDaSegunda, entrada),
        });
    }
}
