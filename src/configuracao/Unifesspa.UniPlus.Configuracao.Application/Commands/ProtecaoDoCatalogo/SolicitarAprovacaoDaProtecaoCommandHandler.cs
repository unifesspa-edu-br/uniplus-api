namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Passa os itens pendentes escolhidos à solicitação e avisa por e-mail quem tem a permissão de aprovar, na mesma transação da
/// gravação: o aviso não se perde se o processo cair depois dela. Só então a proteção de dados fica sabendo: criar ou
/// alterar o item não avisa ninguém. A seleção vazia, o item que não
/// existe ou não está pendente, e a falta de quem possa aprovar são recusados antes de gravar.
/// </summary>
public static class SolicitarAprovacaoDaProtecaoCommandHandler
{
    private const string CadastroTipos = ProtecaoDoCatalogoMapping.TipoDocumento;
    private const string CadastroFatos = ProtecaoDoCatalogoMapping.Fato;

    public static async Task<(Result<SolicitacaoDaProtecaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        SolicitarAprovacaoDaProtecaoCommand command,
        ITipoDocumentoRepository tipos,
        IFatoCandidatoRepository fatos,
        IAtoDaProtecaoDoCatalogoRepository atos,
        IConfiguracaoUnitOfWork unitOfWork,
        IVerificadorDeAcesso verificador,
        ITitularesDaPermissao titulares,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(tipos);
        ArgumentNullException.ThrowIfNull(fatos);
        ArgumentNullException.ThrowIfNull(atos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(titulares);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<AcessoAoCatalogoDeProtecao> decidido = await AcessoAoCatalogoDeProtecao.VerificarAsync(verificador, cancellationToken).ConfigureAwait(false);
        if (decidido.IsFailure)
        {
            return Recusa(decidido.Error!);
        }

        AcessoAoCatalogoDeProtecao acesso = decidido.Value;
        if (!acesso.ManterTipos && !acesso.ManterFatos)
        {
            await acesso.RegistrarRecusaDosCadastrosAsync(verificador, tipos: true, fatos: true, cancellationToken).ConfigureAwait(false);
            return Recusa(AcessoAoCatalogoDeProtecao.SemPermissao);
        }

        if (command.Itens.Count == 0)
        {
            return Recusa(new DomainError(
                ProtecaoDoCatalogoErrorCodes.SolicitacaoSemItem, "Selecione ao menos um item do catálogo para enviar à proteção de dados."));
        }

        // A forma é conferida antes de qualquer leitura (ADR-0125); o item repetido conta uma vez.
        List<FieldError> invalidos = [];
        List<(int Indice, Guid Id)> itensDeTipo = [];
        List<(int Indice, Guid Id)> itensDeFato = [];
        for (int indice = 0; indice < command.Itens.Count; indice++)
        {
            ItemParaSolicitarDto? item = command.Itens[indice];
            string? cadastro = item?.Cadastro?.Trim();
            if (item is null)
            {
                invalidos.Add(new($"itens[{indice}]", new DomainError(
                    ProtecaoDoCatalogoErrorCodes.CadastroInvalido, "O item deve informar o cadastro e o identificador.")));
            }
            else if (cadastro is not (CadastroTipos or CadastroFatos))
            {
                invalidos.Add(new($"itens[{indice}].cadastro", new DomainError(
                    ProtecaoDoCatalogoErrorCodes.CadastroInvalido, $"O cadastro do item deve ser {CadastroTipos} ou {CadastroFatos}.")));
            }
            else if (item.Id == Guid.Empty)
            {
                invalidos.Add(ItemNaoPendente(indice));
            }
            else
            {
                (cadastro == CadastroTipos ? itensDeTipo : itensDeFato).Add((indice, item.Id));
            }
        }

        if (invalidos.Count > 0)
        {
            return (Result<SolicitacaoDaProtecaoDto>.ValidationFailure(invalidos), []);
        }

        HashSet<Guid> idsDeTipos = [.. itensDeTipo.Select(static i => i.Id)];
        HashSet<Guid> idsDeFatos = [.. itensDeFato.Select(static i => i.Id)];
        if ((idsDeTipos.Count > 0 && !acesso.ManterTipos) || (idsDeFatos.Count > 0 && !acesso.ManterFatos))
        {
            await acesso
                .RegistrarRecusaDosCadastrosAsync(verificador, tipos: idsDeTipos.Count > 0, fatos: idsDeFatos.Count > 0, cancellationToken)
                .ConfigureAwait(false);
            return Recusa(AcessoAoCatalogoDeProtecao.SemPermissao);
        }

        // Só os itens pedidos são lidos; o que não vier, por inexistente ou fora de pendente, recusa o lote.
        IReadOnlyList<TipoDocumento> tiposPendentes = idsDeTipos.Count > 0
            ? await tipos.ListarPendentesPorIdAsync(idsDeTipos, cancellationToken).ConfigureAwait(false)
            : [];
        IReadOnlyList<FatoCandidato> fatosPendentes = idsDeFatos.Count > 0
            ? await fatos.ListarPendentesPorIdAsync(idsDeFatos, cancellationToken).ConfigureAwait(false)
            : [];
        HashSet<Guid> tiposLidos = [.. tiposPendentes.Select(static t => t.Id)];
        HashSet<Guid> fatosLidos = [.. fatosPendentes.Select(static f => f.Id)];
        List<FieldError> naoPendentes =
        [
            .. itensDeTipo.Where(i => !tiposLidos.Contains(i.Id)).Concat(itensDeFato.Where(i => !fatosLidos.Contains(i.Id)))
                .OrderBy(static i => i.Indice).Select(static i => ItemNaoPendente(i.Indice)),
        ];
        if (naoPendentes.Count > 0)
        {
            return (Result<SolicitacaoDaProtecaoDto>.ValidationFailure(naoPendentes), []);
        }

        // Só o destinatário válido recebe: a guarda conta os e-mails que o aviso efetivamente monta.
        IReadOnlyList<string> destinatarios = await titulares
            .ListarEmailsAsync(PermissaoConfiguracao.ProtecaoDoCatalogoAprovar.Codigo, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> avisos = AvisosDaProtecaoDoCatalogo.DaSolicitacao(destinatarios, tiposPendentes.Count, fatosPendentes.Count);
        if (avisos.Count == 0)
        {
            return Recusa(new DomainError(
                ProtecaoDoCatalogoErrorCodes.SemQuemAprove,
                "Ninguém tem a permissão de aprovar a proteção de dados do catálogo para receber a solicitação; conceda-a antes de enviar."));
        }

        AtorDoAto ator = AtorDoToken.De(userContext);
        DateTimeOffset agora = timeProvider.GetUtcNow();
        foreach (Result transicao in tiposPendentes.Select(t => t.SolicitarAprovacaoDaProtecao(ator.Sub, agora))
            .Concat(fatosPendentes.Select(f => f.SolicitarAprovacaoDaProtecao(ator.Sub, agora))))
        {
            if (transicao.IsFailure)
            {
                // Nada é gravado pela metade: o item que a transição recusou falha alto, e os já mudados são descartados.
                unitOfWork.DescartarAlteracoesNaoSalvas();
                return Recusa(transicao.Error!);
            }
        }

        // Um ato por item enviado, gravado na mesma transação da solicitação.
        foreach (TipoDocumento tipo in tiposPendentes)
        {
            atos.Registrar(tipo, AtoDaProtecao.Solicitacao, ator, agora);
        }

        foreach (FatoCandidato fato in fatosPendentes)
        {
            atos.Registrar(fato, AtoDaProtecao.Solicitacao, ator, agora);
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

    private static FieldError ItemNaoPendente(int indice) => new($"itens[{indice}].id", new DomainError(
        ProtecaoDoCatalogoErrorCodes.ItemNaoPendente,
        "O item não existe ou não está pendente de solicitação; atualize a lista e selecione de novo."));

    private static (Result<SolicitacaoDaProtecaoDto> Resposta, IEnumerable<object> Mensagens) Recusa(DomainError erro) =>
        (Result<SolicitacaoDaProtecaoDto>.Failure(erro), []);
}
