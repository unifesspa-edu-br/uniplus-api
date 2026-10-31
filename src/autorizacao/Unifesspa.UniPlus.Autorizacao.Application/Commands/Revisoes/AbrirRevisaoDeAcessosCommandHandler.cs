namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da abertura. Só a proteção de dados abre. A revisão traz as concessões ativas de perfil pessoal ou sensível
/// no instante da abertura, pelo mesmo critério com que o aprovador avalia o perfil. Só uma revisão fica em andamento
/// por vez, conferida sob trava; a revisão sem nenhum item nasce concluída.
/// </summary>
public static class AbrirRevisaoDeAcessosCommandHandler
{
    public static async Task<Result<RevisaoDeAcessosDto>> Handle(
        AbrirRevisaoDeAcessosCommand command,
        IRevisaoDeAcessosRepository revisoes,
        IConcessaoRepository concessoes,
        IVerificadorDeAcesso verificador,
        CatalogoDaAplicacao catalogo,
        IAutorizacaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(revisoes);
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        AcessoDaRevisao? acesso = await AcessoDaRevisao.ConsultarAsync(verificador, cancellationToken).ConfigureAwait(false);
        if (acesso is null)
        {
            return Result<RevisaoDeAcessosDto>.Failure(RevisaoHandlerErros.IdentidadeIncompleta());
        }

        if (IdentidadeDoToken.De(userContext) is not { } abertaPor)
        {
            return Result<RevisaoDeAcessosDto>.Failure(RevisaoHandlerErros.AutorNaoIdentificado());
        }

        if (!acesso.Conduz)
        {
            return Result<RevisaoDeAcessosDto>.Failure(RevisaoHandlerErros.SemPermissao());
        }

        DateTimeOffset agora = timeProvider.GetUtcNow();

        // A conferência e a gravação rodam sob a trava: duas aberturas simultâneas não passam as duas.
        await revisoes.TravarAberturaAsync(cancellationToken).ConfigureAwait(false);
        if (await revisoes.ExisteEmAndamentoAsync(agora, cancellationToken).ConfigureAwait(false))
        {
            return Result<RevisaoDeAcessosDto>.Failure(RevisaoHandlerErros.JaHaEmAndamento());
        }

        IReadOnlyList<Perfil> perfis = await revisoes.ListarPerfisComConcessaoAtivaAsync(agora, cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, Perfil> pessoaisOuSensiveis = perfis
            .Where(p => PerfilMapping.Avaliar(p, catalogo).ExigeAprovacaoDeProtecaoDeDados)
            .ToDictionary(static p => p.Id);

        IReadOnlyList<(Guid ConcessaoId, Guid PerfilId, Identidade Beneficiario)> ativas = await revisoes
            .ListarConcessoesAtivasDosPerfisAsync([.. pessoaisOuSensiveis.Keys], agora, cancellationToken)
            .ConfigureAwait(false);

        // Os acessos da proteção de dados, de qualquer perfil, são revistos pelos gestores: é a mesma pergunta que a
        // aprovação do pedido faz sobre o beneficiário.
        Dictionary<Identidade, bool> comProtecaoDeDados = [];
        foreach (Identidade beneficiario in ativas.Select(static c => c.Beneficiario).Distinct())
        {
            comProtecaoDeDados[beneficiario] = await concessoes
                .TemConcessaoAtivaDoPerfilAsync(beneficiario, PerfisDeSistema.ProtecaoDeDadosId, agora, cancellationToken)
                .ConfigureAwait(false);
        }

        RevisaoDeAcessos revisao = RevisaoDeAcessos.Abrir(
            abertaPor,
            ativas.Select(c => new ConcessaoARever(c.ConcessaoId, comProtecaoDeDados[c.Beneficiario])),
            agora);

        await revisoes.AdicionarAsync(revisao, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        RevisaoDetalhada detalhada = (await revisoes.ObterDetalhadaAsync(revisao.Id, agora, cancellationToken).ConfigureAwait(false))!;

        return Result<RevisaoDeAcessosDto>.Success(RevisaoMapping.ToDto(detalhada));
    }
}
