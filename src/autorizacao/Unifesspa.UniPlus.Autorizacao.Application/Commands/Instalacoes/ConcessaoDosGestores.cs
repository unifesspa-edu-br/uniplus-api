namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A concessão dos dois gestores da instalação de código aceito, na mesma transação: qualquer falha reverte tudo. A
/// corrida entre instalações se decide pela contagem dos gestores ativos sob a trava deles: a que chega depois não tem mais
/// como valer.
/// </summary>
/// <remarks>
/// A ordem de travas é a única de todo caminho que concede ou revoga concessão de governança: o perfil de gestor, a
/// vigência do termo de responsabilidade (compartilhada), as pessoas dos gestores, em ordem de emissor e <c>sub</c>, e por
/// último a trava dos gestores, que a revogação também toma depois da pessoa. O gestor acumula com qualquer outro papel, e
/// a instalação nunca recusa por incompatibilidade. Os gestores instalados não aceitam o termo: a exceção fica registrada
/// com a vigência lida, e a instalação segue (ADR-0162).
/// </remarks>
public sealed class ConcessaoDosGestores
{
    private readonly IInstalacaoRepository _instalacoes;
    private readonly IConcessaoRepository _concessoes;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;
    private readonly IIncompatibilidadeDosPapeis _papeis;
    private readonly IAutorizacaoUnitOfWork _unitOfWork;
    private readonly ITermoDeResponsabilidadeRepository _termos;

    public ConcessaoDosGestores(
        IInstalacaoRepository instalacoes,
        IConcessaoRepository concessoes,
        ICatalogoDePermissoesConsultavel catalogo,
        IIncompatibilidadeDosPapeis papeis,
        IAutorizacaoUnitOfWork unitOfWork,
        ITermoDeResponsabilidadeRepository termos)
    {
        ArgumentNullException.ThrowIfNull(instalacoes);
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(papeis);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(termos);

        _instalacoes = instalacoes;
        _concessoes = concessoes;
        _catalogo = catalogo;
        _papeis = papeis;
        _unitOfWork = unitOfWork;
        _termos = termos;
    }

    internal async Task<Result> ConcederAsync(Instalacao instalacao, Identidade autor, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        (Perfil perfil, VersaoDoTermo? vigente) = await TravarAsync(instalacao, cancellationToken).ConfigureAwait(false);

        // A contagem roda sob a trava dos gestores: duas confirmações simultâneas de instalações diferentes não passam as duas.
        bool haGestor = await _concessoes
            .ContarGestoresAtivosAsync(PerfisDeSistema.GestorDeAcessosId, Guid.Empty, agora, cancellationToken)
            .ConfigureAwait(false) > 0;

        return haGestor
            ? await RecusarCorridaPerdidaAsync(instalacao, autor, agora, cancellationToken).ConfigureAwait(false)
            : await InstalarAsync(instalacao, perfil, vigente, autor, agora, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(Perfil Perfil, VersaoDoTermo? Vigente)> TravarAsync(Instalacao instalacao, CancellationToken cancellationToken)
    {
        Perfil perfil = await _concessoes.ObterParaConcederAsync(PerfisDeSistema.GestorDeAcessosId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("O perfil de sistema do gestor de acessos não existe: a carga inicial não rodou.");
        VersaoDoTermo? vigente = await _termos.ObterVigenteSobTravaCompartilhadaAsync(cancellationToken).ConfigureAwait(false);

        await _papeis.TravarAsync(instalacao.Gestores.Select(static g => new Identidade(g.Emissor, g.Sub)), cancellationToken).ConfigureAwait(false);
        await _concessoes.TravarGovernancaParaEscritaAsync(cancellationToken).ConfigureAwait(false);

        return (perfil, vigente);
    }

    // Outra instalação venceu a corrida: esta não tem mais como valer, e o código certo não a mantém pendente. A tentativa
    // não é "aceita": nada foi confirmado. Se nem a recusa se grava, os gestores já estão instalados e a chave fica livre.
    private async Task<Result> RecusarCorridaPerdidaAsync(Instalacao instalacao, Identidade autor, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        instalacao.Cancelar(agora);
        await RegistrarTentativaAsync(instalacao, autor, ResultadoDaTentativaDeInstalacao.JaHaviaGestor, agora, cancellationToken).ConfigureAwait(false);

        DomainError? falha = await GravacaoDaConfirmacao.GravarAsync(_unitOfWork, cancellationToken).ConfigureAwait(false);

        return Result.Failure(falha is null ? InstalacaoHandlerErros.JaHaGestor() : InstalacaoHandlerErros.JaHaGestorAoIniciar());
    }

    private async Task<Result> InstalarAsync(
        Instalacao instalacao, Perfil perfil, VersaoDoTermo? vigente, Identidade autor, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        instalacao.Confirmar(agora);
        Result<IReadOnlyList<Concessao>> concessoes = Conceder(instalacao, perfil, autor, agora);
        if (concessoes.IsFailure)
        {
            _unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.ValidationFailure(concessoes.Errors);
        }

        await AdicionarAsync(concessoes.Value!, cancellationToken).ConfigureAwait(false);
        await _termos.AdicionarExcecaoDaInstalacaoAsync(TermoNaInstalacao.Registrar(instalacao.Id, vigente, agora), cancellationToken).ConfigureAwait(false);
        await RegistrarTentativaAsync(instalacao, autor, ResultadoDaTentativaDeInstalacao.Aceita, agora, cancellationToken).ConfigureAwait(false);
        DomainError? falha = await GravacaoDaConfirmacao.GravarAsync(_unitOfWork, cancellationToken).ConfigureAwait(false);

        return falha is null ? Result.Success() : Result.Failure(falha);
    }

    private Result<IReadOnlyList<Concessao>> Conceder(Instalacao instalacao, Perfil perfil, Identidade autor, DateTimeOffset agora)
    {
        OrigemDaConcessao origem = new(TipoDeOrigem.Instalacao, instalacao.Id);
        AtoDoAgente ato = new(autor, agora);
        Result<Concessao>[] concedidas =
        [
            .. instalacao.Gestores.Select(g => Concessao.Conceder(
                new TitularDaConcessao(new Identidade(g.Emissor, g.Sub, g.Nome), g.Email), perfil, EscopoDaConcessao.DaInstituicao(), origem, ato, _catalogo)),
        ];
        FieldError[] erros = [.. concedidas.Where(static c => c.IsFailure).SelectMany(static c => c.Errors)];

        return erros.Length > 0
            ? Result<IReadOnlyList<Concessao>>.ValidationFailure(erros)
            : Result<IReadOnlyList<Concessao>>.Success([.. concedidas.Select(static c => c.Value!)]);
    }

    private async Task AdicionarAsync(IReadOnlyList<Concessao> concessoes, CancellationToken cancellationToken)
    {
        foreach (Concessao concessao in concessoes)
        {
            await _concessoes.AdicionarAsync(concessao, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task RegistrarTentativaAsync(
        Instalacao instalacao, Identidade autor, ResultadoDaTentativaDeInstalacao resultado, DateTimeOffset agora, CancellationToken cancellationToken) =>
        _instalacoes.AdicionarTentativaAsync(TentativaDeInstalacao.Registrar(instalacao.Id, autor, resultado, agora), cancellationToken);
}
