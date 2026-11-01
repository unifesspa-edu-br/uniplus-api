namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Aplica a decisão da proteção de dados ao tipo de documento ou ao fato. A forma do comando é conferida antes
/// de qualquer leitura (ADR-0125); a permissão de aprovar é do endpoint, e o item que não está numa solicitação é
/// recusado pelo próprio circuito do item.
/// </summary>
public static class DecidirProtecaoDoCatalogoCommandHandler
{
    private const string Aprovada = "APROVADA";
    private const string Negada = "NEGADA";

    public static async Task<Result> Handle(
        DecidirProtecaoDoCatalogoCommand command,
        ITipoDocumentoRepository tipos,
        IFatoCandidatoRepository fatos,
        IConfiguracaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(tipos);
        ArgumentNullException.ThrowIfNull(fatos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        string? cadastro = command.Cadastro?.Trim();
        string? decisao = command.Decisao?.Trim();

        List<FieldError> erros = [];
        if (cadastro is not (ProtecaoDoCatalogoMapping.TipoDocumento or ProtecaoDoCatalogoMapping.Fato))
        {
            erros.Add(new("cadastro", new DomainError(
                ProtecaoDoCatalogoErrorCodes.CadastroInvalido,
                $"O cadastro do item deve ser {ProtecaoDoCatalogoMapping.TipoDocumento} ou {ProtecaoDoCatalogoMapping.Fato}.")));
        }

        if (decisao is not (Aprovada or Negada))
        {
            erros.Add(new("decisao", new DomainError(
                ProtecaoDoCatalogoErrorCodes.DecisaoInvalida, $"A decisão deve ser {Aprovada} ou {Negada}.")));
        }

        // A negativa exige a classificação recomendada dentro do vocabulário, antes de qualquer leitura do item (ADR-0125).
        ClassificacaoDeProtecao? recomendadaInformada = TokensDoFato.Classificacao(command.ClassificacaoRecomendada);
        if (decisao == Negada && recomendadaInformada is null or (ClassificacaoDeProtecao)TokensDoFato.ForaDoVocabulario)
        {
            erros.Add(new("classificacaoRecomendada", new DomainError(
                ProtecaoDoCatalogoErrorCodes.RecomendacaoObrigatoria,
                "A negativa exige a classificação de proteção de dados recomendada, dentro do vocabulário fechado.")));
        }

        ClassificacaoDeProtecao? classificacaoConferida = TokensDoFato.Classificacao(command.ClassificacaoProtecao);
        HipoteseLegalTratamento? hipoteseConferida = TokensDoFato.HipoteseLegal(command.HipoteseLegal);
        if (classificacaoConferida is null or (ClassificacaoDeProtecao)TokensDoFato.ForaDoVocabulario)
        {
            erros.Add(ProtecaoConferidaInvalida("classificacaoProtecao"));
        }

        if (hipoteseConferida is null or (HipoteseLegalTratamento)TokensDoFato.ForaDoVocabulario)
        {
            erros.Add(ProtecaoConferidaInvalida("hipoteseLegal"));
        }

        if (string.IsNullOrWhiteSpace(command.FinalidadeTratamento))
        {
            erros.Add(ProtecaoConferidaInvalida("finalidadeTratamento"));
        }

        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        string ator = userContext.UserId ?? "system";
        DateTimeOffset agora = timeProvider.GetUtcNow();
        ClassificacaoDeProtecao recomendada = TokensDoFato.Classificacao(command.ClassificacaoRecomendada) ?? ClassificacaoDeProtecao.Nenhuma;
        ProtecaoDeDados conferida = new(classificacaoConferida!.Value, command.FinalidadeTratamento!.Trim(), hipoteseConferida!.Value);

        if (cadastro == ProtecaoDoCatalogoMapping.Fato)
        {
            return await MutacaoDoFato.AplicarAsync(
                command.ItemId,
                fato => fato.Protecao != conferida
                    ? ProtecaoMudou()
                    : decisao == Aprovada
                        ? fato.AprovarProtecao(ator, agora)
                        : fato.NegarProtecao(recomendada, ator, agora),
                fatos,
                unitOfWork,
                cancellationToken).ConfigureAwait(false);
        }

        return await MutacaoDoTipo.AplicarAsync(
            command.ItemId,
            tipo => tipo.Protecao != conferida
                ? ProtecaoMudou()
                : decisao == Aprovada
                    ? tipo.AprovarProtecao(ator, agora)
                    : tipo.NegarProtecao(recomendada, ator, agora),
            tipos,
            unitOfWork,
            new DomainError(
                ProtecaoDoCatalogoErrorCodes.ConflitoDeConcorrencia,
                "O tipo de documento foi alterado concorrentemente; confira a proteção e decida de novo."),
            cancellationToken).ConfigureAwait(false);
    }

    // O item foi editado depois de o aprovador vê-lo: aprovar ou negar a proteção vigente decidiria sobre o que ele não viu.
    // Repetir a mesma decisão nunca dá certo, então não é o conflito de concorrência que se tenta de novo.
    private static Result ProtecaoMudou() => Result.Failure(new DomainError(
        ProtecaoDoCatalogoErrorCodes.ProtecaoMudou,
        "A proteção do item mudou depois de conferida; releia o item e decida sobre a proteção atual."));

    private static FieldError ProtecaoConferidaInvalida(string campo) => new(campo, new DomainError(
        ProtecaoDoCatalogoErrorCodes.ProtecaoConferidaInvalida,
        "A decisão exige a proteção que o aprovador conferiu — classificação, hipótese legal e finalidade —, dentro do vocabulário fechado."));
}
