namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Passa à solicitação os itens pendentes escolhidos, dos cadastros que quem envia mantém, e avisa por e-mail quem tem a
/// permissão de aprovar e quem tem a de se manifestar, no outbox da mesma transação: o aviso não se perde se o processo
/// cair depois dela (ADR-0153, UNI-REQ-0373). A seleção vazia, o item que não existe ou não está pendente, e a falta de
/// quem possa aprovar são recusados antes de gravar; a falta de quem possa se manifestar não impede a solicitação.
/// </summary>
public sealed class SolicitacaoDaProtecaoDoCatalogo
{
    private static readonly DomainError SemItem = new(
        ProtecaoDoCatalogoErrorCodes.SolicitacaoSemItem, "Selecione ao menos um item do catálogo para enviar à proteção de dados.");

    private static readonly DomainError SemQuemAprove = new(
        ProtecaoDoCatalogoErrorCodes.SemQuemAprove,
        "Ninguém tem a permissão de aprovar a proteção de dados do catálogo para receber a solicitação; conceda-a antes de enviar.");

    private readonly ITipoDocumentoRepository _tipos;
    private readonly IFatoCandidatoRepository _fatos;
    private readonly IAtoDaProtecaoDoCatalogoRepository _atos;
    private readonly IConfiguracaoUnitOfWork _unitOfWork;
    private readonly ITitularesDaPermissao _titulares;
    private readonly TimeProvider _relogio;

    public SolicitacaoDaProtecaoDoCatalogo(
        ITipoDocumentoRepository tipos,
        IFatoCandidatoRepository fatos,
        IAtoDaProtecaoDoCatalogoRepository atos,
        IConfiguracaoUnitOfWork unitOfWork,
        ITitularesDaPermissao titulares,
        TimeProvider relogio)
    {
        _tipos = tipos ?? throw new ArgumentNullException(nameof(tipos));
        _fatos = fatos ?? throw new ArgumentNullException(nameof(fatos));
        _atos = atos ?? throw new ArgumentNullException(nameof(atos));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _titulares = titulares ?? throw new ArgumentNullException(nameof(titulares));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
    }

    /// <summary>
    /// Envia os itens e devolve, como mensagens de saída, os avisos da solicitação. O acesso a cada cadastro é decidido
    /// aqui, pelo <paramref name="verificador"/>, porque a solicitação alcança os dois cadastros de uma vez.
    /// </summary>
    public async Task<(Result<SolicitacaoDaProtecaoDto> Resposta, IEnumerable<object> Mensagens)> SolicitarAsync(
        SolicitarAprovacaoDaProtecaoCommand command, IVerificadorDeAcesso verificador, AtorDoAto ator, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<IReadOnlyList<IItemSobAprovacao>> pendentes = await ConferirAsync(command.Itens, verificador, cancellationToken).ConfigureAwait(false);
        Result<IReadOnlyList<EnviarEmail>> avisos = pendentes.IsFailure
            ? Result<IReadOnlyList<EnviarEmail>>.ValidationFailure(pendentes.Errors)
            : await AvisosAsync(pendentes.Value!, cancellationToken).ConfigureAwait(false);
        Result enviada = avisos.IsFailure
            ? Result.ValidationFailure(avisos.Errors)
            : await EnviarAsync(pendentes.Value!, ator, cancellationToken).ConfigureAwait(false);

        return enviada.IsFailure
            ? (Result<SolicitacaoDaProtecaoDto>.ValidationFailure(enviada.Errors), [])
            : (Result<SolicitacaoDaProtecaoDto>.Success(Resumo(pendentes.Value!)), avisos.Value!);
    }

    // O acesso, a forma e os itens pendentes, nessa ordem: nada é lido antes de o pedido ter forma e permissão.
    private async Task<Result<IReadOnlyList<IItemSobAprovacao>>> ConferirAsync(
        IReadOnlyList<ItemParaSolicitarDto?> itens, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        Result<AcessoAoCatalogoDeProtecao> acesso = await ConferirAcessoAsync(verificador, cancellationToken).ConfigureAwait(false);
        Result<PedidoDeSolicitacao> pedido = acesso.IsFailure
            ? Result<PedidoDeSolicitacao>.ValidationFailure(acesso.Errors)
            : itens.Count == 0 ? Result<PedidoDeSolicitacao>.Failure(SemItem) : PedidoDeSolicitacao.Ler(itens);
        Result permitido = pedido.IsFailure
            ? Result.ValidationFailure(pedido.Errors)
            : await ConferirCadastrosAsync(acesso.Value, pedido.Value!, verificador, cancellationToken).ConfigureAwait(false);

        return permitido.IsFailure
            ? Result<IReadOnlyList<IItemSobAprovacao>>.ValidationFailure(permitido.Errors)
            : await LerPendentesAsync(pedido.Value!, cancellationToken).ConfigureAwait(false);
    }

    // Quem não mantém nenhum dos dois cadastros não solicita; as negativas dos dois são registradas.
    private static async Task<Result<AcessoAoCatalogoDeProtecao>> ConferirAcessoAsync(IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        Result<AcessoAoCatalogoDeProtecao> decidido = await AcessoAoCatalogoDeProtecao.VerificarAsync(verificador, cancellationToken).ConfigureAwait(false);
        if (decidido.IsFailure || decidido.Value.ManterTipos || decidido.Value.ManterFatos)
        {
            return decidido;
        }

        await decidido.Value.RegistrarRecusaDosCadastrosAsync(verificador, tipos: true, fatos: true, cancellationToken).ConfigureAwait(false);
        return Result<AcessoAoCatalogoDeProtecao>.Failure(AcessoAoCatalogoDeProtecao.SemPermissao);
    }

