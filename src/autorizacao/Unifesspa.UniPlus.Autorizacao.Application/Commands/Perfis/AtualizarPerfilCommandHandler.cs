namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da edição de perfil: substitui nome, descrição e permissões.</summary>
public static class AtualizarPerfilCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarPerfilCommand command,
        IPerfilRepository repository,
        IConcessaoRepository concessoes,
        ICatalogoDePermissoesConsultavel catalogo,
        CatalogoDaAplicacao catalogoDaAplicacao,
        IAutorizacaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(catalogoDaAplicacao);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Perfil? perfil = await repository.ObterParaEditarAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (perfil is null)
        {
            return Result.Failure(PerfilHandlerErros.NaoEncontrado(command.Id));
        }

        // Com concessões não revogadas, a decisão lê as permissões atuais do perfil: editar é conceder
        // a todos os beneficiários, então o que amplia o acesso só entra por perfil novo.
        bool temConcessoes = await concessoes.ExisteNaoRevogadaDoPerfilAsync(perfil.Id, cancellationToken).ConfigureAwait(false);
        TipoDeEscopo? alcanceAnterior = perfil.Alcance(catalogo);
        string[] permissoesAnteriores = [.. perfil.Permissoes.Select(static p => p.Codigo)];

        Result atualizar = perfil.Atualizar(command.Nome, command.Descricao, command.Permissoes, catalogo);
        if (atualizar.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return atualizar;
        }

        if (temConcessoes
            && ProtegeAsConcessoes(perfil, alcanceAnterior, permissoesAnteriores, catalogo, catalogoDaAplicacao) is { } recusa)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(recusa);
        }

        if (await repository.NomeExisteAsync(perfil.Nome, exceto: perfil.Id, cancellationToken).ConfigureAwait(false))
        {
            // O perfil já foi mutado em memória e segue rastreado: sem o descarte, a
            // gravação que o outbox dispara depois do handler persistiria o nome repetido.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(PerfilHandlerErros.NomeDuplicado());
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (PerfilViolacoes.EhConflitoDeNome(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(PerfilHandlerErros.NomeDuplicado());
        }
        catch (Exception ex) when (PerfilViolacoes.EhConflitoDeConcorrencia(ex))
        {
            // Duas edições a partir da mesma versão não se misturam: a segunda recebe o
            // conflito em vez de sobrescrever a lista de permissões da primeira. Traduzir aqui,
            // e não deixar propagar, completa a entrada de idempotência normalmente.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(PerfilHandlerErros.ConflitoDeConcorrencia());
        }

        return Result.Success();
    }

    private static DomainError? ProtegeAsConcessoes(
        Perfil perfil,
        TipoDeEscopo? alcanceAnterior,
        string[] permissoesAnteriores,
        ICatalogoDePermissoesConsultavel catalogo,
        CatalogoDaAplicacao catalogoDaAplicacao)
    {
        if (perfil.Alcance(catalogo) != alcanceAnterior)
        {
            return PerfilHandlerErros.AlcanceAlterado();
        }

        // Cada permissão pessoal ou sensível acrescentada amplia o acesso de quem já tem a concessão, ainda
        // que o perfil já fosse tão sensível quanto ela. Código fora do catálogo conta como sensível.
        bool amplia = perfil.Permissoes
            .Select(static p => p.Codigo)
            .Where(codigo => !permissoesAnteriores.Contains(codigo, StringComparer.Ordinal))
            .Any(codigo => PerfilMapping.SensibilidadeDe(codigo, catalogoDaAplicacao) >= ClassificacaoDeProtecao.Pessoal);

        return amplia ? PerfilHandlerErros.AmpliacaoExigeProtecaoDeDados() : null;
    }
}
