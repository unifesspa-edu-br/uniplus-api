namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O aceite da recomendação da proteção de dados sobre um fato: aplica o aceite no fato, registra o ato na mesma
/// transação da mudança e guarda o retrato do fato aceito, de onde vem o aviso à proteção de dados.
/// </summary>
internal sealed class AceiteDaRecomendacao
{
    private readonly IAtoDaProtecaoDoCatalogoRepository _atos;
    private readonly AtorDoAto _ator;
    private readonly DateTimeOffset _em;

    private RetratoDoItemDaProtecao? _aceito;

    public AceiteDaRecomendacao(IAtoDaProtecaoDoCatalogoRepository atos, AtorDoAto ator, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(atos);
        ArgumentNullException.ThrowIfNull(ator);

        _atos = atos;
        _ator = ator;
        _em = em;
    }

    public async Task<(Result Resposta, IEnumerable<object> Mensagens)> AplicarAsync(
        AceitarRecomendacaoDoFatoCandidatoCommand command, AlteracaoConferidaDoFato alteracoes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(alteracoes);

        Result resultado = await alteracoes.AplicarAsync(command.Id, AceitarNoFato, cancellationToken).ConfigureAwait(false);

        return resultado.IsFailure || _aceito is not { } fato
            ? (resultado, [])
            : (resultado, AvisosDaProtecaoDoCatalogo.DoAceite(
                command.Destinatarios, "fato", fato.Nome, fato.Codigo, fato.Protecao.Classificacao));
    }

    private Result AceitarNoFato(FatoCandidato fato, CatalogoDeFatos catalogo)
    {
        Result aceita = fato.AceitarRecomendacaoDaProtecao(_ator.ContaId, _em, catalogo);
        if (aceita.IsSuccess)
        {
            _aceito = new FatoSobAprovacao(fato).Retrato();
            _atos.Adicionar(AtoDaProtecaoDoCatalogo.Registrar(_aceito, AtoDaProtecao.Aceite, _ator, _em));
        }

        return aceita;
    }
}
