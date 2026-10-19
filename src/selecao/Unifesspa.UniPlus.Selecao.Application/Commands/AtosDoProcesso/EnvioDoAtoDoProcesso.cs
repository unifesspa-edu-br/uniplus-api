namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProdutosPrevistos;

/// <summary>O órgão que numera o ato e a divulgação do certame, conferidos antes de qualquer escrita.</summary>
/// <param name="Orgao">Sigla da unidade administradora do processo.</param>
/// <param name="Divulgado">A divulgação do certame, de onde vêm a unidade e o identificador da chave no acervo.</param>
internal sealed record ProcessoDivulgadoConferido(string Orgao, CertameDivulgado Divulgado);

/// <summary>Tudo o que a conferência decidiu sobre o ato e que o envio grava e manda ao Publicações.</summary>
/// <param name="ProcessoSeletivoId">O processo que publica o ato.</param>
/// <param name="Processo">O órgão e a divulgação do certame.</param>
/// <param name="Tipo">O tipo do ato como o catálogo o declarava na conferência.</param>
/// <param name="Numero">Número que o documento declara, quando o tipo o tem.</param>
/// <param name="DataPublicacao">Data que o documento declara.</param>
/// <param name="Assinante">Quem assina o ato.</param>
/// <param name="Documentos">O documento principal e os anexos já conferidos.</param>
/// <param name="AtoCorrigidoId">O ato que este corrige, quando é errata ou retificação.</param>
/// <param name="MotivoDaCorrecao">Motivo da correção, junto com <paramref name="AtoCorrigidoId"/>.</param>
/// <param name="ItemDoEdital">A identidade do item do edital que o ato cumpre; nula no documento avulso.</param>
/// <param name="PoliticaConfirmadaPorSub">Quem confirmou a política de divulgação; nulo quando ela não foi pedida.</param>
internal sealed record AtoAEnviar(
    Guid ProcessoSeletivoId,
    ProcessoDivulgadoConferido Processo,
    TipoAtoPublicadoView Tipo,
    string? Numero,
    DateOnly DataPublicacao,
    string Assinante,
    DocumentosDoAtoConferidos Documentos,
    Guid? AtoCorrigidoId,
    string? MotivoDaCorrecao,
    Domain.ValueObjects.IdentidadeDoItemDoEdital? ItemDoEdital = null,
    string? PoliticaConfirmadaPorSub = null);

/// <summary>
/// O que o documento avulso e a publicação de um item do edital têm em comum: confere que o processo
/// tem página pública e, no fim, grava o registro do ato do processo e enfileira a requisição de
/// registro na mesma transação (ADR-0108).
/// </summary>
/// <remarks>
/// <para>
/// <b>Idempotência.</b> Vem da <c>Idempotency-Key</c> do endpoint: o <c>AtoId</c> é decidido a cada
/// requisição e fica no registro do ato, que é o que liga o aviso de registro a ele.
/// </para>
/// <para>
/// <b>Campos que o documento não declara.</b> O órgão é a sigla da unidade administradora do processo, a
/// série de numeração é o próprio tipo do ato e o ano é o da data que o documento declara. A unidade e
/// o identificador do processo na chave do documento no acervo são os da versão divulgada, gravados no
/// registro porque a chave não muda depois de publicada.
/// </para>
/// </remarks>
internal static class EnvioDoAtoDoProcesso
{
    private const string ConstraintDaCorrecaoUnica = "ux_atos_do_processo_ato_corrigido";
    private const string ConstraintDaPublicacaoOriginalUnica = "ux_atos_do_processo_item_original";

    public static async Task<Result<ProcessoDivulgadoConferido>> ConferirProcessoDivulgadoAsync(
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        Guid processoSeletivoId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);

