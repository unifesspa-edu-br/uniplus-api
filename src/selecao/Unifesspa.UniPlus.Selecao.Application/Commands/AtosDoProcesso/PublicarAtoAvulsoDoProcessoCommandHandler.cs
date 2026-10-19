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

/// <summary>
/// Publica um ato avulso do processo: confere tudo o que o Publicações recusaria depois, grava o
/// registro do ato do processo e enfileira a requisição de registro, na mesma transação (ADR-0108).
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que conferir aqui.</b> O Publicações só responde o sucesso; a recusa vai para a fila de
/// mensagens que falharam. Cada regra que o catálogo ou o registro central aplicariam é antecipada com
/// erro nomeado, antes de qualquer escrita.
/// </para>
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
public static class PublicarAtoAvulsoDoProcessoCommandHandler
{
    private const string ConstraintDaCorrecaoUnica = "ux_atos_do_processo_ato_corrigido";

    public static async Task<(Result<AtoPublicadoDto> Resposta, IEnumerable<object> Eventos)> Handle(
        PublicarAtoAvulsoDoProcessoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        ITipoAtoPublicadoReader tipoDeAtoReader,
        IVagaDeLinhagemReader vagaDeLinhagemReader,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);
        ArgumentNullException.ThrowIfNull(tipoDeAtoReader);
        ArgumentNullException.ThrowIfNull(vagaDeLinhagemReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        // A forma da correção é do agregado e sai antes de qualquer leitura ou trava (ADR-0125).
        Result correcaoBemFormada = AtoDoProcesso.ValidarCorrecao(command.AtoCorrigidoId, command.MotivoDaCorrecao);
        if (correcaoBemFormada.IsFailure)
        {
            return (Result<AtoPublicadoDto>.ValidationFailure(correcaoBemFormada.Errors), []);
        }

        // As conferências abaixo leem para depois inserir (vaga do tipo único, documento já usado):
        // sem serializar as publicações do mesmo processo, duas requisições passam juntas e as duas
        // gravam. O lock cai com a transação, depois que o registro do ato já é visível à seguinte.
        await processoSeletivoRepository
            .TravarAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        string? orgao = await processoSeletivoRepository
            .ObterSiglaDaUnidadeAdministradoraAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (orgao is null)
        {
            return Recusa(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        CertameDivulgado? divulgado = await certameDivulgadoRepository
            .ObterParaLeituraAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (divulgado is null)
        {
            return Recusa(new DomainError(
                AtoDoProcessoErrorCodes.CertameNaoDivulgado,
                "O processo ainda não tem página pública: o documento avulso aparece na linha do tempo do certame, que nasce com a publicação do edital de abertura."));
        }

        Result<TipoAtoPublicadoView> conferenciaDoTipo = await ConferenciaDoTipoDoAtoAvulso
            .ConferirAsync(tipoDeAtoReader, command.TipoAtoCodigo, command.DataPublicacao, cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDoTipo.IsFailure)
        {
            return Recusa(conferenciaDoTipo.Error!);
        }

        TipoAtoPublicadoView tipo = conferenciaDoTipo.Value!;

        Result conferenciaDoAtoExigido = ConferenciaDoTipoDoAtoAvulso.ExigeAtoCorrigido(tipo, command.AtoCorrigidoId);
        if (conferenciaDoAtoExigido.IsFailure)
        {
            return Recusa(conferenciaDoAtoExigido.Error!);
        }

        Result<IReadOnlyList<Guid>> conferenciaDoCorrigido = await ConferenciaDoAtoCorrigido
            .ConferirAsync(atoDoProcessoRepository, processoSeletivoRepository, vagaDeLinhagemReader, command.ProcessoSeletivoId, command.AtoCorrigidoId, cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDoCorrigido.IsFailure)
        {
            return Recusa(conferenciaDoCorrigido.Error!);
        }

        // O ato que corrige integra a linhagem do corrigido, e a vaga do objeto é da linhagem.
        Result conferenciaDaVaga = await ConferenciaDoTipoDeAto
            .VagaDoObjetoAsync(vagaDeLinhagemReader, tipo, command.ProcessoSeletivoId, conferenciaDoCorrigido.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDaVaga.IsFailure)
        {
            return Recusa(conferenciaDaVaga.Error!);
        }

        Result conferenciaDaVagaLocal = await ConferenciaDoTipoDoAtoAvulso
            .VagaNoRegistroLocalAsync(atoDoProcessoRepository, tipo, command.ProcessoSeletivoId, conferenciaDoCorrigido.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDaVagaLocal.IsFailure)
        {
            return Recusa(conferenciaDaVagaLocal.Error!);
        }

        Result<DocumentosDoAtoConferidos> conferenciaDosDocumentos = await ConferenciaDosDocumentosDoAtoAvulso
            .ConferirAsync(documentoAPublicarRepository, atoDoProcessoRepository, command.ProcessoSeletivoId, command.DocumentoPrincipalId, command.Anexos ?? [], cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDosDocumentos.IsFailure)
        {
            return Recusa(conferenciaDosDocumentos.Error!);
        }

        DocumentosDoAtoConferidos documentos = conferenciaDosDocumentos.Value!;

        AtoDoProcesso versaoDivulgada = await atoDoProcessoRepository
            .ObterComAnexosAsync(divulgado.AtoCriadorId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"A divulgação do processo {command.ProcessoSeletivoId} aponta para o ato {divulgado.AtoCriadorId}, que não tem registro do ato do processo.");

        Result<AtoDoProcesso> registrado = AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                AtoId: Guid.CreateVersion7(),
                ProcessoSeletivoId: command.ProcessoSeletivoId,
                TipoCodigo: tipo.Codigo,
                TipoNome: tipo.Nome,
                Numero: command.Numero,
                DataDoAto: command.DataPublicacao,
                Assinante: command.Assinante,
                UnidadeAdministradora: versaoDivulgada.UnidadeAdministradora,
                IdentificadorLegivelDoProcesso: versaoDivulgada.IdentificadorLegivelDoProcesso,
                DocumentoPrincipalId: documentos.Principal.Id,
                Anexos: [.. documentos.Anexos.Select(static a => new AnexoDeclarado(a.Documento.Id, a.Nome))],
                AtoCorrigidoId: command.AtoCorrigidoId,
                MotivoDaCorrecao: command.MotivoDaCorrecao),
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
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) == ConstraintDaCorrecaoUnica)
        {
            // Corrida entre duas correções do mesmo ato. A recusa não devolve exceção, e o Wolverine salva
            // de novo depois do handler: sem descartar o rastreamento, a mesma violação estoura fora do
            // catch (ADR-0119).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Recusa(new DomainError(
                AtoDoProcessoErrorCodes.AtoCorrigidoJaCorrigido,
                ConferenciaDoAtoCorrigido.MensagemDoAtoJaCorrigido(command.AtoCorrigidoId!.Value, corretor: null)));
        }

        RegistrarAtoNormativoRequisicao requisicao = new(
            AtoId: registro.Id,
            Orgao: orgao,
            // A série de numeração do órgão é o próprio tipo: aviso numera com aviso.
            Serie: tipo.Codigo,
            Ano: command.DataPublicacao.Year,
            Numero: registro.Numero,
            TipoCodigo: tipo.Codigo,
            DataPublicacao: command.DataPublicacao,
            DocumentoHash: documentos.Principal.HashSha256!,
            Assinante: registro.Assinante,
            // Nenhuma versão de configuração: o ato avulso não congela.
            VersaoInvocadaId: null,
            VersaoInvocadaHash: null,
            AtoRetificadoId: registro.AtoCorrigidoId,
            MotivoRetificacao: registro.MotivoDaCorrecao,
            Vinculos: [RequisicaoDeRegistroDoAto.VinculoDoProcesso(command.ProcessoSeletivoId)],
            AtributosDoTipo: RequisicaoDeRegistroDoAto.AtributosDoTipo(tipo),
            Anexos: [.. documentos.Anexos.Select(static a => new AnexoDoAtoRequisicao(a.Nome, a.Documento.TipoDeMidia, a.Documento.HashSha256!))]);

        return (Result<AtoPublicadoDto>.Success(new AtoPublicadoDto(registro.Id)), [requisicao]);
    }

    private static (Result<AtoPublicadoDto> Resposta, IEnumerable<object> Eventos) Recusa(DomainError erro) =>
        (Result<AtoPublicadoDto>.Failure(erro), []);
}
