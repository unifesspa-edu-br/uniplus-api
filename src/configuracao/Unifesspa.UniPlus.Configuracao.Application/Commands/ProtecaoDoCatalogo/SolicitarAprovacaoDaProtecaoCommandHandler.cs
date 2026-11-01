namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Passa os itens pendentes à solicitação e avisa por e-mail quem tem a permissão de aprovar, na mesma transação da
/// gravação: o aviso não se perde se o processo cair depois dela. Só então a proteção de dados fica sabendo: criar ou
/// alterar o item não avisa ninguém. O envio sem item pendente, ou
/// sem ninguém que possa aprovar, é recusado antes de gravar — nos dois casos a solicitação não levaria a lugar nenhum.
/// </summary>
public static class SolicitarAprovacaoDaProtecaoCommandHandler
{
    public static async Task<(Result<SolicitacaoDaProtecaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        SolicitarAprovacaoDaProtecaoCommand command,
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

        AcessoAoCatalogoDeProtecao acesso = command.Acesso;
        if (!acesso.ManterTipos && !acesso.ManterFatos)
        {
            return Recusa(AcessoAoCatalogoDeProtecao.SemPermissao);
        }

        IReadOnlyList<TipoDocumento> tiposPendentes = acesso.ManterTipos
            ? await tipos.ListarPendentesDeSolicitacaoAsync(cancellationToken).ConfigureAwait(false)
            : [];
        IReadOnlyList<FatoCandidato> fatosPendentes = acesso.ManterFatos
            ? await fatos.ListarPendentesDeSolicitacaoAsync(cancellationToken).ConfigureAwait(false)
            : [];
        if (tiposPendentes.Count == 0 && fatosPendentes.Count == 0)
        {
            return Recusa(new DomainError(
                ProtecaoDoCatalogoErrorCodes.SemItemPendente,
                "Nenhum tipo de documento nem fato do catálogo está pendente de solicitação."));
        }

        // Só o destinatário válido recebe: a guarda conta os e-mails que o aviso efetivamente monta.
        IReadOnlyList<EnviarEmail> avisos = AvisosDaProtecaoDoCatalogo.DaSolicitacao(command.Destinatarios, tiposPendentes.Count, fatosPendentes.Count);
        if (avisos.Count == 0)
        {
            return Recusa(new DomainError(
                ProtecaoDoCatalogoErrorCodes.SemQuemAprove,
                "Ninguém tem a permissão de aprovar a proteção de dados do catálogo para receber a solicitação; conceda-a antes de enviar."));
        }

        string ator = userContext.UserId ?? "system";
        DateTimeOffset agora = timeProvider.GetUtcNow();
        foreach (Result transicao in tiposPendentes.Select(t => t.SolicitarAprovacaoDaProtecao(ator, agora))
            .Concat(fatosPendentes.Select(f => f.SolicitarAprovacaoDaProtecao(ator, agora))))
        {
            if (transicao.IsFailure)
            {
                // Nada é gravado pela metade: o item que a transição recusou falha alto, e os já mudados são descartados.
                unitOfWork.DescartarAlteracoesNaoSalvas();
                return Recusa(transicao.Error!);
            }
        }

        Result salvo = await MutacaoDoTipo.SalvarAsync(
            unitOfWork,
            new DomainError(
                ProtecaoDoCatalogoErrorCodes.ConflitoDeConcorrencia,
                "Um item do catálogo foi alterado concorrentemente. Tente novamente."),
            cancellationToken).ConfigureAwait(false);
        if (salvo.IsFailure)
        {
            return Recusa(salvo.Error!);
        }

        return (
            Result<SolicitacaoDaProtecaoDto>.Success(new SolicitacaoDaProtecaoDto(tiposPendentes.Count, fatosPendentes.Count)),
            avisos);
    }

    private static (Result<SolicitacaoDaProtecaoDto> Resposta, IEnumerable<object> Mensagens) Recusa(DomainError erro) =>
        (Result<SolicitacaoDaProtecaoDto>.Failure(erro), []);
}