        string? orgao = await processoSeletivoRepository
            .ObterSiglaDaUnidadeAdministradoraAsync(processoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (orgao is null)
        {
            return Falha($"Processo Seletivo {processoSeletivoId} não encontrado.", "ProcessoSeletivo.NaoEncontrado");
        }

        CertameDivulgado? divulgado = await certameDivulgadoRepository
            .ObterParaLeituraAsync(processoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        return divulgado is null
            ? Falha(
                "O processo ainda não tem página pública: os atos do processo aparecem na linha do tempo do certame, que nasce com a publicação do edital de abertura.",
                AtoDoProcessoErrorCodes.CertameNaoDivulgado)
            : Result<ProcessoDivulgadoConferido>.Success(new ProcessoDivulgadoConferido(orgao, divulgado));
    }

    public static async Task<(Result<AtoPublicadoDto> Resposta, IEnumerable<object> Eventos)> EnviarAsync(
        AtoAEnviar ato,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ato);
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Guid atoCriadorId = ato.Processo.Divulgado.AtoCriadorId;
        AtoDoProcesso versaoDivulgada = await atoDoProcessoRepository
            .ObterComAnexosAsync(atoCriadorId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"A divulgação do processo {ato.ProcessoSeletivoId} aponta para o ato {atoCriadorId}, que não tem registro do ato do processo.");

        DocumentosDoAtoConferidos documentos = ato.Documentos;
        Result<AtoDoProcesso> registrado = AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                AtoId: Guid.CreateVersion7(),
                ProcessoSeletivoId: ato.ProcessoSeletivoId,
                TipoCodigo: ato.Tipo.Codigo,
                TipoNome: ato.Tipo.Nome,
                Numero: ato.Numero,
                DataDoAto: ato.DataPublicacao,
                Assinante: ato.Assinante,
                UnidadeAdministradora: versaoDivulgada.UnidadeAdministradora,
                IdentificadorLegivelDoProcesso: versaoDivulgada.IdentificadorLegivelDoProcesso,
                DocumentoPrincipalId: documentos.Principal.Id,
                Anexos: [.. documentos.Anexos.Select(static a => new AnexoDeclarado(a.Documento.Id, a.Nome))],
                ItemDoEdital: ato.ItemDoEdital,
                AtoCorrigidoId: ato.AtoCorrigidoId,
                MotivoDaCorrecao: ato.MotivoDaCorrecao,
                PoliticaDeDivulgacaoConfirmadaPorSub: ato.PoliticaConfirmadaPorSub),
            timeProvider);
        if (registrado.IsFailure)
        {
            return (Result<AtoPublicadoDto>.ValidationFailure(registrado.Errors), []);
        }

        AtoDoProcesso registro = registrado.Value!;
        await atoDoProcessoRepository.AdicionarAsync(registro, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (RecusaDaUnicidade(ex, ato) is { } recusa)
        {
            // Corrida entre duas escritas do mesmo item ou do mesmo ato corrigido. A recusa não devolve
            // exceção, e o Wolverine salva de novo depois do handler: sem descartar o rastreamento, a
            // mesma violação estoura fora do catch (ADR-0119).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return (Result<AtoPublicadoDto>.Failure(recusa), []);
        }

        RegistrarAtoNormativoRequisicao requisicao = new(
            AtoId: registro.Id,
            Orgao: ato.Processo.Orgao,
            // A série de numeração do órgão é o próprio tipo: aviso numera com aviso.
            Serie: ato.Tipo.Codigo,
            Ano: ato.DataPublicacao.Year,
            Numero: registro.Numero,
            TipoCodigo: ato.Tipo.Codigo,
            DataPublicacao: ato.DataPublicacao,
            DocumentoHash: documentos.Principal.HashSha256!,
            Assinante: registro.Assinante,
            // Nenhuma versão de configuração: o ato do processo não congela.
            VersaoInvocadaId: null,
            VersaoInvocadaHash: null,
            AtoRetificadoId: registro.AtoCorrigidoId,
            MotivoRetificacao: registro.MotivoDaCorrecao,
            Vinculos: [RequisicaoDeRegistroDoAto.VinculoDoProcesso(ato.ProcessoSeletivoId)],
            AtributosDoTipo: RequisicaoDeRegistroDoAto.AtributosDoTipo(ato.Tipo),
            Anexos: [.. documentos.Anexos.Select(static a => new AnexoDoAtoRequisicao(a.Nome, a.Documento.TipoDeMidia, a.Documento.HashSha256!))]);

        return (Result<AtoPublicadoDto>.Success(new AtoPublicadoDto(registro.Id)), [requisicao]);
    }

    private static DomainError? RecusaDaUnicidade(Exception ex, AtoAEnviar ato) =>
        UniqueConstraintViolation.GetViolatedConstraint(ex) switch
        {
            ConstraintDaCorrecaoUnica when ato.AtoCorrigidoId is { } corrigido => new DomainError(
                AtoDoProcessoErrorCodes.AtoCorrigidoJaCorrigido,
                ConferenciaDoAtoCorrigido.MensagemDoAtoJaCorrigido(corrigido, corretor: null)),
            ConstraintDaPublicacaoOriginalUnica when ato.ItemDoEdital is not null => new DomainError(
                AtoDoProcessoErrorCodes.ProdutoJaPublicado,
                ProjecaoDosProdutosPrevistos.MensagemDoItemJaPublicado),
            _ => null,
        };

    private static Result<ProcessoDivulgadoConferido> Falha(string mensagem, string codigo) =>
        Result<ProcessoDivulgadoConferido>.Failure(new DomainError(codigo, mensagem));
}