    private static async Task<Result> ConferirCadastrosAsync(
        AcessoAoCatalogoDeProtecao acesso, PedidoDeSolicitacao pedido, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        bool pedeTipos = pedido.IdsDe(CadastroDoCatalogo.TipoDocumento).Count > 0;
        bool pedeFatos = pedido.IdsDe(CadastroDoCatalogo.Fato).Count > 0;
        if ((pedeTipos && !acesso.ManterTipos) || (pedeFatos && !acesso.ManterFatos))
        {
            await acesso.RegistrarRecusaDosCadastrosAsync(verificador, pedeTipos, pedeFatos, cancellationToken).ConfigureAwait(false);
            return Result.Failure(AcessoAoCatalogoDeProtecao.SemPermissao);
        }

        return Result.Success();
    }

    // Só os itens pedidos são lidos; o que não vier, por inexistente ou fora de pendente, recusa o lote.
    private async Task<Result<IReadOnlyList<IItemSobAprovacao>>> LerPendentesAsync(PedidoDeSolicitacao pedido, CancellationToken cancellationToken)
    {
        HashSet<Guid> idsDeTipos = pedido.IdsDe(CadastroDoCatalogo.TipoDocumento);
        HashSet<Guid> idsDeFatos = pedido.IdsDe(CadastroDoCatalogo.Fato);
        IReadOnlyList<TipoDocumento> tipos = idsDeTipos.Count > 0
            ? await _tipos.ListarPendentesPorIdAsync(idsDeTipos, cancellationToken).ConfigureAwait(false) : [];
        IReadOnlyList<FatoCandidato> fatos = idsDeFatos.Count > 0
            ? await _fatos.ListarPendentesPorIdAsync(idsDeFatos, cancellationToken).ConfigureAwait(false) : [];
        IReadOnlyList<FieldError> naoPendentes = pedido.NaoPendentes(
            tipos.Select(static t => t.Id).ToHashSet(), fatos.Select(static f => f.Id).ToHashSet());

        return naoPendentes.Count > 0
            ? Result<IReadOnlyList<IItemSobAprovacao>>.ValidationFailure(naoPendentes)
            : Result<IReadOnlyList<IItemSobAprovacao>>.Success([.. tipos.Select(static t => new TipoSobAprovacao(t)), .. fatos.Select(static f => new FatoSobAprovacao(f))]);
    }

    // Só o destinatário válido conta. Quem aprova recebe o aviso da decisão; quem se manifesta, o da manifestação, uma
    // vez por pessoa: quem já é avisado como aprovador não recebe um segundo e-mail.
    private async Task<Result<IReadOnlyList<EnviarEmail>>> AvisosAsync(IReadOnlyList<IItemSobAprovacao> itens, CancellationToken cancellationToken)
    {
        SolicitacaoDaProtecaoDto resumo = Resumo(itens);
        IReadOnlyList<string> aprovadores = await _titulares.ListarEmailsAsync(PermissaoConfiguracao.ProtecaoDoCatalogoAprovar.Codigo, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> aosAprovadores = AvisosDaProtecaoDoCatalogo.DaSolicitacao(aprovadores, resumo.TiposDeDocumento, resumo.Fatos);
        if (aosAprovadores.Count == 0)
        {
            return Result<IReadOnlyList<EnviarEmail>>.Failure(SemQuemAprove);
        }

        IReadOnlyList<string> quemSeManifesta = await _titulares.ListarEmailsAsync(PermissaoConfiguracao.ProtecaoDoCatalogoManifestar.Codigo, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> aQuemSeManifesta = AvisosDaProtecaoDoCatalogo.DaSolicitacaoParaManifestacao(
            [.. quemSeManifesta.Except(aprovadores, StringComparer.OrdinalIgnoreCase)], resumo.TiposDeDocumento, resumo.Fatos);
        return Result<IReadOnlyList<EnviarEmail>>.Success([.. aosAprovadores, .. aQuemSeManifesta]);
    }

    // Nada é gravado pela metade: o item que a transição recusa falha alto, e os já mudados são descartados. Um ato por
    // item enviado, gravado na mesma transação da solicitação.
    private async Task<Result> EnviarAsync(IReadOnlyList<IItemSobAprovacao> itens, AtorDoAto ator, CancellationToken cancellationToken)
    {
        DateTimeOffset agora = _relogio.GetUtcNow();
        Result? recusada = itens.Select(i => i.Solicitar(ator.Sub, agora)).FirstOrDefault(static t => t.IsFailure);
        if (recusada is not null)
        {
            _unitOfWork.DescartarAlteracoesNaoSalvas();
            return recusada;
        }

        foreach (IItemSobAprovacao item in itens)
        {
            _atos.Adicionar(AtoDaProtecaoDoCatalogo.Registrar(item.Retrato(), AtoDaProtecao.Solicitacao, ator, agora));
        }

        return await GravacaoDoCircuitoDaProtecao.SalvarAsync(_unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static SolicitacaoDaProtecaoDto Resumo(IReadOnlyList<IItemSobAprovacao> itens) =>
        new(itens.Count(static i => i.Item.Cadastro == CadastroDoCatalogo.TipoDocumento), itens.Count(static i => i.Item.Cadastro == CadastroDoCatalogo.Fato));
}
